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
        var probe = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (probe == null)
        {
            return Result<FillingResultDto>.Failure("Botija não encontrada");
        }

        var probeOrder = await _orderRepository.FindByCylinderIdAsync(command.CylinderId, cancellationToken);
        if (probeOrder == null)
        {
            return Result<FillingResultDto>.Failure("Pedido não encontrado para esta botija");
        }

        Result<FillingResultDto>? result = null;
        await _orderRepository.ExecuteLockedAsync(probeOrder.OrderId, async ct =>
        {
            result = await MarkLockedAsync(command.CylinderId, ct);
        }, cancellationToken);

        return result ?? Result<FillingResultDto>.Failure("Pedido não encontrado para esta botija");
    }

    private async Task<Result<FillingResultDto>> MarkLockedAsync(Guid cylinderId, CancellationToken cancellationToken)
    {
        var cylinder = await _cylinderRepository.FindByIdAsync(cylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<FillingResultDto>.Failure("Botija não encontrada");
        }

        var order = await _orderRepository.FindByCylinderIdAsync(cylinderId, cancellationToken);
        if (order == null)
        {
            return Result<FillingResultDto>.Failure("Pedido não encontrado para esta botija");
        }

        if (order.Status != RefillOrderStatus.Open)
        {
            return Result<FillingResultDto>.Failure(
                $"Pedido não está aberto para enchimento. Estado atual: {order.Status}");
        }

        if (order.Cylinders.All(c => c.CylinderId != cylinderId))
        {
            return Result<FillingResultDto>.Failure("Botija não pertence ao pedido aberto");
        }

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

            await _historyRepository.AddAsync(
                CylinderHistoryEntry.Create(
                    cylinder.CylinderId,
                    CylinderEventType.MarkedReady,
                    "Botija marcada como cheia",
                    order.OrderId),
                cancellationToken);
        }

        // Recalcular só depois da mudança, já com o lock: a última botija do lote
        // vê as irmãs que os pedidos anteriores acabaram de gravar.
        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);
        order.RecalculateStatus(orderCylinders);

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        var totalCylinders = orderCylinders.Count;
        var readyCylinders = orderCylinders.Count(c => RefillOrder.IsCylinderReadyForPickup(c.State));

        var customerPendingCylinders = await _cylinderRepository.CountPendingByCustomerAsync(
            order.CustomerId,
            cancellationToken);

        var notifyCylinderCount = totalCylinders;
        if (order.Status == RefillOrderStatus.ReadyForPickup && customerPendingCylinders == 0)
        {
            notifyCylinderCount = await _cylinderRepository.CountReadyForPickupByCustomerAsync(
                order.CustomerId,
                cancellationToken);
            if (notifyCylinderCount <= 0)
            {
                notifyCylinderCount = totalCylinders;
            }
        }

        return Result<FillingResultDto>.Success(new FillingResultDto
        {
            CylinderId = cylinder.CylinderId,
            State = cylinder.State.ToString(),
            OrderId = order.OrderId,
            OrderStatus = order.Status.ToString(),
            TotalCylindersInOrder = totalCylinders,
            ReadyCylindersInOrder = readyCylinders,
            IsOrderComplete = order.Status == RefillOrderStatus.ReadyForPickup,
            WasAlreadyReady = wasAlreadyReady,
            CustomerPendingCylinders = customerPendingCylinders,
            NotifyCylinderCount = notifyCylinderCount
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
    /// <summary>Botijas do mesmo cliente ainda por encher noutros pedidos abertos.</summary>
    public int CustomerPendingCylinders { get; set; }
    /// <summary>Contagem a usar na mensagem WhatsApp (agrega pedidos ReadyForPickup do cliente).</summary>
    public int NotifyCylinderCount { get; set; }
}
