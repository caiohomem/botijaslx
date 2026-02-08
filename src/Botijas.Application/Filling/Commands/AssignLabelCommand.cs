namespace Botijas.Application.Filling.Commands;

public record AssignLabelCommand(Guid CylinderId, string QrToken);
