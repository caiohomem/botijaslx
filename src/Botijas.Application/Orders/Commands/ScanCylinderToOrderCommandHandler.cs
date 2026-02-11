using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Orders.Commands;

public class ScanCylinderToOrderCommandHandler
{
    private readonly IRefillOrderRepository _orderRepository;
    private readonly ICylinderRepository _cylinderRepository;

    public ScanCylinderToOrderCommandHandler(
        IRefillOrderRepository orderRepository,
        ICylinderRepository cylinderRepository)
    {
        _orderRepository = orderRepository;
        _cylinderRepository = cylinderRepository;
    }

    public async Task<Result<CylinderDto>> Handle(ScanCylinderToOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return Result<CylinderDto>.Failure("Order not found");
        }

        // Try sequential number first (e.g. "1", "0001", "#0001")
        var cleanInput = command.QrToken.TrimStart('#', '0');
        Cylinder? cylinder = null;

        if (long.TryParse(cleanInput, out var seqNum) && seqNum > 0)
        {
            cylinder = await _cylinderRepository.FindBySequentialNumberAsync(seqNum, cancellationToken);
        }

        // Fallback: search by label token
        if (cylinder == null)
        {
            var labelToken = LabelToken.Create(command.QrToken);
            cylinder = await _cylinderRepository.FindByLabelTokenAsync(labelToken, cancellationToken);
        }

        if (cylinder == null)
        {
            return Result<CylinderDto>.Failure("Botija não encontrada");
        }

        // Verificar se não está em outro pedido aberto
        var inOtherOrder = await _cylinderRepository.FindInOpenOrderAsync(cylinder.CylinderId, cancellationToken);
        if (inOtherOrder != null)
        {
            return Result<CylinderDto>.Failure("Cylinder is already in another open order");
        }

        order.AddCylinder(cylinder);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result<CylinderDto>.Success(new CylinderDto
        {
            CylinderId = cylinder.CylinderId,
            SequentialNumber = cylinder.SequentialNumber,
            LabelToken = cylinder.LabelToken?.Value,
            State = cylinder.State.ToString()
        });
    }
}
