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
            var phone = PhoneNumber.Create(command.Phone);
            
            // Verificar se já existe cliente com esse telefone
            var existing = await _customerRepository.FindByPhoneAsync(phone, cancellationToken);
            if (existing != null)
            {
                return Result<CustomerDto>.Failure("Customer with this phone already exists");
            }

            var customer = Customer.Create(command.Name, phone);
            await _customerRepository.AddAsync(customer, cancellationToken);
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
