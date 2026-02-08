using Botijas.Application.PrintJobs;
using Botijas.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Botijas.Infrastructure.PrintJobs;

/// <summary>
/// Simulated print dispatcher that auto-acknowledges print jobs as printed.
/// Replaces SignalR dispatcher when no physical print gateway is connected.
/// </summary>
public class SimulatedPrintJobDispatcher : IPrintJobDispatcher
{
    private readonly IPrintJobRepository _printJobRepository;
    private readonly ILogger<SimulatedPrintJobDispatcher> _logger;

    public SimulatedPrintJobDispatcher(
        IPrintJobRepository printJobRepository,
        ILogger<SimulatedPrintJobDispatcher> logger)
    {
        _printJobRepository = printJobRepository;
        _logger = logger;
    }

    public async Task DispatchAsync(
        Guid printJobId,
        int quantity,
        string? templateId,
        string? customerName,
        string? customerPhone,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[SIMULATED PRINT] Job {PrintJobId}: {Quantity} label(s) for {CustomerName} ({CustomerPhone})",
            printJobId, quantity, customerName ?? "N/A", customerPhone ?? "N/A");

        // Auto-acknowledge as printed
        var printJob = await _printJobRepository.FindByIdAsync(printJobId, cancellationToken);
        if (printJob != null)
        {
            printJob.MarkAsPrinted();
            await _printJobRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[SIMULATED PRINT] Job {PrintJobId} marked as Printed", printJobId);
        }
    }
}
