@tool
extends Node
## Demonstrates how to apply each type of supported undo/redo operation through the
## UndoRedoServices's "queue_" methods, corresponding to these base UndoRedo "add_" actions:
##
## - `add_do_property()` / `add_undo_property()`: Set a property on a given object when doing or
##   undoing the action. In this example, `randomize_list_item_numbers()` utilizes these to update
##   the `text` property of each Label in the container.
##
## - `add_do_method()` / `add_undo_method()`: Run a method on a given object with any number of
##   arguments passed in when doing or undoing the action. In this example, `add_list_item()` and
##   `remove_list_item()` use these to call the `add_child()` and `remove_child()` methods on the
##   container accordingly, and to set the nodes' owner so that they will show up in the editor's
##   scene tree.
##
## - `add_do_reference()` / `add_undo_reference()`: When doing or undoing an action, store a
##   reference to a given object, which will be unreferenced or freed when the UndoRedo's history is
##   cleared. This one is a bit less intuitive than the others and behaves a bit differently
##   depending on what type of object is passed in:
##   - RefCounted objects: When the history is cleared, the object will be unreferenced. So if the
##     UndoRedo was the only thing keeping a reference to the object, then it will automatically be
##     freed after the history clear. If you don't reference a RefCounted object for its UndoRedo
##     actions, you may risk it being freed before the history is cleared, potentially breaking
##     later undos and redos.
##   - Other non-RefCounted objects (e.g. Nodes): When the history is cleared, the object will be
##     immediately freed. This usage is primarily to avoid memory leaks; for example, you can use an
##     action to remove a node from the scene tree without freeing it immediately, allowing you to
##     repeatedly undo & redo the action while keeping the node in memory, and then it will finally
##     be freed after the history is cleared and the operations are set in stone.
##   In this example, we use these within `add_list_item()` and `remove_list_item()` to prevent
##   memory leaks when a node is removed from the container. You can see this in effect by running
##   `clear_undo_redo_history()`, which will print out a count of the orphaned nodes before the
##   clear, and a count of any orphaned nodes freed because of the clear. If you comment out the
##   `queue_do_reference()` and `queue_undo_reference()` calls, you'll see that orphaned nodes are
##   never freed and will continue to take up space in memory.

@export_tool_button("Add list item") var editor_add_list_item_button := add_list_item
@export_tool_button("Remove list item") var editor_remove_list_item_button := remove_list_item
@export_tool_button("Randomize item numbers") var editor_randomize_list_item_numbers_button := (
	randomize_list_item_numbers
)
@export_tool_button("Swap first two item numbers") var editor_swap_first_2_item_numbers_button := (
	swap_first_two_item_numbers
)
@export_tool_button("Randomize item colors") var editor_randomize_list_item_colors_button := (
	randomize_list_item_colors
)
@export_tool_button("Clear UndoRedo history") var editor_clear_undo_redo_history_button := (
	clear_undo_redo_history
)
## Whenever this value is modified in the editor, we'll react by queuing a text change on the first
## label in the container, and then creating a merge commit that reuses the Editor's automatically-
## generated UndoRedo action so that all changes get bundled into one, and undoing/redoing will
## apply or unapply them all simultaneously.
@export var update_first_item_number_input := 0:
	set(value):
		update_first_item_number_input = value
		# Don't create an UndoRedo action if the scene hasn't fully loaded yet.
		if not is_inside_tree():
			return
		# Only add custom UndoRedo operations if this code isn't running because of an undo or redo
		# event; see the function's doc comments for details.
		if not UndoRedoService.is_valid_operation_context(true):
			return

		var list_items := list_items_v_box_container.get_children()
		if list_items.is_empty():
			return

		var list_item: Label = list_items[0]
		UndoRedoService.queue_do_undo_property(
				list_item,
				&"text",
				"List Item %d" % update_first_item_number_input,
				list_item.text,
		)
		UndoRedoService.commit_merge_action("Update first list item's number")

## This is a more advanced version of the previous export var (update_first_item_number_input); the
## only functional difference is that instead of updating the item once with the new number, we
## sequentially update the item 3 times while still merging into the same UndoRedo action.
## The end result will be the number repeated 3 times in a row, like `111` for the input `1`.
## This simulates more advanced use cases of UndoRedo where you may want to make multiple changes to
## the same value from multiple sources that could trigger simultaneously.
##
## Use case example:
## - You shift-select 3 nodes in the scene tree, each with the same script + exposed @export var.
## - Each export var is configured to update a shared dictionary with its own unique key + value.
## - You update all 3 nodes' export var with a new value in the inspector simultaneously.
##
## In this described example, if you simply queue up property changes to replace the old dictionary
## with a new one each time, each dictionary wouldn't contain the other nodes' keys + values, so you
## would be left with an incomplete version after the final one is committed.
## So instead, here we need to wait for each previous commit to finish so our changes can be applied
## sequentially. See `_on_sequential_numbers_input_changed()` to see this in effect.
@export var update_first_item_with_multiple_sequential_numbers_input := 0:
	set(value):
		update_first_item_with_multiple_sequential_numbers_input = value
		_on_sequential_numbers_input_changed(true)
		_on_sequential_numbers_input_changed(false)
		_on_sequential_numbers_input_changed(false)

@onready var list_items_v_box_container: VBoxContainer = %ListItemsVBoxContainer

var _editor_toaster: Object ## Reference to the EditorToaster instance for in-editor messages
var _last_orphan_node_count := 0


func _ready() -> void:
	if not Engine.is_editor_hint():
		return
	
	_editor_toaster = Engine.get_singleton(&"EditorInterface").get_editor_toaster()
	_last_orphan_node_count = int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT))
	UndoRedoService.history_changed.connect(_on_undo_redo_history_changed)
	UndoRedoService.version_changed.connect(_on_undo_redo_version_changed)


func _on_undo_redo_history_changed() -> void:
	_editor_toaster.push_toast(
		"UndoRedo history changed! (new action was committed or the history was cleared)"
	)


func _on_undo_redo_version_changed() -> void:
	_editor_toaster.push_toast("UndoRedo version changed! (undo or redo was applied)")


func _on_sequential_numbers_input_changed(clear_old_label: bool) -> void:
	# Don't create an UndoRedo action if the scene hasn't fully loaded yet.
	if not is_inside_tree():
		return
	# Only add custom UndoRedo operations if this code isn't running because of an undo or redo
	# event; see the function's doc comments for details.
	if not UndoRedoService.is_valid_operation_context(true):
		return
	
	# IMPORTANT: we need to await the currently mid-commit UndoRedo action so our changes get
	# applied sequentially instead of potentially replacing each other.
	await UndoRedoService.history_changed
	
	var list_items := list_items_v_box_container.get_children()
	if list_items.is_empty():
		return
	
	var list_item: Label = list_items[0]
	var new_label_text := "List Item " if clear_old_label else list_item.text
	new_label_text += str(update_first_item_with_multiple_sequential_numbers_input)
	
	UndoRedoService.queue_do_undo_property(
			list_item,
			&"text",
			new_label_text,
			list_item.text,
	)
	UndoRedoService.commit_merge_action("Update first list item's number sequentially")


## Add a new Label node to the container. Uses `queue_do_method()` to add the node to the scene
## tree, and again to set its owner to this node so that it can be shown & saved. It uses
## `queue_undo_method()` to remove the node if the action is undone. We also use
## `queue_do_reference()` to store a reference to this node so that it can be automatically freed if
## the action is reverted and then cleared later, preventing a memory leak.
## You can try commenting out the `queue_do_reference()` line below, and run the
## `clear_undo_redo_history()` method to see the memory leak in action.
func add_list_item() -> void:
	var list_item := Label.new()
	list_item.text = "List Item %d" % (list_items_v_box_container.get_child_count() + 1)

	UndoRedoService.queue_do_method(list_items_v_box_container, &"add_child", list_item, true)
	UndoRedoService.queue_do_method(list_item, &"set_owner", self)
	UndoRedoService.queue_do_reference(list_item)
	UndoRedoService.queue_undo_method(list_items_v_box_container, &"remove_child", list_item)
	UndoRedoService.commit_action("Add list item")


## The reverse of `add_list_item()` above that removes the last Label node from the container using
## `queue_do_method()`, with `queue_undo_method()` used to re-add it to the container and reset its
## owner. We also make use of `queue_undo_reference()` to store a reference to the removed node so
## that it can be automatically freed if the action is applied and then cleared later, preventing a
## memory leak.
## You can try commenting out the `queue_undo_reference()` line below, and run the
## `clear_undo_redo_history()` method to see the memory leak in action.
func remove_list_item() -> void:
	var list_items := list_items_v_box_container.get_children()
	if list_items.is_empty():
		return
	var list_item: Label = list_items.back()

	UndoRedoService.queue_do_method(list_items_v_box_container, &"remove_child", list_item)
	UndoRedoService.queue_undo_method(list_items_v_box_container, &"add_child", list_item)
	UndoRedoService.queue_undo_method(list_item, &"set_owner", self)
	UndoRedoService.queue_undo_reference(list_item)
	UndoRedoService.commit_action("Remove list item")


## Randomize the item number order of all the Label nodes in the container. Makes use of
## `queue_do_undo_property()` to set each Label's `text` property to a new string and to revert
## each one back to their original value when the action is undone.
func randomize_list_item_numbers() -> void:
	var list_items := list_items_v_box_container.get_children()
	if list_items.is_empty():
		return

	var list_items_count := list_items.size()
	var list_item_indices := range(list_items_count)
	list_item_indices.shuffle()

	for i in list_items_count:
		var list_item: Label = list_items[i]
		var new_text := "List Item %d" % (list_item_indices[i] + 1)
		UndoRedoService.queue_do_undo_property(list_item, &"text", new_text, list_item.text)

	UndoRedoService.commit_action("Randomize list item numbers")


## Swap the text of the first two Label nodes in the container. Makes use of
## `queue_do_undo_property()` to set each Labels' `text` property to a new string and to revert each
## one back to their original value when the action is undone.
func swap_first_two_item_numbers() -> void:
	var list_items := list_items_v_box_container.get_children()
	if list_items.size() < 2:
		return

	var label_1: Label = list_items[0]
	var label_2: Label = list_items[1]

	UndoRedoService.queue_do_undo_property(label_1, &"text", label_2.text, label_1.text)
	UndoRedoService.queue_do_undo_property(label_2, &"text", label_1.text, label_2.text)
	UndoRedoService.commit_action("Swap first two item numbers")


## Randomize the color of each label in the container via it's `modulate` property.
## This version uses `queue_do_undo_method()` to show how the same method can be applied when an
## action is both done and undone.
func randomize_list_item_colors() -> void:
	var list_items := list_items_v_box_container.get_children()
	if list_items.is_empty():
		return

	for list_item: Label in list_items:
		UndoRedoService.queue_do_undo_method(
				list_item,
				&"set_modulate",
				[Color(randf(), randf(), randf())],
				[list_item.modulate],
		)

	UndoRedoService.commit_action("Randomize list item colors")


## Clears the UndoRedo history, preventing further undos/redos of committed actions, and freeing
## references to objects referenced by those actions. Here, we also print out the new orphan node
## count before and after the clear happens so you can see the effects of `queue_do_reference()` and
## `queue_undo_reference()` freeing the orphaned Label nodes that would otherwise cause memory
## leaks.
func clear_undo_redo_history() -> void:
	var orphan_nodes_before_history_clear := int(
			Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)
	)
	print(
			"New orphan nodes created since last UndoRedo history clear: %d"
			% (orphan_nodes_before_history_clear - _last_orphan_node_count)
	)

	UndoRedoService.clear_history()

	var orphan_nodes_after_history_clear := int(
			Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)
	)

	print(
			"Orphan nodes freed by UndoRedo history clear: %d"
			% (orphan_nodes_before_history_clear - orphan_nodes_after_history_clear)
	)

	_last_orphan_node_count = orphan_nodes_after_history_clear
