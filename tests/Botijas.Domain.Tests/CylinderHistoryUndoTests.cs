using Botijas.Domain.Entities;
using Botijas.Domain.Services;
using Xunit;

namespace Botijas.Domain.Tests;

public class CylinderHistoryUndoTests
{
    [Fact]
    public void ExtractUndoneIds_ReadsMarkerFromDetails()
    {
        var markedReadyId = Guid.NewGuid();
        var undo = CylinderHistoryEntry.Create(
            Guid.NewGuid(),
            CylinderEventType.ActionUndone,
            details: $"Ação desfeita: MarkedReady. Observação: erro {CylinderHistoryUndo.UndoMarker}{markedReadyId}||");

        var undone = CylinderHistoryUndo.ExtractUndoneIds([undo]);

        Assert.Contains(markedReadyId, undone);
    }

    [Fact]
    public void ActiveEvents_ExcludesUndoneMarkedReadyAndUndoEntry()
    {
        var cylinderId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var received = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.Received, orderId: orderId);
        var markedReady = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady, orderId: orderId);
        var undo = CylinderHistoryEntry.Create(
            cylinderId,
            CylinderEventType.ActionUndone,
            details: $"{CylinderHistoryUndo.UndoMarker}{markedReady.Id}||",
            orderId: orderId);
        var markedAgain = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady, orderId: orderId);

        var active = CylinderHistoryUndo.ActiveEvents([received, markedReady, undo, markedAgain]).ToList();

        Assert.Equal(2, active.Count);
        Assert.Contains(received, active);
        Assert.Contains(markedAgain, active);
        Assert.DoesNotContain(markedReady, active);
        Assert.DoesNotContain(undo, active);
    }

    [Fact]
    public void ActiveEvents_CountsOnlyOneFillAfterUndoAndRedo()
    {
        var cylinderId = Guid.NewGuid();
        var first = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady);
        var undo = CylinderHistoryEntry.Create(
            cylinderId,
            CylinderEventType.ActionUndone,
            details: $"{CylinderHistoryUndo.UndoMarker}{first.Id}||");
        var second = CylinderHistoryEntry.Create(cylinderId, CylinderEventType.MarkedReady);

        var fills = CylinderHistoryUndo.ActiveEvents([first, undo, second])
            .Count(h => h.EventType == CylinderEventType.MarkedReady);

        Assert.Equal(1, fills);
    }
}
