using Godot;
using System;

namespace UndoRedoService;

public class UndoRedoService
{
    public readonly static string HistoryChangedSignalName = "history_changed";
    public readonly static string VersionChangedSignalName = "version_changed";

    private static Node? _instance;

    public static Node Instance =>
        _instance ??= ((SceneTree)Engine.GetMainLoop())
            .Root
            .GetNode<Node>("UndoRedoService");

#region signal helpers
    public static void ConnectHistoryChanged(Action action)
    {
        Instance.Connect(HistoryChangedSignalName, Callable.From(action));
    }

    public static void DisconnectHistoryChanged(Action action)
    {
        Instance.Disconnect(HistoryChangedSignalName, Callable.From(action));
    }

    public static void ConnectVersionChanged(Action action)
    {
        Instance.Connect(VersionChangedSignalName, Callable.From(action));
    }

    public static void DisconnectVersionChanged(Action action)
    {
        Instance.Disconnect(VersionChangedSignalName, Callable.From(action));
    }

    public static void ConnectHistoryChanged(Callable callable)
    {
        Instance.Connect(HistoryChangedSignalName, callable);
    }

    public static void DisconnectHistoryChanged(Callable callable)
    {
        Instance.Disconnect(HistoryChangedSignalName, callable);
    }

    public static void ConnectVersionChanged(Callable callable)
    {
        Instance.Connect(VersionChangedSignalName, callable);
    }

    public static void DisconnectVersionChanged(Callable callable)
    {
        Instance.Disconnect(VersionChangedSignalName, callable);
    }
#endregion

    /// <summary>
    /// <para>
    /// Determine whether the caller should allow state-changing operations to run.
    /// </para>
    /// 
    /// <para>
    /// At runtime this returns <c>true</c> since there is no UndoRedo step to worry about.
    /// In the editor, the logic is more complicated. In most cases the only time it's not valid to run
    /// in-editor operations is when an undo or redo is currently in progress.
    /// Usually we don't want to run code as a side effect from an undo or redo, because the UndoRedo
    /// action already tracks and applies the before and after state automatically in those cases.
    /// </para>
    /// 
    /// <para>
    /// Unfortunately we can't directly tell if an undo or redo is in progress, but we can tell if a new
    /// commit is in progress, which should be the case any time code runs in the editor as a side effect
    /// of e.g. changing an exposed export property in the inspector, adding/removing/transforming nodes
    /// in the scene tree, and so on; any editor events that already create and commit UndoRedo actions
    /// in the History tab.
    /// </para>
    /// 
    /// <para>
    /// If the code is designed to run under those circumstances, then we can pass [code]true[/code] here
    /// to <paramref name="isUndoRedoReaction"/> in which case we'll run a
    /// <see cref="IsCommittingAction"/> check and return <c>false</c> if
    /// there is no mid-progress action already, indicating that this code ran due to an undo or redo and
    /// thus is invalid.
    /// </para>
    /// 
    /// <para>
    /// If the code is not meant to run in response to any other UndoRedo action, pass <c>false</c>
    /// </para>
    /// 
    /// </summary>
    /// <param name="isUndoRedoReaction">
    /// <c>true</c> if the caller should only run as part of an editor Undo/Redo action.
    /// </param>
    /// <returns>
    /// <c>true</c> if the operation should proceed; otherwise <c>false</c>.
    /// </returns>
    public static bool IsValidOperationContext(bool isUndoRedoReaction)
    {
        return (bool)Instance.Call(MethodName.IsValidOperationContext, isUndoRedoReaction);
    }

    /// <summary>
    /// <para>
    /// Queues a 'do' <paramref name="method"/> call for an <paramref name="obj"/> with new <paramref name="args"/> passed in.
    /// </para>
    /// 
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
    /// <para>
    /// Queue a 'do' property change for a <paramref name="property"/> on an <paramref name="obj"/>
    /// with a new <paramref name="value"/> passed in.
    /// </para>
    /// 
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
    /// <para>
    /// Queue a 'do' reference to an <paramref name="obj"/>, allowing the object to be unreferenced or freed when
    /// the UndoRedo 'do' history is cleared.
    /// </para>
    /// 
    /// See <see cref="EditorUndoRedoManager.AddDoReference"/> for details.
    /// </summary>
    /// <param name="obj">Object reference to add.</param>
    public static void QueueDoReference(GodotObject obj)
    {
        Instance.Call(MethodName.QueueDoReference, obj);
    }

    /// <summary>
    /// <para>
    /// Queue an 'undo' <paramref name="method"/> call to an <paramref name="obj"/> with old <paramref name="args"/> passed in.
    /// </para>
    /// 
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
    /// <para>
    /// Queue an 'undo' property change for a <paramref name="property"/> on an <paramref name="obj"/> with an old
    /// <paramref name="value"/> passed in.
    /// </para>
    /// 
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
    /// <para>
    /// Queue an 'undo' reference to an <paramref name="obj"/>, allowing the object to be unreferenced or freed
    /// when the UndoRedo 'undo' history is cleared.
    /// </para>
    /// 
    /// See <see cref="EditorUndoRedoManager.AddUndoReference"/> for details.
    /// </summary>
    /// <param name="obj"></param>
    public static void QueueUndoReference(GodotObject obj)
    {
        Instance.Call(MethodName.QueueUndoReference, obj);
    }

    /// <summary>
    /// Helper to queue both a do and undo <paramref name="property"/> change at once for an <paramref name="obj"/>. The
    /// same <paramref name="property"/> will be modified each way, with the given <paramref name="newValue"/> applied on
    /// 'do', and an <paramref name="oldValue"/> on 'undo'.
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
    /// Helper to queue both a do and undo <paramref name="method"/> call at once for one <paramref name="obj"/>. The
    /// same <paramref name="method"/> will be called each way, with the given <paramref name="doArgs"/> passed on 'do',
    /// and <paramref name="undoArgs"/> passed on 'undo'.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// NOTE: do/undo args are handled as arrays instead of varargs so we can know which belong to which.
    /// </para>
    ///
    /// <para>
    /// If working with Godot collections, use <seealso cref="QueueDoUndoMethod(GodotObject, StringName, Godot.Collections.Array, Godot.Collections.Array)"/>
    /// </para>
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

    /// <summary>
    /// Overload of <see cref="QueueDoUndoMethod(GodotObject, StringName, Variant[], Variant[])"/>
    /// if you already have Godot arrays to avoid allocating new arrays.
    /// </summary>
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
    /// <inheritdoc cref="EditorUndoRedoManager.ClearHistory(int, bool)"/>
    /// </summary>
    public static void ClearHistory(int id = -99, bool increaseVersion = true)
    {
        Instance.Call(MethodName.ClearHistory, id, increaseVersion);
    }

    /// <summary>
    /// <inheritdoc cref="EditorUndoRedoManager.ForceFixedHistory()"/>
    /// </summary>
    public static void ForceFixedHistory()
    {
        Instance.Call(MethodName.ForceFixedHistory);
    }

    /// <summary>
    /// <inheritdoc cref="EditorUndoRedoManager.GetHistoryUndoRedo(int)"/>
    /// </summary>
    /// <remarks>
    /// Note: Use <see cref="UndoRedoService.GetObjectHistoryId(GodotObject)"/>
    /// </remarks>
    public static UndoRedo GetHistoryUndoRedo(int id)
    {
        return (UndoRedo)Instance.Call(MethodName.GetHistoryUndoRedo, id);
    }

    /// <summary>
    /// <inheritdoc cref="EditorUndoRedoManager.GetObjectHistoryId(GodotObject)"/>
    /// </summary>
    /// <remarks>
    /// Note: Use <see cref="UndoRedoService.GetHistoryUndoRedo(int)"/>
    /// </remarks>
    /// <returns></returns>
    public static int GetObjectHistoryId(GodotObject obj)
    {
        return (int)Instance.Call(MethodName.GetObjectHistoryId, obj);
    }

    /// <summary>
    /// <inheritdoc cref="EditorUndoRedoManager.IsCommittingAction()"/>
    /// </summary>
    /// <remarks>
    /// See <seealso cref="UndoRedoService.CommitAction(StringName, UndoRedo.MergeMode, GodotObject?, bool, bool)"/>
    /// </remarks>
    /// <returns></returns>
    public static bool IsCommittingAction()
    {
        return (bool)Instance.Call(MethodName.IsCommittingAction);
    }

    /// <summary>
    /// <para>
    /// Create a new UndoRedo action that will attempt to be merged into the previous one (when possible)
    /// using the queued operations from the `queue_` functions called previously, with the
    /// <see cref="UndoRedo.MERGE_ALL"/> merge mode. This will wipe the queued operations list immediately
    /// afterward.
    /// </para>
    ///
    /// <para>
    /// If there is already an action being processed by the <see cref="EditorUndoRedoManager"/>, this will
    /// automatically wait for it to finish before proceeding.
    /// If we detect that the action can't be merged, <paramref name="standaloneActionName"/> will be used as its
    /// name.
    /// </para>
    ///
    /// <para>
    /// It's technically possible, though very unlikely, that we may think an action can be merged when
    /// it cannot, in which case a new action with the same name as the last one will be created.
    /// </para>
    /// </summary>
    /// 
    /// <param name="standaloneActionName"></param>
    /// 
    /// <param name="skipSubsequentUndoProperties">
    /// If <c>true</c> (default), only the first detected undo operation for any given property will be committed.
    /// </param>
    /// 
    /// <param name="skipSubsequentUndoMethods">
    /// If <c>true</c> (default), only the first detected undo operation for any given method will be committed.
    /// </param>
    /// 
    /// <param name="skipSubsequentUndoReferences">
    /// If <c>true</c> (default), only the first detected undo operation for any given object reference will be committed.
    /// </param>
    /// 
    /// <param name="backwardUndoOps">
    /// Determines the processing order of undo operations. It must be <c>false</c> (default, representing <i>forward</i>
    /// processing of undo operations) in order for this action to be mergeable into most engine-derived UndoRedo actions.
    /// </param>
    /// 
    /// <param name="markUnsaved">
    /// Can be set to <c>false</c> if you don't want the editor to treat this action as a change to the editor/game state
    /// (prompting the user to save in some circumstances), though in most cases this isn't relevant for merged editor actions.
    /// </param>
    /// <remarks>
    /// <para>
    /// <paramref name="skipSubsequentUndoProperties"/>, <paramref name="skipSubsequentUndoMethods"/>,
    /// and <paramref name="skipSubsequentUndoReferences"/>
    /// are what allow us to safely merge subsequent changes into editor UndoRedo actions,
    /// otherwise we could get invalid state when trying to undo an action with more than one merged
    /// change to the same property or method.
    /// </para>
    /// <para>
    /// We're making some big assumptions here that may require compromises; if your use case doesn't
    /// align with these expectations, use a different approach as documented in the repo.
    /// </para>
    /// <para>
    /// As a reminder, <b>always</b> use a version control system (like git) and save your changes to it often.
    /// </para>
    /// </remarks>
    public static void CommitMergeAction(
        string standaloneActionName,
        bool skipSubsequentUndoProperties = true,
        bool skipSubsequentUndoMethods = true,
        bool skipSubsequentUndoReferences = true,
        bool backwardUndoOps = false,
        bool markUnsaved = true)
    {
        Instance.Call(
            MethodName.CommitMergeAction,
            standaloneActionName,
            skipSubsequentUndoProperties,
            skipSubsequentUndoMethods,
            skipSubsequentUndoReferences,
            backwardUndoOps,
            markUnsaved
        );
    }

    /// <summary>
    /// <para>
    /// A more straightforward commit operation that can be used in place of the
    /// <see cref="EditorUndoRedoManager.CreateAction(string, UndoRedo.MergeMode, GodotObject, bool, bool)"/> and
    /// <see cref="EditorUndoRedoManager.CommitAction(bool)"/> methods, to process queued operations that don't need
    /// to be merged into an Editor action.
    /// </para>
    /// 
    /// See those methods for parameter details; in most cases, only <paramref name="actionName"/> should be set.
    /// </summary>
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
        public static readonly StringName CommitMergeAction = new("commit_merge_action");

    }
}
