namespace Botijas.Application.PrintJobs.Commands;

public record AckPrintJobFailedCommand(Guid PrintJobId, string Error);
