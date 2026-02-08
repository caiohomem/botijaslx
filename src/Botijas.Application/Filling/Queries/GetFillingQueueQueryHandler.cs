using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Queries;

public class GetFillingQueueQueryHandler
{
    private readonly ICylinderRepository _cylinderRepository;

    public GetFillingQueueQueryHandler(ICylinderRepository cylinderRepository)
    {
        _cylinderRepository = cylinderRepository;
    }

    public async Task<Result<List<FillingQueueItemDto>>> Handle(GetFillingQueueQuery query, CancellationToken cancellationToken)
    {
        var items = await _cylinderRepository.GetFillingQueueAsync(cancellationToken);
        
        var dtos = items.Select(item => new FillingQueueItemDto
        {
            CylinderId = item.CylinderId,
            LabelToken = item.LabelToken,
            State = item.State,
            ReceivedAt = item.ReceivedAt,
            OrderId = item.OrderId,
            CustomerName = item.CustomerName,
            CustomerPhone = item.CustomerPhone,
            TotalCylindersInOrder = item.TotalCylindersInOrder,
            ReadyCylindersInOrder = item.ReadyCylindersInOrder
        }).ToList();
        
        return Result<List<FillingQueueItemDto>>.Success(dtos);
    }
}
