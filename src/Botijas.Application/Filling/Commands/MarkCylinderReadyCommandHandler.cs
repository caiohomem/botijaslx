using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Commands;

public class MarkCylinderReadyCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public MarkCylinderReadyCommandHandler(
        ICylinderRepository cylinderRepository,
        IRefillOrderRepository orderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
        _orderRepository = orderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<FillingResultDto>> Handle(MarkCylinderReadyCommand command, CancellationToken cancellationToken)
    {
        // Buscar cilindro
        var cylinder = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<FillingResultDto>.Failure("Botija não encontrada");
        }

        // Buscar pedido que contém esta botija
        var order = await _orderRepository.FindByCylinderIdAsync(command.CylinderId, cancellationToken);
        if (order == null)
        {
            return Result<FillingResultDto>.Failure("Pedido não encontrado para esta botija");
        }

        // Marcar como pronta
        try
        {
            cylinder.MarkAsReady();
        }
        catch (InvalidOperationException ex)
        {
            return Result<FillingResultDto>.Failure(ex.Message);
        }

        // Buscar todos os cilindros do pedido para verificar se está completo
        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        
        // Atualizar status do pedido
        order.CheckAndUpdateStatus(orderCylinders);

        // Registrar histórico
        var historyEntry = CylinderHistoryEntry.Create(
            cylinder.CylinderId,
            CylinderEventType.MarkedReady,
            "Botija marcada como cheia",
            order.OrderId);
        await _historyRepository.AddAsync(historyEntry, cancellationToken);

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        // Calcular progresso
        var totalCylinders = orderCylinders.Count;
        var readyCylinders = orderCylinders.Count(c => c.State == CylinderState.Ready);

        return Result<FillingResultDto>.Success(new FillingResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            TotalCylindersInOrder = totalCylinders,
            ReadyCylindersInOrder = readyCylinders,
            IsOrderComplete = order.Status == RefillOrderStatus.ReadyForPickup
        });
    }
}

public class FillingResultDto
{
    public Guid CylinderId { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public int TotalCylindersInOrder { get; set; }
    public int ReadyCylindersInOrder { get; set; }
    public bool IsOrderComplete { get; set; }
}
