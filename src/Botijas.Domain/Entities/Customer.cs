using Botijas.Domain.DomainEvents;
using Botijas.Domain.ValueObjects;

namespace Botijas.Domain.Entities;

public class Customer
{
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Customer() // EF Core
    {
        Name = string.Empty;
        Phone = PhoneNumber.Create("000000000");
    }

    private Customer(Guid customerId, string name, PhoneNumber phone)
    {
        CustomerId = customerId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        CreatedAt = DateTime.UtcNow;
    }

    public static Customer Create(string name, PhoneNumber phone)
    {
        var customer = new Customer(Guid.NewGuid(), name, phone);
        customer._domainEvents.Add(new CustomerCreated(customer.CustomerId, customer.Name, customer.Phone.Value));
        return customer;
    }

    public void UpdatePhone(PhoneNumber newPhone)
    {
        Phone = newPhone ?? throw new ArgumentNullException(nameof(newPhone));
    }

    public void UpdateName(string newName)
    {
        Name = newName ?? throw new ArgumentNullException(nameof(newName));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
