using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Customers.Queries;

public class SearchCustomersQueryHandler
{
    private readonly ICustomerRepository _customerRepository;

    public SearchCustomersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Result<List<CustomerDto>>> Handle(SearchCustomersQuery query, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.SearchAsync(query.Query, cancellationToken);
        
        var dtos = customers.Select(c => new CustomerDto
        {
            CustomerId = c.CustomerId,
            Name = c.Name,
            Phone = c.Phone.Value
        }).ToList();

        return Result<List<CustomerDto>>.Success(dtos);
    }
}
