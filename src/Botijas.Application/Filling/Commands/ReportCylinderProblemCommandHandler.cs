using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Commands;

public class ReportCylinderProblemCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public ReportCylinderProblemCommandHandler(
        ICylinderRepository cylinderRepository,
        IRefillOrderRepository orderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
        _orderRepository = orderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<ReportProblemResultDto>> Handle(
        ReportCylinderProblemCommand command,
        CancellationToken cancellationToken)
    {
        var cylinder = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<ReportProblemResultDto>.Failure("Botija não encontrada");
        }

        var fullNotes = $"[{command.ProblemType}] {command.Notes}";

        try
        {
            cylinder.ReportProblem(fullNotes);
        }
        catch (ArgumentException ex)
        {
            return Result<ReportProblemResultDto>.Failure(ex.Message);
        }

        // Registrar histórico
        var historyEntry = CylinderHistoryEntry.Create(
            cylinder.CylinderId,
            CylinderEventType.ProblemReported,
            fullNotes);
        await _historyRepository.AddAsync(historyEntry, cancellationToken);

        var order = await _orderRepository.FindByCylinderIdAsync(command.CylinderId, cancellationToken);
        if (order != null)
        {
            var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
            order.RecalculateStatus(orderCylinders);
        }

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        return Result<ReportProblemResultDto>.Success(new ReportProblemResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            ProblemType = command.ProblemType,
            Notes = command.Notes
        });
    }
}

public class ReportProblemResultDto
{
    public Guid CylinderId { get; set; }
    public string State { get; set; } = string.Empty;
    public string ProblemType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
