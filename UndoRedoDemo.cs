using Godot;
using System.Collections.Generic;
using System.Linq;

namespace UndoRedoDemo;

using UndoRedoService = UndoRedoService.UndoRedoService;

[Tool]
public partial class UndoRedoDemo : Node
{
    [ExportToolButton("Add list item")]
    public Callable AddListItemButton => Callable.From(AddListItem);

    [ExportToolButton("Remove list item")]
    public Callable RemoveListItemButton => Callable.From(RemoveListItem);

    [ExportToolButton("Randomize item numbers")]
    public Callable RandomizeNumbersButton => Callable.From(RandomizeListItemNumbers);

    [ExportToolButton("Swap first two item numbers")]
    public Callable SwapFirstTwoItemNumbersButton => Callable.From(SwapFirstTwoItemNumbers);

    [ExportToolButton("Randomize item colors")]
    public Callable RandomizeColorsButton => Callable.From(RandomizeListItemColors);

    [ExportToolButton("Clear UndoRedo history")]
    public Callable ClearHistoryButton => Callable.From(ClearHistory);

    private int _updateFirstItemNumberInput;

    [Export]
    public int UpdateFirstItemNumberInput
    {
        get => _updateFirstItemNumberInput;
        set
        {
            _updateFirstItemNumberInput = value;

            if (!IsInsideTree())
                return;

            if (!UndoRedoService.IsValidOperationContext(true))
                return;

            var items = _listContainer.GetChildren()
                .OfType<Label>()
                .ToList();

            if (items.Count == 0)
                return;

            var item = items[0];

            UndoRedoService.QueueDoUndoProperty(
                item,
                "text",
                $"List Item {value}",
                item.Text
            );

            UndoRedoService.CommitMergeAction(
                "Update first list item's number"
            );
        }
    }

    private int _updateFirstItemWithMultipleSequentialNumbersInput;

    [Export]
    public int UpdateFirstItemWithMultipleSequentialNumbersInput
    {
        get => _updateFirstItemWithMultipleSequentialNumbersInput;
        set
        {
            _updateFirstItemWithMultipleSequentialNumbersInput = value;

            OnSequentialNumbersInputChanged(true);
            OnSequentialNumbersInputChanged(false);
            OnSequentialNumbersInputChanged(false);
        }
    }

    private EditorToaster _editorToaster;

    private long _lastOrphanNodeCount;
    private VBoxContainer _listContainer;

    public override void _Ready()
    {
        _listContainer = GetNode<VBoxContainer>("%ListItemsVBoxContainer");
        _lastOrphanNodeCount = (long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        if (!Engine.IsEditorHint()) return;

        _editorToaster = EditorInterface.Singleton.GetEditorToaster();
    }

    private void OnUndoRedoHistoryChanged()
    {
        _editorToaster.PushToast("UndoRedo history changed! (new action was committed or the history was cleared)");
    }

    private void OnUndoRedoVersionChanged()
    {
        _editorToaster.PushToast("UndoRedo version changed! (undo or redo was applied)");
    }

    private async void OnSequentialNumbersInputChanged(bool clearOldLabel)
    {
        if (!IsInsideTree()) return;

        if (!UndoRedoService.IsValidOperationContext(true)) return;

        await ToSignal(
            UndoRedoService.Instance,
            UndoRedoService.HistoryChangedSignalName
        );

        var items = _listContainer.GetChildren()
            .OfType<Label>()
            .ToList();

        if (items.Count == 0)
            return;

        var item = items.First();
        var newLabelText = clearOldLabel
            ? "List Item "
            : item.Text;
        newLabelText += UpdateFirstItemWithMultipleSequentialNumbersInput.ToString();

        UndoRedoService.QueueDoUndoProperty(item, "text", newLabelText, item.Text);
        UndoRedoService.CommitMergeAction("Update first list item's number sequentially");
    }

    private void AddListItem()
    {
        if (_listContainer == null)
            return;

        var label = new Label
        {
            Text = $"List Item {_listContainer.GetChildCount() + 1}"
        };

        UndoRedoService.QueueDoMethod(_listContainer, "add_child", label, true);
        UndoRedoService.QueueDoMethod(label, "set_owner", this);
        UndoRedoService.QueueDoReference(label);
        UndoRedoService.QueueUndoMethod(_listContainer, "remove_child", label);
        UndoRedoService.CommitAction("Add list item");
    }

    private void RemoveListItem()
    {
        if (_listContainer == null)
            return;

        var items = _listContainer.GetChildren()
            .OfType<Label>()
            .ToList();

        if (items.Count == 0)
            return;

        var label = items[^1];

        UndoRedoService.QueueDoMethod(_listContainer, "remove_child", label);
        UndoRedoService.QueueUndoMethod(_listContainer, "add_child", label);
        UndoRedoService.QueueUndoMethod(label, "set_owner", this);
        UndoRedoService.QueueUndoReference(label);
        UndoRedoService.CommitAction("Remove list item");
    }


    private void RandomizeListItemNumbers()
    {
        if (_listContainer == null)
            return;

        var items = _listContainer.GetChildren()
            .OfType<Label>()
            .ToList();

        if (items.Count == 0)
            return;

        var numbers = Enumerable.Range(1, items.Count).ToList();
        Shuffle(numbers);

        for (int i = 0; i < items.Count; i++)
        {
            UndoRedoService.QueueDoUndoProperty(
                items[i],
                "text",
                $"List Item {numbers[i]}",
                items[i].Text
            );
        }

        UndoRedoService.CommitAction("Randomize item numbers");
    }

    private void SwapFirstTwoItemNumbers()
    {
        if (_listContainer == null)
            return;

        var items = _listContainer.GetChildren().OfType<Label>().ToList();

        if (items.Count < 2) return;

        var label1 = items[0];
        var label2 = items[1];

        UndoRedoService.QueueDoUndoProperty(label1, "text", label2.Text, label1.Text);
        UndoRedoService.QueueDoUndoProperty(label2, "text", label1.Text, label2.Text);
        UndoRedoService.CommitAction("Swap first two item numbers");
    }


    private void RandomizeListItemColors()
    {
        if (_listContainer == null)
            return;

        foreach (var label in _listContainer.GetChildren().OfType<Label>())
        {
            UndoRedoService.QueueDoUndoMethod(
                label,
                "set_modulate",
                [
                    new Color(
                        GD.Randf(),
                        GD.Randf(),
                        GD.Randf()
                    )
                ],
                [
                    label.Modulate
                ]
            );
        }

        UndoRedoService.CommitAction("Randomize item colors");
    }


    private void ClearHistory()
    {
        long orphanNodesBefore = (long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        GD.Print($"New orphan nodes created since last UndoRedo history clear: {orphanNodesBefore - _lastOrphanNodeCount}");

        UndoRedoService.ClearHistory();

        long orphanNodesAfter = (long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        GD.Print($"Orphan nodes freed by UndoRedo history clear: {orphanNodesBefore - orphanNodesAfter}");
        _lastOrphanNodeCount = orphanNodesAfter;
    }


    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
