using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Cylinders.Queries;

public record GetProblemCylindersQuery();

public class ProblemCylinderDto
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
    public string? OccurrenceNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerPhoneType { get; set; }
}

public class GetProblemCylindersQueryHandler
{
    private readonly ICylinderRepository _cylinderRepository;

    public GetProblemCylindersQueryHandler(ICylinderRepository cylinderRepository)
    {
        _cylinderRepository = cylinderRepository;
    }

    public async Task<Result<List<ProblemCylinderDto>>> Handle(
        GetProblemCylindersQuery query,
        CancellationToken cancellationToken)
    {
        var items = await _cylinderRepository.GetProblemCylindersAsync(cancellationToken);

        var dtos = items
            .Select(item => new ProblemCylinderDto
            {
                CylinderId = item.CylinderId,
                SequentialNumber = item.SequentialNumber,
                LabelToken = item.LabelToken,
                State = item.State,
                OccurrenceNotes = item.OccurrenceNotes,
                CreatedAt = item.CreatedAt,
                OrderId = item.OrderId,
                OrderStatus = item.OrderStatus,
                CustomerId = item.CustomerId,
                CustomerName = item.CustomerName,
                CustomerPhone = item.CustomerPhone,
                CustomerPhoneType = item.CustomerPhoneType
            })
            .ToList();

        return Result<List<ProblemCylinderDto>>.Success(dtos);
    }
}
