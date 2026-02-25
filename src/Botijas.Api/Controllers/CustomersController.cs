using Botijas.Application.Customers;
using Botijas.Application.Customers.Commands;
using Botijas.Application.Customers.Queries;
using Botijas.Api.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CreateCustomerCommandHandler _createHandler;
    private readonly SearchCustomersQueryHandler _searchHandler;
    private readonly GetCustomerCylindersQueryHandler _customerCylindersHandler;
    private readonly UpdateCustomerPhoneCommandHandler _updatePhoneHandler;
    private readonly DeleteCustomerHandler _deleteHandler;

    public CustomersController(
        CreateCustomerCommandHandler createHandler,
        SearchCustomersQueryHandler searchHandler,
        GetCustomerCylindersQueryHandler customerCylindersHandler,
        UpdateCustomerPhoneCommandHandler updatePhoneHandler,
        DeleteCustomerHandler deleteHandler)
    {
        _createHandler = createHandler;
        _searchHandler = searchHandler;
        _customerCylindersHandler = customerCylindersHandler;
        _updatePhoneHandler = updatePhoneHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var result = await _createHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.CustomerId }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var result = await _searchHandler.Handle(new SearchCustomersQuery(query), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { customers = result.Value });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // TODO: Implementar quando necessário
        return NotFound();
    }

    [HttpGet("{id}/cylinders")]
    public async Task<IActionResult> GetCustomerCylinders(Guid id, CancellationToken cancellationToken)
    {
        var result = await _customerCylindersHandler.Handle(new GetCustomerCylindersQuery(id), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPut("{id}/phone")]
    public async Task<IActionResult> UpdatePhone(
        Guid id,
        [FromBody] UpdatePhoneRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updatePhoneHandler.Handle(
            new UpdateCustomerPhoneCommand(id, request.Phone),
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteHandler.Handle(id, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }
}

public record UpdatePhoneRequest(string Phone);
