using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Pickup.Commands;

public class MarkOrderNotifiedCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;

    public MarkOrderNotifiedCommandHandler(IRefillOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<MarkNotifiedResultDto>> Handle(
        MarkOrderNotifiedCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<MarkNotifiedResultDto>.Failure("Pedido não encontrado");
        }

        try
        {
            order.MarkAsNotified();
        }
        catch (InvalidOperationException ex)
        {
            return Result<MarkNotifiedResultDto>.Failure(ex.Message);
        }

        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result<MarkNotifiedResultDto>.Success(new MarkNotifiedResultDto
        {
            OrderId = order.OrderId,
            NotifiedAt = order.NotifiedAt!.Value
        });
    }
}

public class MarkNotifiedResultDto
{
    public Guid OrderId { get; set; }
    public DateTime NotifiedAt { get; set; }
}
