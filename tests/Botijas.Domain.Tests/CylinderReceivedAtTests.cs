using Botijas.Domain.Services;
using Xunit;

namespace Botijas.Domain.Tests;

public class CylinderReceivedAtTests
{
    [Fact]
    public void Resolve_PrefersReceivedEventOverOrderCreatedAt()
    {
        var receivedAt = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
        var orderCreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var result = CylinderReceivedAt.Resolve(receivedAt, orderCreatedAt);

        Assert.Equal(receivedAt, result);
    }

    [Fact]
    public void Resolve_FallsBackToOrderCreatedAtWhenEventMissing()
    {
        var orderCreatedAt = new DateTime(2026, 8, 4, 9, 30, 0, DateTimeKind.Utc);

        var result = CylinderReceivedAt.Resolve(null, orderCreatedAt);

        Assert.Equal(orderCreatedAt, result);
    }

    [Fact]
    public void Resolve_DoesNotUseOldCylinderCreatedAt()
    {
        // Botija criada há meses, recebida hoje — sem evento, o fallback é o pedido de hoje.
        var orderCreatedAt = new DateTime(2026, 8, 4, 11, 0, 0, DateTimeKind.Utc);
        var oldCylinderCreatedAt = new DateTime(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc);

        var result = CylinderReceivedAt.Resolve(null, orderCreatedAt);

        Assert.Equal(orderCreatedAt, result);
        Assert.NotEqual(oldCylinderCreatedAt, result);
    }
}
