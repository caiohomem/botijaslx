using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Botijas.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly BotijasDbContext _context;

    public CustomerRepository(BotijasDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
    }

    public async Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default)
    {
        // EF Core armazena Phone como string no banco, então carregamos e comparamos em memória
        var phoneValue = phone.Value;
        var customers = await _context.Customers.ToListAsync(cancellationToken);
        return customers.FirstOrDefault(c => c.Phone.Value == phoneValue);
    }

    public async Task<List<Customer>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await _context.Customers
                .OrderBy(c => c.Name)
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        // Carregar todos e filtrar em memória devido à conversão de Value Objects
        var searchTerm = query.ToLower();
        var customers = await _context.Customers.ToListAsync(cancellationToken);
        
        return customers
            .Where(c =>
                c.Name.ToLower().Contains(searchTerm) ||
                c.Phone.Value.Contains(searchTerm)
            )
            .OrderBy(c => c.Name)
            .Take(50)
            .ToList();
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _context.Customers.Update(customer);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _context.Customers.Remove(customer);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
