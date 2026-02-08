using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.PrintJobs.Commands;

public class AckPrintJobPrintedCommandHandler
{
    private readonly IPrintJobRepository _printJobRepository;

    public AckPrintJobPrintedCommandHandler(IPrintJobRepository printJobRepository)
    {
        _printJobRepository = printJobRepository;
    }

    public async Task<Result<PrintJobDto>> Handle(AckPrintJobPrintedCommand command, CancellationToken cancellationToken)
    {
        var printJob = await _printJobRepository.FindByIdAsync(command.PrintJobId, cancellationToken);
        if (printJob == null)
        {
            return Result<PrintJobDto>.Failure("Print job not found");
        }

        try
        {
            printJob.MarkAsPrinted();
            await _printJobRepository.SaveChangesAsync(cancellationToken);

            return Result<PrintJobDto>.Success(new PrintJobDto
            {
                PrintJobId = printJob.PrintJobId,
                StoreId = printJob.StoreId,
                Quantity = printJob.Quantity,
                TemplateId = printJob.TemplateId,
                Status = printJob.Status.ToString(),
                CreatedAt = printJob.CreatedAt,
                CompletedAt = printJob.CompletedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Result<PrintJobDto>.Failure(ex.Message);
        }
    }
}
