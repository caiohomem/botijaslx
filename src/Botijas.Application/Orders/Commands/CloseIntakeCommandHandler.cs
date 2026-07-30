using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Orders.Commands;

/// <summary>
/// Entrada de botijas numa só transação. Antes o frontend encadeava vários pedidos
/// HTTP (criar pedido, juntar cada botija existente, criar as novas, etiquetar), o que
/// deixava estado parcial quando um passo falhava e levava a dividir uma visita em
/// dois pedidos — o pedido pequeno ficava pronto sozinho e disparava o WhatsApp cedo.
/// </summary>
public class CloseIntakeCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;
    private readonly ICustomerRepository _customerRepository;

    public CloseIntakeCommandHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<CloseIntakeResultDto>> Handle(
        CloseIntakeCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FulfillmentMethod>(command.FulfillmentMethod, true, out var fulfillmentMethod))
        {
            return Result<CloseIntakeResultDto>.Failure("Fulfillment method inválido");
        }

        var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        if (customer == null)
        {
            return Result<CloseIntakeResultDto>.Failure("Cliente não encontrado");
        }

        var existingIds = (command.ExistingCylinderIds ?? []).Distinct().ToList();
        var newCount = Math.Max(0, command.NewCylinderCount);

        if (existingIds.Count == 0 && newCount == 0)
        {
            return Result<CloseIntakeResultDto>.Failure("Adicione pelo menos uma botija ao pedido");
        }

        if (newCount > 100)
        {
            return Result<CloseIntakeResultDto>.Failure("Quantidade de botijas novas demasiado alta");
        }

        // Uma visita = um pedido. Reaproveita o pedido aberto do cliente com o mesmo
        // modo de entrega para que botijas dadas em passos separados fiquem juntas.
        var openOrders = await _orderRepository.FindOpenOrdersByCustomerAsync(command.CustomerId, cancellationToken);
        var order = openOrders
            .Where(o => o.FulfillmentMethod == fulfillmentMethod)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        var reusedExistingOrder = order != null;

        if (order == null)
        {
            order = RefillOrder.Create(
                command.CustomerId,
                fulfillmentMethod,
                command.RefillPaid,
                command.ShippingPaid);
            await _orderRepository.AddAsync(order, cancellationToken);
        }
        else
        {
            order.UpdateFulfillmentDetails(
                fulfillmentMethod,
                order.RefillPaid || command.RefillPaid,
                order.ShippingPaid || command.ShippingPaid);
        }

        var addedCylinders = new List<Cylinder>();

        foreach (var cylinderId in existingIds)
        {
            var cylinder = await _cylinderRepository.FindByIdAsync(cylinderId, cancellationToken);
            if (cylinder == null)
            {
                return Result<CloseIntakeResultDto>.Failure($"Botija {cylinderId} não encontrada");
            }

            if (order.Cylinders.Any(c => c.CylinderId == cylinderId))
            {
                return Result<CloseIntakeResultDto>.Failure(
                    $"Botija #{cylinder.SequentialNumber} já está neste pedido");
            }

            var openOrderId = await _cylinderRepository.FindOpenOrderIdAsync(cylinderId, cancellationToken);
            if (openOrderId.HasValue && openOrderId.Value != order.OrderId)
            {
                return Result<CloseIntakeResultDto>.Failure(
                    $"Botija #{cylinder.SequentialNumber} já está noutro pedido aberto");
            }

            try
            {
                cylinder.ReceiveForRefill();
            }
            catch (InvalidOperationException ex)
            {
                return Result<CloseIntakeResultDto>.Failure(
                    $"Botija #{cylinder.SequentialNumber}: {ex.Message}");
            }

            order.AddCylinder(cylinder);
            await _historyRepository.AddAsync(
                CylinderHistoryEntry.Create(
                    cylinder.CylinderId,
                    CylinderEventType.Received,
                    "Botija recebida para enchimento",
                    order.OrderId),
                cancellationToken);

            addedCylinders.Add(cylinder);
        }

        for (var i = 0; i < newCount; i++)
        {
            var cylinder = Cylinder.Create(0);
            await _cylinderRepository.AddAsync(cylinder, cancellationToken);
            order.AddCylinder(cylinder);

            await _historyRepository.AddAsync(
                CylinderHistoryEntry.Create(
                    cylinder.CylinderId,
                    CylinderEventType.Received,
                    "Botija recebida para enchimento",
                    order.OrderId),
                cancellationToken);

            var labelToken = LabelToken.Create($"{order.OrderId}-{cylinder.SequentialNumber}");
            var labelOwner = await _cylinderRepository.FindByLabelTokenAsync(labelToken, cancellationToken);
            if (labelOwner != null && labelOwner.CylinderId != cylinder.CylinderId)
            {
                return Result<CloseIntakeResultDto>.Failure(
                    $"Etiqueta {labelToken.Value} já está em uso por outra botija");
            }

            cylinder.AssignLabel(labelToken);
            await _historyRepository.AddAsync(
                CylinderHistoryEntry.Create(
                    cylinder.CylinderId,
                    CylinderEventType.LabelAssigned,
                    $"Etiqueta atribuída: {labelToken.Value}",
                    order.OrderId),
                cancellationToken);

            addedCylinders.Add(cylinder);
        }

        // Os repositórios partilham o mesmo DbContext, por isso um SaveChanges grava
        // pedido, botijas e histórico de uma só vez.
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var orderCylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

        return Result<CloseIntakeResultDto>.Success(new CloseIntakeResultDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            FulfillmentMethod = order.FulfillmentMethod.ToString(),
            RefillPaid = order.RefillPaid,
            ShippingPaid = order.ShippingPaid,
            CreatedAt = order.CreatedAt,
            ReusedExistingOrder = reusedExistingOrder,
            CylinderCount = orderCylinders.Count,
            AddedCylinders = addedCylinders
                .Select(c => new CylinderDto
                {
                    CylinderId = c.CylinderId,
                    SequentialNumber = c.SequentialNumber,
                    LabelToken = c.LabelToken?.Value,
                    State = c.State.ToString()
                })
                .ToList(),
            Cylinders = orderCylinders
                .OrderBy(c => c.SequentialNumber)
                .Select(c => new CylinderDto
                {
                    CylinderId = c.CylinderId,
                    SequentialNumber = c.SequentialNumber,
                    LabelToken = c.LabelToken?.Value,
                    State = c.State.ToString()
                })
                .ToList()
        });
    }
}

public class CloseIntakeResultDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FulfillmentMethod { get; set; } = string.Empty;
    public bool RefillPaid { get; set; }
    public bool ShippingPaid { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>True quando as botijas entraram num pedido aberto que já existia.</summary>
    public bool ReusedExistingOrder { get; set; }
    public int CylinderCount { get; set; }
    /// <summary>Botijas adicionadas nesta entrada (para impressão de etiquetas).</summary>
    public List<CylinderDto> AddedCylinders { get; set; } = new();
    /// <summary>Todas as botijas do pedido depois desta entrada.</summary>
    public List<CylinderDto> Cylinders { get; set; } = new();
}
