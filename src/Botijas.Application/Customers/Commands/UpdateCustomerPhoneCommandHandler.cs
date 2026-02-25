using Botijas.Application.Common;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;

namespace Botijas.Application.Customers.Commands;

public class UpdateCustomerPhoneCommandHandler
{
    private readonly ICustomerRepository _customerRepository;

    public UpdateCustomerPhoneCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Result<CustomerDto>> Handle(
        UpdateCustomerPhoneCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Max-9 digits validation at command level (not in value object, to protect read path)
            var digits = new string(command.Phone.Where(char.IsDigit).ToArray());
            if (digits.Length > 9)
                return Result<CustomerDto>.Failure("Phone number must have at most 9 digits");

            var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
            if (customer == null)
                return Result<CustomerDto>.Failure("Cliente não encontrado");

            var phone = PhoneNumber.Create(command.Phone);

            // Check uniqueness - ensure no OTHER customer has this phone
            var existing = await _customerRepository.FindByPhoneAsync(phone, cancellationToken);
            if (existing != null && existing.CustomerId != command.CustomerId)
                return Result<CustomerDto>.Failure("Customer with this phone already exists");

            customer.UpdatePhone(phone);
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
