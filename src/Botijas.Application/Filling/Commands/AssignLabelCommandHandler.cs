using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Filling.Commands;

public class AssignLabelCommandHandler
{
    private readonly ICylinderRepository _cylinderRepository;
    private readonly ICylinderHistoryRepository _historyRepository;

    public AssignLabelCommandHandler(
        ICylinderRepository cylinderRepository,
        ICylinderHistoryRepository historyRepository)
    {
        _cylinderRepository = cylinderRepository;
        _historyRepository = historyRepository;
    }

    public async Task<Result<AssignLabelResultDto>> Handle(
        AssignLabelCommand command,
        CancellationToken cancellationToken)
    {
        // Buscar botija
        var cylinder = await _cylinderRepository.FindByIdAsync(command.CylinderId, cancellationToken);
        if (cylinder == null)
        {
            return Result<AssignLabelResultDto>.Failure("Botija não encontrada");
        }

        // Verificar se a etiqueta já está em uso
        var labelToken = LabelToken.Create(command.QrToken);
        var existingCylinder = await _cylinderRepository.FindByLabelTokenAsync(labelToken, cancellationToken);
        if (existingCylinder != null && existingCylinder.CylinderId != command.CylinderId)
        {
            return Result<AssignLabelResultDto>.Failure("Esta etiqueta já está em uso por outra botija");
        }

        // Guardar etiqueta anterior para histórico
        var previousLabel = cylinder.LabelToken?.Value;

        // Atribuir nova etiqueta
        cylinder.AssignLabel(labelToken);

        // Registrar histórico
        var details = previousLabel != null 
            ? $"Etiqueta alterada: {previousLabel} → {command.QrToken}"
            : $"Etiqueta atribuída: {command.QrToken}";
        var historyEntry = CylinderHistoryEntry.Create(
            cylinder.CylinderId,
            CylinderEventType.LabelAssigned,
            details);
        await _historyRepository.AddAsync(historyEntry, cancellationToken);

        await _cylinderRepository.SaveChangesAsync(cancellationToken);
        await _historyRepository.SaveChangesAsync(cancellationToken);

        return Result<AssignLabelResultDto>.Success(new AssignLabelResultDto
        {
            CylinderId = cylinder.CylinderId,
            LabelToken = cylinder.LabelToken!.Value,
            PreviousLabelToken = previousLabel
        });
    }
}

public class AssignLabelResultDto
{
    public Guid CylinderId { get; set; }
    public string LabelToken { get; set; } = string.Empty;
    public string? PreviousLabelToken { get; set; }
}
