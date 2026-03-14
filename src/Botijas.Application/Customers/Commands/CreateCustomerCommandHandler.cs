using Botijas.Application.Common;
using Botijas.Domain.Entities;
using Botijas.Domain.ValueObjects;
using Botijas.Domain.Repositories;

namespace Botijas.Application.Customers.Commands;

public class CreateCustomerCommandHandler
{
    private readonly ICustomerRepository _customerRepository;

    public CreateCustomerCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (!CustomerPhoneTypeParser.TryParse(command.PhoneType, out var phoneType))
            {
                return Result<CustomerDto>.Failure("Invalid phone type");
            }

            var phone = PhoneNumber.Create(command.Phone);
            var digits = phone.Value;
            if (phoneType == CustomerPhoneType.PT && digits.Length != 9)
            {
                return Result<CustomerDto>.Failure("PT phone number must have exactly 9 digits");
            }

            if (phoneType == CustomerPhoneType.International && digits.Length > 14)
            {
                return Result<CustomerDto>.Failure("International phone number must have at most 14 digits");
            }
            
            // Verificar se já existe cliente com esse telefone
            var existing = await _customerRepository.FindByPhoneAsync(phone, cancellationToken);
            if (existing != null)
            {
                return Result<CustomerDto>.Failure("Customer with this phone already exists");
            }

            var customer = Customer.Create(command.Name, phone, phoneType);
            await _customerRepository.AddAsync(customer, cancellationToken);
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
