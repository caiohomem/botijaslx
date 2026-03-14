using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Cylinders.Commands;

public class UndoCylinderHistoryActionCommandHandler
{
    private const string UndoMarker = "||UNDO:";
    private static readonly CylinderEventType[] UndoableEventTypes =
    [
        CylinderEventType.MarkedReady,
        CylinderEventType.Delivered,
        CylinderEventType.ProblemReported
    ];

    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;
    private readonly IRefillOrderRepository _orderRepository;

    public UndoCylinderHistoryActionCommandHandler(
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository,
        IRefillOrderRepository orderRepository)
    {
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
        _orderRepository = orderRepository;
    }

    public async Task<Result<UndoCylinderHistoryActionResultDto>> Handle(
        UndoCylinderHistoryActionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Comment))
        {
            return Result<UndoCylinderHistoryActionResultDto>.Failure("Observação é obrigatória");
        }

        var cylinder = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<UndoCylinderHistoryActionResultDto>.Failure("Botija não encontrada");
        }

        var historyEntry = await _historyRepository.FindByIdAsync(command.HistoryEntryId, cancellationToken);
        if (historyEntry == null || historyEntry.CylinderId != command.CylinderId)
        {
            return Result<UndoCylinderHistoryActionResultDto>.Failure("Ação de histórico não encontrada");
        }

        if (!UndoableEventTypes.Contains(historyEntry.EventType))
        {
            return Result<UndoCylinderHistoryActionResultDto>.Failure("Esta ação não pode ser desfeita");
        }

        var fullHistory = await _historyRepository.GetByCylinderIdAsync(command.CylinderId, cancellationToken);
        var undoneIds = ExtractUndoneIds(fullHistory);
        var activeHistory = fullHistory
            .Where(h => h.EventType != CylinderEventType.ActionUndone && !undoneIds.Contains(h.Id))
            .ToList();

        var latestUndoableActiveHistory = activeHistory.FirstOrDefault(h => UndoableEventTypes.Contains(h.EventType));
        if (latestUndoableActiveHistory?.Id != command.HistoryEntryId)
        {
            return Result<UndoCylinderHistoryActionResultDto>.Failure("Só é possível desfazer a ação ativa mais recente da botija");
        }

        var order = await _orderRepository.FindByCylinderIdAsync(command.CylinderId, cancellationToken);
        var previousActiveHistory = activeHistory
            .SkipWhile(h => h.Id != command.HistoryEntryId)
            .Skip(1)
            .FirstOrDefault();

        RestoreCylinderToHistoryState(cylinder, previousActiveHistory);

        if (order != null)
        {
            var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
            order.RecalculateStatus(orderCylinders);
        }

        var restoredEventType = previousActiveHistory?.EventType.ToString() ?? CylinderEventType.Received.ToString();
        var historyDetails =
            $"Ação desfeita: {historyEntry.EventType}. " +
            $"Estado restaurado para: {restoredEventType}. " +
            $"Observação: {command.Comment.Trim()} {UndoMarker}{historyEntry.Id}||";
        var undoEntry = CylinderHistoryEntry.Create(
            cylinder.CylinderId,
            CylinderEventType.ActionUndone,
            historyDetails,
            order?.OrderId ?? historyEntry.OrderId);

        await _historyRepository.AddAsync(undoEntry, cancellationToken);
        await _cylinderRepository.SaveChangesAsync(cancellationToken);

        if (order != null)
        {
            await _orderRepository.SaveChangesAsync(cancellationToken);
        }

        await _historyRepository.SaveChangesAsync(cancellationToken);

        return Result<UndoCylinderHistoryActionResultDto>.Success(new UndoCylinderHistoryActionResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order?.OrderId,
            OrderStatus = order?.Status.ToString(),
            UndoneEventType = historyEntry.EventType.ToString(),
            RestoredEventType = restoredEventType
        });
    }

    private static void RestoreCylinderToHistoryState(Cylinder cylinder, CylinderHistoryEntry? previousActiveHistory)
    {
        switch (previousActiveHistory?.EventType)
        {
            case CylinderEventType.Delivered:
                cylinder.MarkAsReady();
                cylinder.MarkAsDelivered();
                break;
            case CylinderEventType.MarkedReady:
                cylinder.RevertToReady();
                break;
            case CylinderEventType.ProblemReported:
                cylinder.RevertToProblem(previousActiveHistory.Details);
                break;
            case CylinderEventType.LabelAssigned:
            case CylinderEventType.Received:
            default:
                cylinder.RevertToReceived();
                break;
        }
    }

    private static HashSet<Guid> ExtractUndoneIds(IEnumerable<CylinderHistoryEntry> history)
    {
        var undoneIds = new HashSet<Guid>();

        foreach (var entry in history.Where(h => h.EventType == CylinderEventType.ActionUndone))
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
}

public class UndoCylinderHistoryActionResultDto
{
    public Guid CylinderId { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public string UndoneEventType { get; set; } = string.Empty;
    public string RestoredEventType { get; set; } = string.Empty;
}
