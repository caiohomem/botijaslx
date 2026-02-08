using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.DomainEvents;
using Botijas.Domain.Repositories;

namespace Botijas.Application.PrintJobs.Commands;

public class CreatePrintJobCommandHandler
{
    private readonly IPrintJobRepository _printJobRepository;
    private readonly IPrintJobDispatcher _dispatcher;

    public CreatePrintJobCommandHandler(
        IPrintJobRepository printJobRepository,
        IPrintJobDispatcher dispatcher)
    {
        _printJobRepository = printJobRepository;
        _dispatcher = dispatcher;
    }

    public async Task<Result<PrintJobDto>> Handle(CreatePrintJobCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var printJob = PrintJob.Create(command.StoreId, command.Quantity, command.TemplateId);
            await _printJobRepository.AddAsync(printJob, cancellationToken);
            await _printJobRepository.SaveChangesAsync(cancellationToken);

            // Marcar como dispatched e enviar para gateway
            printJob.MarkAsDispatched();
            await _printJobRepository.SaveChangesAsync(cancellationToken);
            await _dispatcher.DispatchAsync(
                printJob.PrintJobId, 
                printJob.Quantity, 
                printJob.TemplateId,
                command.CustomerName,
                command.CustomerPhone,
                cancellationToken);

            printJob.ClearDomainEvents();

            return Result<PrintJobDto>.Success(new PrintJobDto
            {
                PrintJobId = printJob.PrintJobId,
                StoreId = printJob.StoreId,
                Quantity = printJob.Quantity,
                TemplateId = printJob.TemplateId,
                Status = printJob.Status.ToString(),
                CreatedAt = printJob.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return Result<PrintJobDto>.Failure(ex.Message);
        }
    }
}
