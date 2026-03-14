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
            if (!CustomerPhoneTypeParser.TryParse(command.PhoneType, out var phoneType))
            {
                return Result<CustomerDto>.Failure("Invalid phone type");
            }

            var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
            if (customer == null)
                return Result<CustomerDto>.Failure("Cliente não encontrado");

            var phone = PhoneNumber.Create(command.Phone);
            var digits = phone.Value;
            if (phoneType == Domain.Entities.CustomerPhoneType.PT && digits.Length != 9)
                return Result<CustomerDto>.Failure("PT phone number must have exactly 9 digits");

            if (phoneType == Domain.Entities.CustomerPhoneType.International && digits.Length > 14)
                return Result<CustomerDto>.Failure("International phone number must have at most 14 digits");

            // Check uniqueness - ensure no OTHER customer has this phone
            var existing = await _customerRepository.FindByPhoneAsync(phone, cancellationToken);
            if (existing != null && existing.CustomerId != command.CustomerId)
                return Result<CustomerDto>.Failure("Customer with this phone already exists");

            customer.UpdatePhone(phone, phoneType);
            await _customerRepository.UpdateAsync(customer, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);

            return Result<CustomerDto>.Success(new CustomerDto
            {
                CustomerId = customer.CustomerId,
                Name = customer.Name,
                Phone = customer.Phone.Value,
                PhoneType = customer.PhoneType.ToString()
            });
        }
        catch (ArgumentException ex)
        {
            return Result<CustomerDto>.Failure(ex.Message);
        }
    }
}
