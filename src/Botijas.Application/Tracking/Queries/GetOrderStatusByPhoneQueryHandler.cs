using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.Services;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Tracking.Queries;

/// <summary>
/// Consulta pública de autoatendimento: permite ao cliente final ver o estado dos seus
/// pedidos apenas indicando o telefone, sem precisar de contactar/visitar a loja.
/// Não expõe dados de outros clientes nem identificadores internos sensíveis.
/// </summary>
public class GetOrderStatusByPhoneQueryHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public GetOrderStatusByPhoneQueryHandler(
        ICustomerRepository customerRepository,
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<TrackingResultDto>> Handle(GetOrderStatusByPhoneQuery query, CancellationToken cancellationToken)
    {
        PhoneNumber phone;
        try
        {
            phone = PhoneNumber.Create(query.Phone);
        }
        catch (ArgumentException ex)
        {
            return Result<TrackingResultDto>.Failure(ex.Message);
        }

        var customer = await _customerRepository.FindByPhoneAsync(phone, cancellationToken);
        if (customer == null)
        {
            // Resposta uniforme (sem pedidos) para telefone inexistente, evitando confirmar
            // a existência (ou não) de um número no sistema.
            return Result<TrackingResultDto>.Success(new TrackingResultDto
            {
                CustomerName = null,
                Orders = new List<TrackingOrderDto>()
            });
        }

        var orders = await _orderRepository.FindAllByCustomerAsync(customer.CustomerId, cancellationToken);
        var readyAtByOrder = await _orderRepository.GetReadyAtByOrderAsync(cancellationToken);

        var orderDtos = new List<TrackingOrderDto>();

        foreach (var order in orders.Take(20))
        {
            var cylinders = await _cylinderRepository.FindByOrderIdAsync(order.OrderId, cancellationToken);

            // Pedidos fechados: o estado atual da botija pode já refletir um ciclo novo
            // (ex.: botija reentregue e recebida noutro pedido). Resolver via histórico
            // do OrderId, igual ao dashboard.
            var states = new List<string>(cylinders.Count);
            foreach (var cylinder in cylinders)
            {
                var history = await _historyRepository.GetByCylinderIdAsync(cylinder.CylinderId, cancellationToken);
                states.Add(OrderCylinderStateResolver.Resolve(
                    order.Status,
                    cylinder.State,
                    history,
                    order.OrderId));
            }

            orderDtos.Add(new TrackingOrderDto
            {
                Status = order.Status.ToString(),
                FulfillmentMethod = order.FulfillmentMethod.ToString(),
                CreatedAt = order.CreatedAt,
                ReadyAt = readyAtByOrder.TryGetValue(order.OrderId, out var readyAt) ? readyAt : null,
                CompletedAt = order.CompletedAt,
                ShippedAt = order.ShippedAt,
                CancelledAt = order.CancelledAt,
                TotalCylinders = cylinders.Count,
                ReceivedCylinders = states.Count(s => s == nameof(CylinderState.Received)),
                ReadyCylinders = states.Count(s => s == nameof(CylinderState.Ready)),
                ProblemCylinders = states.Count(s => s == nameof(CylinderState.Problem)),
                DeliveredCylinders = states.Count(s => s == nameof(CylinderState.Delivered))
            });
        }

        var ordered = orderDtos
            .OrderBy(o => StatusPriority(o.Status))
            .ThenByDescending(o => o.CreatedAt)
            .ToList();

        return Result<TrackingResultDto>.Success(new TrackingResultDto
        {
            CustomerName = customer.Name,
            Orders = ordered
        });
    }

    // Pedidos mais relevantes para o cliente aparecem primeiro: prontos > em curso > concluídos > cancelados.
    private static int StatusPriority(string status) => status switch
    {
        nameof(RefillOrderStatus.ReadyForPickup) => 0,
        nameof(RefillOrderStatus.Open) => 1,
        nameof(RefillOrderStatus.Completed) => 2,
        nameof(RefillOrderStatus.Cancelled) => 3,
        _ => 4
    };
}

public class TrackingResultDto
{
    public string? CustomerName { get; set; }
    public List<TrackingOrderDto> Orders { get; set; } = new();
}

public class TrackingOrderDto
{
    public string Status { get; set; } = string.Empty;
    public string FulfillmentMethod { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int TotalCylinders { get; set; }
    public int ReceivedCylinders { get; set; }
    public int ReadyCylinders { get; set; }
    public int ProblemCylinders { get; set; }
    public int DeliveredCylinders { get; set; }
}
