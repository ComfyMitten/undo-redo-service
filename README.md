![UndoRedo Service Logo](/assets/UndoRedoServiceBanner.png)

# UndoRedo Service
Improve your editor tooling with easier support for undo & redo operations in Godot!

![Godot Editor History Sample](/assets/EditorHistorySample.png)

**UndoRedo Service** is an addon for Godot 4 that allows you to more easily take advantage of the editor's built-in UndoRedo system to save changes to editor game state right as they happen. Everything is managed through the [UndoRedoService](addons/undo_redo_service/undo_redo_service.gd) singleton that becomes active once the addon is installed.

## UndoRedoService Features
- Provides safe access to the `EditorUndoRedoManager` singleton that won't crash your game on exported builds
- Queue up operations to be executed with a streamlined approach as compared to the built-in `UndoRedo` action flow
- Bundle do & undo property changes and method calls
- Merge UndoRedo actions together with `commit_merge_action`, allowing you to treat multiple undo/redo actions as one
- Safely apply queued operations without causing errors due to other unfinished `UndoRedo` actions
- Make & merge custom actions in response to native `UndoRedo` actions
- Includes a custom C# wrapper to simplify usage of the service in C# projects
- Provides GDScript & C# demo scenes that showcase how to use service in depth

## Installation
You can install the addon by copying the `addons/undo_redo_service/` folder over to your project's `addons/` directory, and enabling it in the Project Settings -> Plugins tab.

![Godot Project Settings Plugins Tab](/assets/ProjectSettingsEnablePlugin.png)

## Examples: Before & After

### 1. Updating an Object's property

#### Native UndoRedo
```gdscript
var undo_redo_manager := EditorInterface.get_editor_undo_redo()
undo_redo_manager.create_action("Edit Object property")
undo_redo_manager.add_do_property(my_object, &"my_property", new_value)
undo_redo_manager.add_undo_property(my_object, &"my_property", old_value)
undo_redo_manager.commit_action()
```
> [!WARNING]
> this example uses a direct `EditorUndoRedoManager` reference, but this would actually crash the exported version of the project at runtime if the script is loaded in.
> 
> The safe way to access this natively is via `Engine.get_singleton(&"EditorInterface").get_editor_undo_redo()`, which gets you a basic Object type with no autocompletion capabilites.

#### With UndoRedoService
```gdscript
UndoRedoService.queue_do_undo_property(my_object, &"my_property", new_value, old_value)
UndoRedoService.commit_action("Edit Object Property")
```
or
```gdscript
UndoRedoService.queue_do_property(my_object, &"my_property", new_value)
UndoRedoService.queue_undo_property(my_object, &"my_property", old_value)
UndoRedoService.commit_action("Edit Object Property")
```

### 2. Calling an Object's method

#### Native UndoRedo
```gdscript
var undo_redo_manager := EditorInterface.get_editor_undo_redo()
undo_redo_manager.create_action("Call Object method")
undo_redo_manager.add_do_method(my_object, &"my_method", new_arg_1, new_arg_2)
undo_redo_manager.add_undo_property(my_object, &"my_method", old_arg_1, old_arg_2)
undo_redo_manager.commit_action()
```

#### With UndoRedoService
```gdscript
UndoRedoService.queue_do_undo_method(my_object, &"my_method", [new_arg_1, new_arg_2], [old_arg_1, old_arg_2])
UndoRedoService.commit_action("Call Object method")
```
or
```gdscript
UndoRedoService.queue_do_method(my_object, &"my_method", new_arg_1, new_arg_2)
UndoRedoService.queue_undo_method(my_object, &"my_method", old_arg_1, old_arg_2)
UndoRedoService.commit_action("Call Object method")
```

### 3. Adding & removing nodes

#### Native UndoRedo
```gdscript
func add_list_item() -> void:
	var list_item := Label.new()
	
	var undo_redo_manager := EditorInterface.get_editor_undo_redo()
	undo_redo_manager.create_action("Add list item")
	undo_redo_manager.add_do_method(list_items_v_box_container, &"add_child", list_item, true)
	undo_redo_manager.add_do_method(list_item, &"set_owner", self)
	undo_redo_manager.add_do_reference(list_item)
	undo_redo_manager.add_undo_method(list_items_v_box_container, &"remove_child", list_item)
	undo_redo_manager.commit_action()

func remove_list_item(list_item: Label) -> void:
	var undo_redo_manager := EditorInterface.get_editor_undo_redo()
	undo_redo_manager.create_action("Remove list item")
	undo_redo_manager.add_do_method(list_items_v_box_container, &"remove_child", list_item)
	undo_redo_manager.add_undo_method(list_items_v_box_container, &"add_child", list_item)
	undo_redo_manager.add_undo_method(list_item, &"set_owner", self)
	undo_redo_manager.add_undo_reference(list_item)
	undo_redo_manager.commit_action()
```

#### With UndoRedoService
```gdscript
func add_list_item() -> void:
	var list_item := Label.new()
	
	UndoRedoService.queue_do_method(list_items_v_box_container, &"add_child", list_item, true)
	UndoRedoService.queue_do_method(list_item, &"set_owner", self)
	UndoRedoService.queue_do_reference(list_item)
	UndoRedoService.queue_undo_method(list_items_v_box_container, &"remove_child", list_item)
	UndoRedoService.commit_action("Add list item")

func remove_list_item(list_item: Label) -> void:
	UndoRedoService.queue_do_method(list_items_v_box_container, &"remove_child", list_item)
	UndoRedoService.queue_undo_method(list_items_v_box_container, &"add_child", list_item)
	UndoRedoService.queue_undo_method(list_item, &"set_owner", self)
	UndoRedoService.queue_undo_reference(list_item)
	UndoRedoService.commit_action("Remove list item")
```

### 4. Merging a new UndoRedo action into an Editor-created action

#### Native UndoRedo
```gdscript
# Running code in response to an editor-invoked UndoRedo action,
# e.g. in response to an export variable changing in the Inspector.

var undo_redo_manager := EditorInterface.get_editor_undo_redo()
var undo_redo_action_name := "Randomize sprite color"
		
# Wait for an in-progress action to complete if there is one
if undo_redo_manager.is_committing_action():
	# Find the current mid-commit action's name. We need to get the specific UndoRedo
	# reference to do this.
	var undo_redo := undo_redo_manager.get_history_undo_redo(
			undo_redo_manager.get_object_history_id(self)
	)
	
	undo_redo_action_name = undo_redo.get_current_action_name()
	
	await undo_redo_manager.history_changed
else:
	return # There wasn't a mid-commit action when we expected one, so exit early

var new_color := Color(randf(), randf(), randf())

undo_redo_manager.create_action(undo_redo_action_name, UndoRedo.MERGE_ALL)
undo_redo_manager.add_do_property(example_sprite, &"modulate", new_color)
undo_redo_manager.add_undo_property(example_sprite, &"modulate", example_sprite.modulate)
undo_redo_manager.commit_action()
```
> [!CAUTION]
> This native example doesn't account for a serious design flaw that can cause irrecoverable state errors when undoing/redoing an action with multiple actions merged into it. This comes down to an undo operation order limitation that, at least as of Godot 4.7, must be worked around via a caching method. 
> See our [UndoRedo Tooling Demo - Tool V7](https://github.com/ComfyMitten/godot-undo-redo-tooling-demo/blob/main/tool_scenes/tool_v7/tool_v7.gd) for a deeper look into why this fails and how to (mostly) work around it.

#### With UndoRedoService
```gdscript
# Running code in response to an editor-invoked UndoRedo action,
# e.g. in response to an export variable changing in the Inspector.

if not EditorUndoRedoHelper.is_valid_operation_context(is_undo_redo_reaction):
	return

var new_color := Color(randf(), randf(), randf())

EditorUndoRedoHelper.queue_do_undo_property(example_sprite, &"modulate", new_color, example_sprite.modulate)
# `commit_merge_action()` takes care of the `await` step, action name check, and undo operation
# caching for us! We just need to pass in a fallback action name.
EditorUndoRedoHelper.commit_merge_action("Randomize sprite color")
```

## Limitations
This addon is not a remake or redesign of the native `UndoRedo` system at large; it is more or less a streamlined wrapper over the existing `EditorUndoRedoManager` singleton, with a built-in undo operation caching mechanism that allows us to skip duplicate undo ops for merged actions when desired (to deal with the design flaw mentioned above).

This tool provides workarounds to facilitate some common UndoRedo needs, but ultimately it is limited to the UndoRedo API exposed from within Godot's internal library, and is unable to fix its core limitations. For example, it is impossible to know ahead of time with 100% certainty whether a newly created action will be able to merge in with a previous one.

The good news is that the native UndoRedo system is not actually that complicated; if you check out the Godot source code (C++) and look at [undo_redo.cpp](https://github.com/godotengine/godot/blob/4.7.1-stable/core/object/undo_redo.cpp) & [editor_undo_redo_manager.cpp](https://github.com/godotengine/godot/blob/4.7.1-stable/editor/editor_undo_redo_manager.cpp), you may find it relatively easy to modify these APIs to expose new functions and enable new behaviors! My hope is that over time, the UndoRedo system will improve enough that a tool like this one isn't necessary for more advanced use cases.

## Contribution

> [!NOTE]
> **UndoRedo Service** is intentionally very limited in its scope! 

If you encounter a bug with this tool, please report it by creating an Issue if one doesn't already exist for it! Make sure to describe bugs with sufficient detail, and ideally, include an explanation of how to reproduce them reliably.

We are open to taking in code contributions for bug fixes, and potentially even feature additions, but any new features should align well with the current goal and scope of this tool. **If you have a new feature in mind, please make a proposal for it first via an Issue and make sure we agree to the proposed changes!.** Keep in mind that we're already quite happy with the current scope of this tool and may not prioritize new additions, but we will consider pull requests for accepted proposals from the community. Human-written code only, please.

> [!NOTE]
> This project supports a C# wrapper, and as such the C# version of Godot is recommended, but not required. When opening the project in a non-C# version of Godot, it is safe to ignore the benign "This project uses C#" warning" that pops up in the project browser. Just be sure not to commit the `project.godot` settings change that the Engine will try to make to disable C# support.

## License

[MIT](LICENSE)

## Credits

[Godot Engine](https://godotengine.org/)
