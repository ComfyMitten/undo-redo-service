using Godot;
using System.Collections.Generic;
using System.Linq;

namespace UndoRedoDemo;

using UndoRedoService = UndoRedoService.UndoRedoService;

[Tool]
public partial class UndoRedoDemo : Node
{
    [Export]
    public VBoxContainer ListContainer { get; set; }

    [ExportToolButton("Add list item")]
    public Callable AddListItemButton => Callable.From(AddListItem);

    [ExportToolButton("Remove list item")]
    public Callable RemoveListItemButton => Callable.From(RemoveListItem);

    [ExportToolButton("Randomize item numbers")]
    public Callable RandomizeNumbersButton => Callable.From(RandomizeListItemNumbers);

    [ExportToolButton("Randomize item colors")]
    public Callable RandomizeColorsButton => Callable.From(RandomizeListItemColors);

    [ExportToolButton("Clear UndoRedo history")]
    public Callable ClearHistoryButton => Callable.From(ClearHistory);

    private long _lastOrphanNodeCount;

    public override void _Ready()
    {
        _lastOrphanNodeCount = (long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
    }

    private void AddListItem()
    {
        if (ListContainer == null)
            return;

        var label = new Label
        {
            Text = $"List Item {ListContainer.GetChildCount() + 1}"
        };

        UndoRedoService.QueueDoMethod(ListContainer, "add_child", label);
        UndoRedoService.QueueDoMethod(label, "set_owner", this);
        UndoRedoService.QueueDoReference(label);
        UndoRedoService.QueueUndoMethod(ListContainer, "remove_child", label);
        UndoRedoService.CommitAction("Add list item");
    }


    private void RemoveListItem()
    {
        if (ListContainer == null)
            return;

        var items = ListContainer.GetChildren()
            .OfType<Label>()
            .ToList();

        if (items.Count == 0)
            return;

        var label = items[^1];

        UndoRedoService.QueueDoMethod(ListContainer, "remove_child", label);
        UndoRedoService.QueueUndoMethod(ListContainer, "add_child", label);
        UndoRedoService.QueueUndoMethod(label, "set_owner", this);
        UndoRedoService.QueueUndoReference(label);
        UndoRedoService.CommitAction("Remove list item");
    }


    private void RandomizeListItemNumbers()
    {
        if (ListContainer == null)
            return;

        var items = ListContainer.GetChildren()
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


    private void RandomizeListItemColors()
    {
        if (ListContainer == null)
            return;

        foreach (var label in ListContainer.GetChildren().OfType<Label>())
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
