using Botijas.Application.Orders.Commands;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Xunit;

namespace Botijas.Domain.Tests;

public class DeleteEmptyOpenOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeletesOpenOrderWithNoCylinders()
    {
        var order = RefillOrder.Create(Guid.NewGuid());
        var repo = new InMemoryOrderRepository(order);
        var handler = new DeleteEmptyOpenOrderCommandHandler(repo);

        var result = await handler.Handle(new DeleteEmptyOpenOrderCommand(order.OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await repo.FindByIdAsync(order.OrderId));
    }

    [Fact]
    public async Task Handle_RejectsOrderWithCylinders()
    {
        var order = RefillOrder.Create(Guid.NewGuid());
        order.AddCylinder(Cylinder.Create(1));
        var repo = new InMemoryOrderRepository(order);
        var handler = new DeleteEmptyOpenOrderCommandHandler(repo);

        var result = await handler.Handle(new DeleteEmptyOpenOrderCommand(order.OrderId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(await repo.FindByIdAsync(order.OrderId));
    }

    [Fact]
    public async Task Handle_RejectsNonOpenOrder()
    {
        var order = RefillOrder.Create(Guid.NewGuid());
        order.Cancel("teste");
        var repo = new InMemoryOrderRepository(order);
        var handler = new DeleteEmptyOpenOrderCommandHandler(repo);

        var result = await handler.Handle(new DeleteEmptyOpenOrderCommand(order.OrderId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(await repo.FindByIdAsync(order.OrderId));
    }

    private sealed class InMemoryOrderRepository : IRefillOrderRepository
    {
        private readonly Dictionary<Guid, RefillOrder> _orders = new();

        public InMemoryOrderRepository(RefillOrder order)
        {
            _orders[order.OrderId] = order;
        }

        public Task AddAsync(RefillOrder order, CancellationToken cancellationToken = default)
        {
            _orders[order.OrderId] = order;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(RefillOrder order, CancellationToken cancellationToken = default)
        {
            _orders.Remove(order.OrderId);
            return Task.CompletedTask;
        }

        public Task<RefillOrder?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_orders.TryGetValue(orderId, out var order) ? order : null);

        public Task<RefillOrder?> FindByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<CurrentCylinderOrderInfo>> FindCurrentCylinderOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RefillOrder>> FindOpenOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RefillOrder>> FindAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RefillOrder>> FindReadyForPickupAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<Guid, DateTime>> GetReadyAtByOrderAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
