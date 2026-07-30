using Godot;
using System.Linq;

namespace UndoRedoService;

public class UndoRedoService
{
    private static Node? _instance;

    public static Node Instance =>
        _instance ??= ((SceneTree)Engine.GetMainLoop())
            .Root
            .GetNode<Node>("UndoRedoService");

    /// <summary>
    /// Returns whether it's safe to run editor code in the current context.
    ///
    /// At runtime this always returns <c>true</c>. In the editor, it prevents
    /// code from running during Undo/Redo unless that's the expected context.
    /// </summary>
    /// <param name="requiresUndoRedoContext">
    /// <c>true</c> if the caller should only run as part of an editor Undo/Redo action.
    /// </param>
    /// <returns>
    /// <c>true</c> if the operation should proceed; otherwise <c>false</c>.
    /// </returns>
    public static bool IsValidOperationContext(bool requiresUndoRedoContext)
    {
        return (bool)Instance.Call(MethodName.IsValidOperationContext, requiresUndoRedoContext);
    }

    /// <summary>
    /// Queues a 'do' method call to run when the UndoRedo action is committed.
    /// See <see cref="EditorUndoRedoManager.AddDoMethod"/> for details.
    /// </summary>
    /// <param name="obj">Object that receives the method call.</param>
    /// <param name="method">Method name to invoke.</param>
    /// <param name="args">Arguments passed to the method.</param>
    public static void QueueDoMethod(GodotObject obj, StringName method, params Variant[] args)
    {
        var callArgs = new Variant[args.Length + 2];
        callArgs[0] = obj;
        callArgs[1] = method;

        args.CopyTo(callArgs, 2);

        Instance.Call(MethodName.QueueDoMethod, callArgs);
    }

    /// <summary>
    /// Queue a 'do' property change to run when the UndoRedo action is committed.
    /// See <see cref="EditorUndoRedoManager.AddDoProperty"/> for details.
    /// </summary>
    /// <param name="obj">Object that receives the property change.</param>
    /// <param name="property">Property to set</param>
    /// <param name="value">Value to set the property</param>
    public static void QueueDoProperty(GodotObject obj, StringName property, Variant value)
    {
        Instance.Call(MethodName.QueueDoProperty, obj, property, value);
    }

    /// <summary>
    /// Queue a 'do' reference to an object, allowing the object to be unreferenced or freed when the UndoRedo 'do' history is cleared; 
    /// see <see cref="EditorUndoRedoManager.AddDoReference"/> for details.
    /// </summary>
    /// <param name="obj"></param>
    public static void QueueDoReference(GodotObject obj)
    {
        Instance.Call(MethodName.QueueDoReference, obj);
    }

    /// <summary>
    /// Queue an 'undo' method call for an object.
    /// See <see cref="EditorUndoRedoManager.AddUndoMethod"/> for details.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="method"></param>
    /// <param name="args"></param>
    public static void QueueUndoMethod(GodotObject obj, StringName method, params Variant[] args)
    {
        var callArgs = new Variant[args.Length + 2];
        callArgs[0] = obj;
        callArgs[1] = method;

        args.CopyTo(callArgs, 2);

        Instance.Call(MethodName.QueueUndoMethod, callArgs);
    }

    /// <summary>
    /// Queue an 'undo' property change for a property on an object.
    /// See <see cref="EditorUndoRedoManager.AddUndoProperty"/> for details.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="property"></param>
    /// <param name="value"></param>
    public static void QueueUndoProperty(GodotObject obj, StringName property, Variant value)
    {
        Instance.Call(MethodName.QueueUndoProperty, obj, property, value);
    }

    /// <summary>
    /// Queue an 'undo' reference to an object, allowing the object to be unreferenced or freed when the
    /// UndoRedo 'undo' history is cleared. See <see cref="EditorUndoRedoManager.AddUndoReference"/> for details.
    /// </summary>
    /// <param name="obj"></param>
    public static void QueueUndoReference(GodotObject obj)
    {
        Instance.Call(MethodName.QueueUndoReference, obj);
    }

    /// <summary>
    /// Helper to queue both a do and undo property change at once for an object. The same property will
    /// be modified each way, with the given `new_value` applied on 'do', and an `old_value` on 'undo'.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="property"></param>
    /// <param name="newValue"></param>
    /// <param name="oldValue"></param>
    public static void QueueDoUndoProperty(
        GodotObject obj,
        StringName property,
        Variant newValue,
        Variant oldValue)
    {
        Instance.Call(
            MethodName.QueueDoUndoProperty,
            obj,
            property,
            newValue,
            oldValue
        );
    }

    /// <summary>
    /// Queues both a do and undo method call for the same object and method.
    /// </summary>
    /// <remarks>
    /// <paramref name="doArgs"/> are passed when the action is applied.
    /// <paramref name="undoArgs"/> are passed when the action is reverted.
    /// </remarks>
    public static void QueueDoUndoMethod(
        GodotObject obj,
        StringName method,
        Variant[] doArgs,
        Variant[] undoArgs)
    {
        QueueDoUndoMethod(
            obj,
            method,
            new Godot.Collections.Array(doArgs),
            new Godot.Collections.Array(undoArgs)
        );
    }

    private static void QueueDoUndoMethod(
        GodotObject obj,
        StringName method,
        Godot.Collections.Array doArgs,
        Godot.Collections.Array undoArgs)
    {
        Instance.Call(
            MethodName.QueueDoUndoMethod,
            obj,
            method,
            doArgs,
            undoArgs
        );
    }

    /// <summary>
    /// Force any newly committed actions to not skip any initial 'undo' operation steps, by clearing the
    /// cache that it relies on. This only applies to merge commits, not standard ones.
    /// </summary>
    public static void ClearMergedUndoOperationsCache()
    {
        Instance.Call(MethodName.ClearMergedUndoOperationsCache);
    }

    /// <summary>
    /// Wrapper for <see cref="EditorUndoRedoManager.ClearHistory(int, bool)"/>
    /// </summary>
    /// <param name="id"></param>
    /// <param name="increaseVersion"></param>
    public static void ClearHistory(int id = -99, bool increaseVersion = true)
    {
        Instance.Call(MethodName.ClearHistory, id, increaseVersion);
    }

    /// <summary>
    /// Wrapper for <see cref="EditorUndoRedoManager.ForceFixedHistory"/>
    /// </summary>
    public static void ForceFixedHistory()
    {
        Instance.Call(MethodName.ForceFixedHistory);
    }

    /// <summary>
    /// Wrapper for <see cref="EditorUndoRedoManager.GetHistoryUndoRedo(int)"/>
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static UndoRedo GetHistoryUndoRedo(int id)
    {
        return (UndoRedo)Instance.Call(MethodName.GetHistoryUndoRedo, id);
    }

    /// <summary>
    /// Wrapper for <see cref="EditorUndoRedoManager.GetObjectHistoryId(GodotObject)"/>
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static int GetObjectHistoryId(GodotObject obj)
    {
        return (int)Instance.Call(MethodName.GetObjectHistoryId, obj);
    }

    /// <summary>
    /// Wrapper for <see cref="EditorUndoRedoManager.IsCommittingAction"/>
    /// </summary>
    /// <returns></returns>
    public static bool IsCommittingAction()
    {
        return (bool)Instance.Call(MethodName.IsCommittingAction);
    }

    public static void CommitAction(
        StringName actionName,
        UndoRedo.MergeMode mergeMode = UndoRedo.MergeMode.Disable,
        GodotObject? customContext = null,
        bool backwardUndoOps = false,
        bool markUnsaved = true)
    {
        Instance.Call(
            MethodName.CommitAction,
            actionName,
            (int)mergeMode,
            customContext,
            backwardUndoOps,
            markUnsaved
        );
    }

    public static class MethodName
    {
        public static readonly StringName IsValidOperationContext = new("is_valid_operation_context");

        public static readonly StringName QueueDoMethod = new("queue_do_method");
        public static readonly StringName QueueDoProperty = new("queue_do_property");
        public static readonly StringName QueueDoReference = new("queue_do_reference");

        public static readonly StringName QueueUndoMethod = new("queue_undo_method");
        public static readonly StringName QueueUndoProperty = new("queue_undo_property");
        public static readonly StringName QueueUndoReference = new("queue_undo_reference");

        public static readonly StringName QueueDoUndoProperty = new("queue_do_undo_property");
        public static readonly StringName QueueDoUndoMethod = new("queue_do_undo_method");

        public static readonly StringName ClearMergedUndoOperationsCache = new("clear_merged_undo_operations_cache");
        public static readonly StringName ClearHistory = new("clear_history");
        public static readonly StringName ForceFixedHistory = new("force_fixed_history");
        public static readonly StringName GetHistoryUndoRedo = new("get_history_undo_redo");
        public static readonly StringName GetObjectHistoryId = new("get_object_history_id");

        public static readonly StringName IsCommittingAction = new("is_committing_action");
        public static readonly StringName CommitAction = new("commit_action");

    }
}
