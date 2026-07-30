using Botijas.Application.Orders.Commands;
using Botijas.Domain.Entities;
using Botijas.Domain.Repositories;
using Botijas.Domain.ValueObjects;
using Xunit;

namespace Botijas.Domain.Tests;

public class CloseIntakeCommandHandlerTests
{
    [Fact]
    public async Task Handle_AcceptsExistingAndNewCylindersInSameOrder()
    {
        var fixture = new Fixture();
        var existing = fixture.AddDeliveredCylinder(1);
        var alsoExisting = fixture.AddDeliveredCylinder(2);
        var third = fixture.AddDeliveredCylinder(3);

        var result = await fixture.Handler.Handle(
            new CloseIntakeCommand(
                fixture.Customer.CustomerId,
                "Pickup",
                RefillPaid: false,
                ShippingPaid: false,
                ExistingCylinderIds: [existing.CylinderId, alsoExisting.CylinderId, third.CylinderId],
                NewCylinderCount: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(4, result.Value!.CylinderCount);
        Assert.Equal(4, result.Value.AddedCylinders.Count);
        Assert.False(result.Value.ReusedExistingOrder);
        Assert.All(result.Value.AddedCylinders, c => Assert.Equal("Received", c.State));
    }

    [Fact]
    public async Task Handle_AssignsLabelToNewCylinders()
    {
        var fixture = new Fixture();

        var result = await fixture.Handler.Handle(
            new CloseIntakeCommand(fixture.Customer.CustomerId, NewCylinderCount: 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.All(result.Value!.AddedCylinders, c => Assert.False(string.IsNullOrWhiteSpace(c.LabelToken)));
        Assert.Equal(2, result.Value.AddedCylinders.Select(c => c.LabelToken).Distinct().Count());
    }

    [Fact]
    public async Task Handle_ReusesOpenOrderSoVisitStaysInOneOrder()
    {
        var fixture = new Fixture();
        var existing = fixture.AddDeliveredCylinder(1);

        var first = await fixture.Handler.Handle(
            new CloseIntakeCommand(
                fixture.Customer.CustomerId,
                ExistingCylinderIds: [existing.CylinderId]),
            CancellationToken.None);

        var second = await fixture.Handler.Handle(
            new CloseIntakeCommand(fixture.Customer.CustomerId, NewCylinderCount: 1),
            CancellationToken.None);

        Assert.True(second.IsSuccess, second.Error);
        Assert.True(second.Value!.ReusedExistingOrder);
        Assert.Equal(first.Value!.OrderId, second.Value.OrderId);
        Assert.Equal(2, second.Value.CylinderCount);
    }

    [Fact]
    public async Task Handle_KeepsShippingIntakeSeparateFromPickupOrder()
    {
        var fixture = new Fixture();

        var pickup = await fixture.Handler.Handle(
            new CloseIntakeCommand(fixture.Customer.CustomerId, "Pickup", NewCylinderCount: 1),
            CancellationToken.None);

        var shipping = await fixture.Handler.Handle(
            new CloseIntakeCommand(fixture.Customer.CustomerId, "Shipping", NewCylinderCount: 1),
            CancellationToken.None);

        Assert.True(shipping.IsSuccess, shipping.Error);
        Assert.False(shipping.Value!.ReusedExistingOrder);
        Assert.NotEqual(pickup.Value!.OrderId, shipping.Value.OrderId);
    }

    [Fact]
    public async Task Handle_RejectsCylinderInAnotherCustomersOpenOrder()
    {
        var fixture = new Fixture();
        var existing = fixture.AddDeliveredCylinder(1);
        var otherOrder = RefillOrder.Create(Guid.NewGuid());
        otherOrder.AddCylinder(existing);
        fixture.Orders.Seed(otherOrder);

        var result = await fixture.Handler.Handle(
            new CloseIntakeCommand(
                fixture.Customer.CustomerId,
                ExistingCylinderIds: [existing.CylinderId]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("outro pedido aberto", result.Error);
    }

    [Fact]
    public async Task Handle_RejectsEmptyIntake()
    {
        var fixture = new Fixture();

        var result = await fixture.Handler.Handle(
            new CloseIntakeCommand(fixture.Customer.CustomerId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    private sealed class Fixture
    {
        public Customer Customer { get; }
        public FakeOrderRepository Orders { get; } = new();
        public FakeCylinderRepository Cylinders { get; } = new();
        public FakeHistoryRepository History { get; } = new();
        public CloseIntakeCommandHandler Handler { get; }

        public Fixture()
        {
            Customer = Botijas.Domain.Entities.Customer.Create(
                "Tiago",
                PhoneNumber.Create("912552003"),
                CustomerPhoneType.PT);
            Cylinders.OrdersSource = Orders.All;
            Handler = new CloseIntakeCommandHandler(
                Orders,
                Cylinders,
                History,
                new FakeCustomerRepository(Customer));
        }

        public Cylinder AddDeliveredCylinder(long sequentialNumber)
        {
            var cylinder = Cylinder.Create(sequentialNumber);
            cylinder.MarkAsReady();
            cylinder.MarkAsDelivered();
            Cylinders.Seed(cylinder);
            return cylinder;
        }
    }

    private sealed class FakeOrderRepository : IRefillOrderRepository
    {
        private readonly Dictionary<Guid, RefillOrder> _orders = new();

        public void Seed(RefillOrder order) => _orders[order.OrderId] = order;

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

        public Task<List<RefillOrder>> FindOpenOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_orders.Values
                .Where(o => o.CustomerId == customerId && o.Status == RefillOrderStatus.Open)
                .ToList());

        public IEnumerable<RefillOrder> All => _orders.Values;

        public Task<RefillOrder?> FindByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_orders.Values.FirstOrDefault(o => o.Cylinders.Any(c => c.CylinderId == cylinderId)));

        public Task<List<CurrentCylinderOrderInfo>> FindCurrentCylinderOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RefillOrder>> FindAllByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RefillOrder>> FindReadyForPickupAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<Guid, DateTime>> GetReadyAtByOrderAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCylinderRepository : ICylinderRepository
    {
        private readonly Dictionary<Guid, Cylinder> _cylinders = new();
        private long _nextSequential = 100;

        public void Seed(Cylinder cylinder) => _cylinders[cylinder.CylinderId] = cylinder;

        public Task AddAsync(Cylinder cylinder, CancellationToken cancellationToken = default)
        {
            if (cylinder.SequentialNumber == 0)
            {
                typeof(Cylinder).GetProperty("SequentialNumber")!.SetValue(cylinder, _nextSequential++);
            }

            _cylinders[cylinder.CylinderId] = cylinder;
            return Task.CompletedTask;
        }

        public Task<Cylinder?> FindByIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_cylinders.TryGetValue(cylinderId, out var cylinder) ? cylinder : null);

        public Task<Cylinder?> FindByLabelTokenAsync(LabelToken labelToken, CancellationToken cancellationToken = default)
            => Task.FromResult(_cylinders.Values.FirstOrDefault(c => c.LabelToken?.Value == labelToken.Value));

        public Task<Guid?> FindOpenOrderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
        {
            var order = OrdersSource
                .FirstOrDefault(o => o.Status == RefillOrderStatus.Open && o.Cylinders.Any(c => c.CylinderId == cylinderId));
            return Task.FromResult(order?.OrderId);
        }

        public Task<List<Cylinder>> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var order = OrdersSource.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                return Task.FromResult(new List<Cylinder>());
            }

            return Task.FromResult(order.Cylinders
                .Select(c => _cylinders.TryGetValue(c.CylinderId, out var cylinder) ? cylinder : null)
                .Where(c => c != null)
                .Select(c => c!)
                .ToList());
        }

        public IEnumerable<RefillOrder> OrdersSource { get; set; } = [];

        public Task<int> CountPendingByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountReadyForPickupByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<Cylinder?> FindBySequentialNumberAsync(long sequentialNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_cylinders.Values.FirstOrDefault(c => c.SequentialNumber == sequentialNumber));

        public Task<Cylinder?> FindInOpenOrderAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<Cylinder>> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ProblemCylinderItem>> GetProblemCylindersAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<FillingQueueItem>> GetFillingQueueAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(Cylinder cylinder, CancellationToken cancellationToken = default)
        {
            _cylinders.Remove(cylinder.CylinderId);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHistoryRepository : ICylinderHistoryRepository
    {
        public List<CylinderHistoryEntry> Entries { get; } = new();

        public Task AddAsync(CylinderHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<CylinderHistoryEntry?> FindByIdAsync(Guid historyEntryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(e => e.Id == historyEntryId));

        public Task<List<CylinderHistoryEntry>> GetByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.Where(e => e.CylinderId == cylinderId).ToList());

        public Task<CylinderHistoryEntry?> GetLatestByCylinderIdAsync(Guid cylinderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.LastOrDefault(e => e.CylinderId == cylinderId));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer _customer;

        public FakeCustomerRepository(Customer customer) => _customer = customer;

        public Task<Customer?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(customerId == _customer.CustomerId ? _customer : null);

        public Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<Customer>> SearchAsync(string? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
