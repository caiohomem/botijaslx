namespace Botijas.Application.Filling.Commands;

public record ReportCylinderProblemCommand(
    Guid CylinderId,
    string ProblemType,
    string Notes
);
