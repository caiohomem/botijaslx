namespace Botijas.Application.Cylinders.Commands;

public record UndoCylinderHistoryActionCommand(Guid CylinderId, Guid HistoryEntryId, string Comment);
