namespace Botijas.Application.Customers.Commands;

public record UpdateCustomerPhoneCommand(Guid CustomerId, string Phone, string PhoneType);
