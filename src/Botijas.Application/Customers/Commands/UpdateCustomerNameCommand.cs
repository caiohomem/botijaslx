namespace Botijas.Application.Customers.Commands;

public record UpdateCustomerNameCommand(Guid CustomerId, string NewName);
