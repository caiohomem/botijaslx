using Botijas.Domain.Entities;
using Botijas.Domain.ValueObjects;

namespace Botijas.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
    Task<List<Customer>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
    Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
