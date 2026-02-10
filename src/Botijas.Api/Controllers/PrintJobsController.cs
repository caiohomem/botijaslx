using Botijas.Application.PrintJobs.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/print-jobs")]
public class PrintJobsController : ControllerBase
{
    private readonly CreatePrintJobCommandHandler _createHandler;
    private readonly AckPrintJobPrintedCommandHandler _ackPrintedHandler;
    private readonly AckPrintJobFailedCommandHandler _ackFailedHandler;

    public PrintJobsController(
        CreatePrintJobCommandHandler createHandler,
        AckPrintJobPrintedCommandHandler ackPrintedHandler,
        AckPrintJobFailedCommandHandler ackFailedHandler)
    {
        _createHandler = createHandler;
        _ackPrintedHandler = ackPrintedHandler;
        _ackFailedHandler = ackFailedHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePrintJobCommand command, CancellationToken cancellationToken)
    {
        var result = await _createHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.PrintJobId }, result.Value);
    }

    [HttpPost("{id}/ack-printed")]
    public async Task<IActionResult> AckPrinted(Guid id, CancellationToken cancellationToken)
    {
        var result = await _ackPrintedHandler.Handle(new AckPrintJobPrintedCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost("{id}/ack-failed")]
    public async Task<IActionResult> AckFailed(Guid id, [FromBody] AckPrintJobFailedCommand command, CancellationToken cancellationToken)
    {
        var result = await _ackFailedHandler.Handle(command with { PrintJobId = id }, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // TODO: Implementar quando necessário
        return NotFound();
    }
}
