using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Filling.Commands;

public class ReportCylinderProblemCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public ReportCylinderProblemCommandHandler(
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
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

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
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
