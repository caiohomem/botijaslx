using Botijas.Application.Pickup;
using Xunit;

namespace Botijas.Domain.Tests;

public class PickupOrderConsolidatorTests
{
    [Fact]
    public void ConsolidateByCustomer_MergesDuplicateReadyOrdersIntoOneCard()
    {
        var customerId = Guid.NewGuid();
        var olderOrderId = Guid.NewGuid();
        var newerOrderId = Guid.NewGuid();

        var orders = new List<PickupOrderDto>
        {
            new()
            {
                OrderId = olderOrderId,
                CustomerId = customerId,
                CustomerName = "João Michel",
                CustomerPhone = "914229090",
                FulfillmentMethod = "Pickup",
                CreatedAt = new DateTime(2026, 6, 5, 19, 47, 0, DateTimeKind.Utc),
                ReadyAt = new DateTime(2026, 6, 5, 19, 47, 0, DateTimeKind.Utc),
                NeedsNotification = false,
                Cylinders =
                [
                    new PickupCylinderDto
                    {
                        CylinderId = Guid.NewGuid(),
                        SequentialNumber = 1188,
                        State = "Ready",
                        OrderId = olderOrderId,
                    },
                    new PickupCylinderDto
                    {
                        CylinderId = Guid.NewGuid(),
                        SequentialNumber = 1189,
                        State = "Ready",
                        OrderId = olderOrderId,
                    },
                ],
            },
            new()
            {
                OrderId = newerOrderId,
                CustomerId = customerId,
                CustomerName = "João Michel",
                CustomerPhone = "914229090",
                FulfillmentMethod = "Pickup",
                CreatedAt = new DateTime(2026, 9, 7, 13, 20, 0, DateTimeKind.Utc),
                ReadyAt = new DateTime(2026, 9, 7, 13, 20, 0, DateTimeKind.Utc),
                NeedsNotification = true,
                Cylinders =
                [
                    new PickupCylinderDto
                    {
                        CylinderId = Guid.NewGuid(),
                        SequentialNumber = 1190,
                        State = "Ready",
                        OrderId = newerOrderId,
                    },
                    new PickupCylinderDto
                    {
                        CylinderId = Guid.NewGuid(),
                        SequentialNumber = 1191,
                        State = "Ready",
                        OrderId = newerOrderId,
                    },
                ],
            },
        };

        var consolidated = PickupOrderConsolidator.ConsolidateByCustomer(orders);

        Assert.Single(consolidated);
        var card = consolidated[0];
        Assert.Equal(olderOrderId, card.OrderId);
        Assert.Equal(4, card.TotalCylinders);
        Assert.True(card.NeedsNotification);
        Assert.Equal(new DateTime(2026, 6, 5, 19, 47, 0, DateTimeKind.Utc), card.ReadyAt);
        Assert.Equal(4, card.Cylinders.Count);
        Assert.Contains(card.Cylinders, c => c.OrderId == newerOrderId);
        Assert.Equal([1188L, 1189L, 1190L, 1191L], card.Cylinders.Select(c => c.SequentialNumber).ToArray());
    }

    [Fact]
    public void ConsolidateByCustomer_KeepsSeparateCardsForDifferentFulfillmentMethods()
    {
        var customerId = Guid.NewGuid();

        var orders = new List<PickupOrderDto>
        {
            new()
            {
                OrderId = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = "Cliente",
                FulfillmentMethod = "Pickup",
                CreatedAt = DateTime.UtcNow,
                Cylinders = [new PickupCylinderDto { CylinderId = Guid.NewGuid(), SequentialNumber = 1, OrderId = Guid.NewGuid() }],
            },
            new()
            {
                OrderId = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = "Cliente",
                FulfillmentMethod = "Shipping",
                CreatedAt = DateTime.UtcNow,
                Cylinders = [new PickupCylinderDto { CylinderId = Guid.NewGuid(), SequentialNumber = 2, OrderId = Guid.NewGuid() }],
            },
        };

        var consolidated = PickupOrderConsolidator.ConsolidateByCustomer(orders);

        Assert.Equal(2, consolidated.Count);
    }
}
