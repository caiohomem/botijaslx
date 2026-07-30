using Botijas.Domain.Entities;

namespace Botijas.Domain.Services;

/// <summary>
/// Resolve quais eventos de histórico foram anulados via ActionUndone.
/// O marcador é gravado em Details: <c>||UNDO:{guid}||</c>.
/// </summary>
public static class CylinderHistoryUndo
{
    public const string UndoMarker = "||UNDO:";

    public static HashSet<Guid> ExtractUndoneIds(IEnumerable<CylinderHistoryEntry> history)
    {
        return ExtractUndoneIds(history.Select(h => (h.EventType, h.Details)));
    }

    public static HashSet<Guid> ExtractUndoneIds(
        IEnumerable<(CylinderEventType EventType, string? Details)> entries)
    {
        var undoneIds = new HashSet<Guid>();

        foreach (var entry in entries.Where(h => h.EventType == CylinderEventType.ActionUndone))
        {
            var details = entry.Details ?? string.Empty;
            var start = details.IndexOf(UndoMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += UndoMarker.Length;
            var end = details.IndexOf("||", start, StringComparison.Ordinal);
            if (end < 0)
            {
                continue;
            }

            var value = details[start..end];
            if (Guid.TryParse(value, out var undoneId))
            {
                undoneIds.Add(undoneId);
            }
        }

        return undoneIds;
    }

    /// <summary>
    /// Eventos de negócio que ainda contam (exclui ActionUndone e eventos anulados).
    /// </summary>
    public static IEnumerable<CylinderHistoryEntry> ActiveEvents(
        IEnumerable<CylinderHistoryEntry> history)
    {
        var list = history.ToList();
        var undoneIds = ExtractUndoneIds(list);

        return list.Where(h =>
            h.EventType != CylinderEventType.ActionUndone &&
            !undoneIds.Contains(h.Id));
    }
}
