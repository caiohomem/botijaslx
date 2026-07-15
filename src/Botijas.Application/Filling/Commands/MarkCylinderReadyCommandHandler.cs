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

        if (order.Status != RefillOrderStatus.Open)
        {
            return Result<FillingResultDto>.Failure(
                $"Pedido não está aberto para enchimento. Estado atual: {order.Status}");
        }

        var orderCylinderRef = order.Cylinders.FirstOrDefault(c => c.CylinderId == command.CylinderId);
        if (orderCylinderRef == null)
        {
            return Result<FillingResultDto>.Failure("Botija não pertence ao pedido aberto");
        }

        // Buscar todos os cilindros do pedido e sincronizar refs antes de validar o estado.
        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        order.RecalculateStatus(orderCylinders);

        var wasAlreadyReady = RefillOrder.IsCylinderReadyForPickup(cylinder.State);

        if (!wasAlreadyReady)
        {
            try
            {
                cylinder.MarkAsReady();
            }
            catch (InvalidOperationException ex)
            {
                return Result<FillingResultDto>.Failure(ex.Message);
            }

            order.RecalculateStatus(orderCylinders);

            var historyEntry = CylinderHistoryEntry.Create(
                cylinder.CylinderId,
                CylinderEventType.MarkedReady,
                "Botija marcada como cheia",
                order.OrderId);
            await _historyRepository.AddAsync(historyEntry, cancellationToken);
        }

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        // Calcular progresso
        var totalCylinders = orderCylinders.Count;
        var readyCylinders = orderCylinders.Count(c => RefillOrder.IsCylinderReadyForPickup(c.State));

        return Result<FillingResultDto>.Success(new FillingResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            TotalCylindersInOrder = totalCylinders,
            ReadyCylindersInOrder = readyCylinders,
            IsOrderComplete = order.Status == RefillOrderStatus.ReadyForPickup,
            WasAlreadyReady = wasAlreadyReady
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
    public bool WasAlreadyReady { get; set; }
}
