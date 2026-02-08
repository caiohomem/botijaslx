namespace Botijas.Application.Customers;

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
