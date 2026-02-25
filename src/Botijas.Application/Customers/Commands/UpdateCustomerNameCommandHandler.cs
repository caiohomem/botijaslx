using Botijas.Application.Common;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Customers.Commands;

public class UpdateCustomerNameCommandHandler
{
    private readonly ICustomerRepository _customerRepository;

    public UpdateCustomerNameCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Result<CustomerDto>> Handle(
        UpdateCustomerNameCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.NewName))
                return Result<CustomerDto>.Failure("Customer name cannot be empty");

            var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
            if (customer == null)
                return Result<CustomerDto>.Failure("Cliente não encontrado");

            customer.UpdateName(command.NewName.Trim());
            await _customerRepository.UpdateAsync(customer, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);

            return Result<CustomerDto>.Success(new CustomerDto
            {
                CustomerId = customer.CustomerId,
                Name = customer.Name,
                Phone = customer.Phone.Value
            });
        }
        catch (ArgumentException ex)
        {
            return Result<CustomerDto>.Failure(ex.Message);
        }
    }
}
