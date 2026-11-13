using Ordering.Domain.Entities;
using Ordering.Domain.ValueObjects;

namespace Ordering.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Place_CalculatesTotalFromLines()
    {
        var order = Order.Place(1, "Alex Rivera", "alex@example.com",
            [new OrderLine(1, "Mouse", 2, 10.50m), new OrderLine(2, "Hub", 1, 5m)],
            DateTimeOffset.UtcNow);

        Assert.Equal(26m, order.Total);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.CancelledAt);
    }

    [Fact]
    public void Place_WithoutLines_Throws()
    {
        Assert.Throws<ArgumentException>(() => Order.Place(1, "Alex Rivera", "alex@example.com", [], DateTimeOffset.UtcNow));
    }
}
