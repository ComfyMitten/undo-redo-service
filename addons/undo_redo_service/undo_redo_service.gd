@tool
extends Node
## A helper singleton, intended to be used only in the editor, that provides an easy way to queue up
## UndoRedo operations and commit them either as a standalone action, or as a merged action, with
## provisions for handling the common need of preventing invalid undo state as a result of a merge.
## Accessible in scripts as [code]UndoRedoService[/code]. It's recommended to avoid accessing this
## singleton at runtime by guarding with [code]Engine.is_editor_hint()[/code].
##
## NOTE: this service largely functions as a workaround for certain [EditorUndoRedoManager] &
## [UndoRedo] behaviors that aren't currently supported, but you could choose to instead compile the
## engine with tweaks applied to the
## [url=https://github.com/godotengine/godot/blob/4.7/editor/editor_undo_redo_manager.cpp]
## editor_undo_redo_manager.cpp[/url] and
## [url=https://github.com/godotengine/godot/blob/4.7/core/object/undo_redo.cpp]
## undo_redo.cpp[/url] C++ files for more direct, more efficient, and most importantly, more
## reliable behaviors to achieve the same result.

## Mirrors the [signal EditorUndoRedoManager.history_changed] signal, emitted whenever an action is
## commited or the undo/redo history is cleared.[br][br]
## NOTE: this can be very useful when there is a need to apply sequential updates to the same value
## in response to one base editor action.[br]
## For example, if multiple nodes are selected in the editor and modified simultaneously, and each
## one tries to increment a shared counter via [method queue_do_property], those modifications will
## replace each other and lead to the counter only being incremented once.[br]
## If however each instance uses [code]await UndoRedoService.history_changed[/code] before
## calculating the incremented value, they will all be applied sequentially as desired.[br]
## This can only be safely done when an action is currently being committed. In these cases, it is
## recommended to guard with an [method is_valid_operation_context] check with [code]true[/code]
## passed in, which determines whether an action being committed.
signal history_changed

## Mirrors the [signal EditorUndoRedoManager.version_changed] signal, emitted during undos & redos.
signal version_changed

## Mirrors the [enum EditorUndoRedoManager.SpecialHistory] enum used to designate unique ids for
## UndoRedo history instances other than the standard per-editor-scene histories.
## These can be used with [method get_history_undo_redo] for special use cases.
enum SpecialHistory {
	GLOBAL_HISTORY = 0,
	REMOTE_HISTORY = -9,
	INVALID_HISTORY = -99
}

## How long we can wait after an UndoRedo action is applied before the next one will become
## un-mergable, according to one of the hidden conditions of [method UndoRedo.create_action].
const ACTION_MERGE_TIME_THRESHOLD_MS = 800

## We need to track the time of the last UndoRedo's commit (via the [signal history_changed] signal)
## so we can know if too much time has passed for a new merge to be possible.
## [br][br]
## NOTE: this is currently set up to track the last action time across every scene's UndoRedos. If
## there are multiple scenes open in the editor, and they each save changes in quick succession,
## it's possible for the service to think a merge will be possible when it isn't. At worst this may
## lead to what should be a benign unmerged action, but it's worth understanding the limitation.
## [br][br]
## NOTE: the default value is the same as [constant INT64_MIN] in Godot 4.7+, set manually for
## compatibility.
var _last_undo_redo_action_time := -9223372036854775808:
	set(value):
		_last_undo_redo_action_time = value
		_merged_undo_operations_clear_timer.start(ACTION_MERGE_TIME_THRESHOLD_MS * .001)

## Clears the undo caches once enough time has passed that we're sure no new actions can be merged.
var _merged_undo_operations_clear_timer: Timer

## Tracks the undo property operations last committed to a merged editor action.
## Cleared automatically whenever we expect that a new action cannot merge into an old one.
var _merged_undo_properties_cache: Dictionary[Object, Array] = { }
## Tracks the undo method operations last committed to a merged editor action.
## Cleared automatically whenever we expect that a new action cannot merge into an old one.
var _merged_undo_methods_cache: Dictionary[Object, Array] = { }
## Tracks the undo reference operations last committed to a merged editor action.
## Cleared automatically whenever we expect that a new action cannot merge into an old one.
var _merged_undo_references_cache: Array[Object] = []

## Sequenced operations intended to belong to one yet-to-be-processed action. Committing an action
## removes these operations from the queue and processes them all at once shortly afterward.
var _queued_operations: Array[Callable] = []

## Internal reference to the [EditorUndoRedoManager].[br][br]
## NOTE: we're using the [Object] type instead of a reference to the [EditorUndoRedoManager]
## singleton so the script can still be parsed on exported builds.
var _undo_redo_manager: Object

## Whether this singleton is running in the editor.
@onready var _in_editor := Engine.is_editor_hint()


func _ready() -> void:
	if _in_editor:
		# NOTE: we indirectly retrieve the EditorInterface singleton instead of referring to the
		# autoload itself so that the script can still be parsed on exported builds.
		_undo_redo_manager = Engine.get_singleton(&"EditorInterface").get_editor_undo_redo()
		_undo_redo_manager.history_changed.connect(_on_editor_undo_redo_history_changed)
		_undo_redo_manager.version_changed.connect(version_changed.emit)
		
		_merged_undo_operations_clear_timer = Timer.new()
		_merged_undo_operations_clear_timer.one_shot = true
		add_child(_merged_undo_operations_clear_timer)
		_merged_undo_operations_clear_timer.timeout.connect(clear_merged_undo_operations_cache)


func _on_editor_undo_redo_history_changed() -> void:
	_last_undo_redo_action_time = Time.get_ticks_msec()
	history_changed.emit()


## Determine whether the caller should allow state-changing operations to run.
## [br][br]
## At runtime this returns [code]true[/code] since there is no UndoRedo step to worry about.
## [br][br]
## In the editor, the logic is more complicated. In most cases the only time it's not valid to run
## in-editor operations is when an undo or redo is currently in progress.
## Usually we don't want to run code as a side effect from an undo or redo, because the UndoRedo
## action already tracks and applies the before and after state automatically in those cases.
## [br][br]
## Unfortunately we can't directly tell if an undo or redo is in progress, but we can tell if a new
## commit is in progress, which should be the case any time code runs in the editor as a side effect
## of e.g. changing an exposed export property in the inspector, adding/removing/transforming nodes
## in the scene tree, and so on; any editor events that already create and commit UndoRedo actions
## in the History tab.
## [br][br]
## If the code is designed to run under those circumstances, then we can pass [code]true[/code] here
## to [param is_undo_redo_reaction], in which case we'll run a
## [method EditorUndoRedoMananger.is_committing_action] check and return [code]false[/code] if
## there is no mid-progress action already, indicating that this code ran due to an undo or redo and
## thus is invalid.
## [br][br]
## If the code is not meant to run in response to any other UndoRedo action, pass [code]false[/code]
## instead.
func is_valid_operation_context(is_undo_redo_reaction: bool) -> bool:
	return (
		not _in_editor or not is_undo_redo_reaction or _undo_redo_manager.is_committing_action()
	)


## Queue a 'do' [param method] call for an [param object] with new [param args] passed in.[br]
## See [method EditorUndoRedoManager.add_do_method] for details.
func queue_do_method(object: Object, method: StringName, ... args: Array) -> void:
	var do_method_args := [object, method]
	do_method_args.append_array(args)
	_queued_operations.append(_undo_redo_manager.add_do_method.bindv(do_method_args))


## Queue a 'do' property change for a [param property] on an [param object] with a new
## [param value] passed in.[br]
## See [method EditorUndoRedoManager.add_do_property] for details.
func queue_do_property(object: Object, property: StringName, value: Variant) -> void:
	_queued_operations.append(_undo_redo_manager.add_do_property.bind(object, property, value))


## Queue a 'do' reference to an [param object], allowing the object to be unreferenced or freed when
## the UndoRedo 'do' history is cleared.[br]
## See [method EditorUndoRedoManager.add_do_reference] for details.
func queue_do_reference(object: Object) -> void:
	_queued_operations.append(_undo_redo_manager.add_do_reference.bind(object))


## Queue an 'undo' [param method] call for an [param object] with old [param args] passed in.[br]
## See [method EditorUndoRedoManager.add_undo_method] for details.
func queue_undo_method(object: Object, method: StringName, ... args: Array) -> void:
	var undo_method_args := [object, method]
	undo_method_args.append_array(args)
	_queued_operations.append(_undo_redo_manager.add_undo_method.bindv(undo_method_args))


## Queue an 'undo' property change for a [param property] on an [param object] with an old
## [param value] passed in.[br]
## See [method EditorUndoRedoManager.add_undo_property] for details.
func queue_undo_property(object: Object, property: StringName, value: Variant) -> void:
	_queued_operations.append(_undo_redo_manager.add_undo_property.bind(object, property, value))


## Queue an 'undo' reference to an [param object], allowing the object to be unreferenced or freed
## when the UndoRedo 'undo' history is cleared.[br]
## See [method EditorUndoRedoManager.add_undo_reference] for details.
func queue_undo_reference(object: Object) -> void:
	_queued_operations.append(_undo_redo_manager.add_undo_reference.bind(object))


## Helper to queue both a do and undo [param property] change at once for an [param object]. The
## same [param property] will be modified each way, with the given [param new_value] applied on
## 'do', and an [param old_value] on 'undo'.
func queue_do_undo_property(
		object: Object,
		property: StringName,
		new_value: Variant,
		old_value: Variant,
) -> void:
	queue_do_property(object, property, new_value)
	queue_undo_property(object, property, old_value)


## Helper to queue both a do and undo [param method] call at once for one [param object]. The same
## [param method] will be called each way, with the given [param do_args] passed on 'do', and
## [param undo_args] passed on 'undo'.[br][br]
## NOTE: do/undo args are handled as arrays instead of varargs so we can know which belong to which.
func queue_do_undo_method(
		object: Object,
		method: StringName,
		do_args: Array,
		undo_args: Array,
) -> void:
	var do_method_args := [object, method]
	do_method_args.append_array(do_args)
	queue_do_method.callv(do_method_args)
	
	var undo_method_args := [object, method]
	undo_method_args.append_array(undo_args)
	queue_undo_method.callv(undo_method_args)


## Force any newly committed actions to not skip any initial 'undo' operation steps, by clearing the
## cache that it relies on. This only applies to merge commits, not standard ones.
func clear_merged_undo_operations_cache() -> void:
	_merged_undo_properties_cache.clear()
	_merged_undo_methods_cache.clear()
	_merged_undo_references_cache.clear()
	
	_merged_undo_operations_clear_timer.stop()


## Wrapper for [method EditorUndoRedoManager.clear_history]
func clear_history(id: int = -99, increase_version: bool = true) -> void:
	_undo_redo_manager.clear_history(id, increase_version)


## Wrapper for [method EditorUndoRedoManager.force_fixed_history]
func force_fixed_history() -> void:
	_undo_redo_manager.force_fixed_history()


## Wrapper for [method EditorUndoRedoManager.get_history_undo_redo]
func get_history_undo_redo(id: int) -> UndoRedo:
	return _undo_redo_manager.get_history_undo_redo(id)


## Wrapper for [method EditorUndoRedoManager.get_object_history_id]
func get_object_history_id(object: Object) -> int:
	return _undo_redo_manager.get_object_history_id(object)


## Wrapper for [method EditorUndoRedoManager.is_committing_action]
func is_committing_action() -> bool:
	return _undo_redo_manager.is_committing_action()


## Create a new UndoRedo action that will attempt to be merged into the previous one (when possible)
## using the queued operations from the `queue_` functions called previously, with the
## [constant UndoRedo.MERGE_ALL] merge mode. This will wipe the queued operations list immediately
## afterward.[br]
## If there is already an action being processed by the [EditorUndoRedoManager], this will
## automatically wait for it to finish before proceeding.[br]
## If we detect that the action can't be merged, [param standalone_action_name] will be used as its
## name.[br]
## It's technically possible, though very unlikely, that we may think an action can be merged when
## it cannot, in which case a new action with the same name as the last one will be created.[br]
## If [param skip_subsequent_undo_properties] is [code]true[/code] (default), only the first
## detected undo operation for any given property will be committed.[br]
## If [param skip_subsequent_undo_methods] is [code]true[/code] (default), only the first detected
## undo operation for any given method will be committed.[br]
## If [param skip_subsequent_undo_references] is [code]true[/code] (default), only the first
## detected undo operation for any given object reference will be committed.[br]
## These 3 params are what allow us to safely merge subsequent changes into editor UndoRedo actions,
## otherwise we could get invalid state when trying to undo an action with more than one merged
## change to the same property or method.[br]
## [param backward_undo_ops] is what determines the processing order of undo operations. It must be
## [code]false[/code] (default, representing [i]forward[/i] processing of undo operations) in order
## for this action to be mergeable into most engine-derived UndoRedo actions.[br]
## [param mark_unsaved] can be set to [code]false[/code] if you don't want the editor to treat this
## action as a change to the editor/game state (prompting the user to save in some circumstances),
## though in most cases this isn't relevant for merged editor actions.
## [br][br]
## We're making some big assumptions here that may require compromises; if your use case doesn't
## align with these expectations, use a different approach:
## [br]
## 1. It is acceptable to create a new action in the UndoRedo history if a merge isn't possible.[br]
## 2. The correct, matching [param backward_undo_ops] value is chosen for the previous action to
##    merge into. For most editor-derived actions, this will be the default of [code]false[/code].
##    [br]
## 3. All objects whose properties are modified and/or methods are called belong to the same scene.
##    [br]
## 4. Any queued object references & values will still be valid even if we need to wait for a
##    previous action to finish processing.[br]
## 5. No undo operations were added for operations that were already directly handled by the
##    editor's base action that we're merging into, e.g. if we changed an export property in the
##    inspector, we did not then add an undo operation that modifies that same property.[br]
## 6. It won't be problematic to skip some of the later undo ops that were added for the same
##    property/method (no side effects that only they would've supported the reversal of), e.g. if
##    a value changed from 0 -> 1 -> 2, undoing it directly from 2 -> 0 has the same end result as
##    undoing from 2 -> 1 -> 0, or in other words the initial undo operation is 'idempotent'.[br]
## 7. Understanding that this is a hacky workaround that ultimately has to take an educated guess as
##    to whether or not a new action will be merged, it's acceptable that the guess may occasionally
##    be incorrect, leading to a potentially invalid undo state. We do our best here and expect
##    it'll work well enough in practice, but we can't guarantee it. As a reminder, always use a
##    version control system (like git) and save your changes to it often.[br]
func commit_merge_action(
		standalone_action_name: String,
		skip_subsequent_undo_properties := true,
		skip_subsequent_undo_methods := true,
		skip_subsequent_undo_references := true,
		backward_undo_ops := false,
		mark_unsaved := true,
) -> void:
	var _merge_action_to_process := _process_undo_redo_action.bind(
			true,
			skip_subsequent_undo_properties,
			skip_subsequent_undo_methods,
			skip_subsequent_undo_references,
			standalone_action_name,
			UndoRedo.MERGE_ALL,
			null,
			backward_undo_ops,
			mark_unsaved,
			_queued_operations.duplicate(),
	)
	_queued_operations.clear()
	
	if _undo_redo_manager.is_committing_action():
		await _undo_redo_manager.history_changed
	
	_merge_action_to_process.call()


## A more straightforward commit operation that can be used in place of the
## [method EditorUndoRedoManager.create_action] and [method EditorUndoRedoManager.commit_action]
## methods, to process queued operations that don't need to be merged into an Editor action. See
## those methods for parameter details; in most cases, only [param action_name] should be set.
func commit_action(
		action_name: String,
		merge_mode: UndoRedo.MergeMode = UndoRedo.MERGE_DISABLE,
		custom_context: Object = null,
		backward_undo_ops := false,
		mark_unsaved := true,
) -> void:
	var _standard_action_to_process := _process_undo_redo_action.bind(
			false,
			false,
			false,
			false,
			action_name,
			merge_mode,
			custom_context,
			backward_undo_ops,
			mark_unsaved,
			_queued_operations.duplicate(),
	)
	_queued_operations.clear()
	
	if _undo_redo_manager.is_committing_action():
		await _undo_redo_manager.history_changed
	
	_standard_action_to_process.call()


## Process a queued UndoRedo action consisting of multiple operations (as a series of callables),
## with optional support for better handling of merges into existing editor-derived actions.
## [br][br]
## Godot's requirements for merging an action into another one:
## [br]
## 1. A non [constant UndoRedo.MERGE_DISABLE] merge mode must be chosen[br]
## 2. There must already be an action to merge into[br]
## 3. The last action's name must match the new one's name[br]
## 4. The last action's [param backward_undo_ops] mode must match the new one's
##    (default [code]false[/code])[br]
## 5. The last action must have been created or updated less than 800ms ago[br]
## [br]
## We can readily control for 1, 2, and 3 here. 5 can also be checked, but is a bit less reliable
## since we need to keep track of the last processed action's time manually and can't directly check
## at commit time whether the threshold has been passed from the UndoRedo's side. Sadly point 4
## can't be accounted for here since the [EditorUndoRedoManager] & [UndoRedo] APIs don't expose a
## way to check the specific parameters of an already-committed operation. So we provide an
## overridable [param backward_undo_ops] in the commit function defaulting to false that can be set
## if needed.
func _process_undo_redo_action(
		is_editor_merge_action: bool,
		merge_skip_subsequent_undo_properties: bool,
		merge_skip_subsequent_undo_methods: bool,
		merge_skip_subsequent_undo_references: bool,
		action_name: String,
		merge_mode: UndoRedo.MergeMode,
		custom_context: Object,
		backward_undo_ops: bool,
		mark_unsaved: bool,
		operations: Array[Callable],
) -> void:
	if operations.is_empty():
		return
	
	# NOTE: we're assuming that only the `add_do_` and `add_undo_` functions are in our operations
	# list, and that each one has its arguments bound already. Each of those functions has a
	# reference object as its first parameter that we should be able to use to get the
	# scene-specific UndoRedo for those operations, from which we can check the action history.
	var undo_redo_for_scene: UndoRedo = _undo_redo_manager.get_history_undo_redo(
			_undo_redo_manager.get_object_history_id(operations[0].get_bound_arguments() [0])
	)
	
	var existing_actions_count := undo_redo_for_scene.get_history_count()
	
	
	# Fulfills merge requirements 2 and 5 to the best of our abilities.
	var is_valid_merge := (
		merge_mode != UndoRedo.MERGE_DISABLE and existing_actions_count > 0
		and _last_undo_redo_action_time + ACTION_MERGE_TIME_THRESHOLD_MS > Time.get_ticks_msec()
	)
	
	# If we know a new action will be created, clear the cache so new undos will be fully supported.
	if not is_valid_merge:
		clear_merged_undo_operations_cache()
	
	# Create and execute the action
	if is_editor_merge_action and is_valid_merge:
		action_name = undo_redo_for_scene.get_current_action_name()
	
	_undo_redo_manager.create_action(
			action_name,
			merge_mode,
			custom_context,
			backward_undo_ops,
			mark_unsaved,
	)
	
	for operation in operations:
		# For standard actions, we'll use the built-in EditorUndoRedoManager handling.
		if not is_editor_merge_action:
			operation.call()
		
		# More complex editor merge-compatible actions go through the caching step to prevent
		# subsequent undos of the same properties/methods/refs when they should be skipped.
		else:
			var args := operation.get_bound_arguments()
			var obj: Object = args[0]
			var operation_method_name := operation.get_method()
			
			if operation_method_name == &"add_undo_property":
				var property: StringName = args[1]
				
				if obj not in _merged_undo_properties_cache:
					_merged_undo_properties_cache[obj] = []
				
				if (
					merge_skip_subsequent_undo_properties
					and property in _merged_undo_properties_cache[obj]
				):
					continue
				
				_merged_undo_properties_cache[obj].append(property)
			elif operation_method_name == &"add_undo_method":
				var method: StringName = args[1]
				
				if obj not in _merged_undo_methods_cache:
					_merged_undo_methods_cache[obj] = []
				
				if merge_skip_subsequent_undo_methods and method in _merged_undo_methods_cache[obj]:
					continue
				
				_merged_undo_methods_cache[obj].append(method)
			elif operation_method_name == &"add_undo_reference":
				if merge_skip_subsequent_undo_references and obj in _merged_undo_references_cache:
					continue
				
				_merged_undo_references_cache.append(obj)
			
			operation.call()
	
	_undo_redo_manager.commit_action()
