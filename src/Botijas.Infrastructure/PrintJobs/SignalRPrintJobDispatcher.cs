using Botijas.Application.PrintJobs;
using Botijas.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Botijas.Infrastructure.PrintJobs;

public class SignalRPrintJobDispatcher : IPrintJobDispatcher
{
    private readonly IHubContext<PrintHub> _hubContext;

    public SignalRPrintJobDispatcher(IHubContext<PrintHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task DispatchAsync(
        Guid printJobId, 
        int quantity, 
        string? templateId,
        string? customerName,
        string? customerPhone,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group("PrintGateway").SendAsync(
            "PrintJobCreated",
            printJobId,
            quantity,
            templateId,
            customerName,
            customerPhone,
            cancellationToken);
    }
}
