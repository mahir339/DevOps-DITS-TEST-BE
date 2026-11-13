using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Entities;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order() // for EF Core
    {
        CustomerName = string.Empty;
        CustomerEmail = string.Empty;
        Status = OrderStatus.Confirmed;
    }

    private Order(int customerId, string customerName, string customerEmail, IEnumerable<OrderLine> lines, DateTimeOffset createdAt)
    {
        CustomerId = customerId;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        _lines.AddRange(lines);
        CreatedAt = createdAt;
        Status = OrderStatus.Confirmed;
    }

    /// <summary>Assigned by the database when the order is stored.</summary>
    public int Id { get; set; }

    public int CustomerId { get; private set; }

    public string CustomerName { get; private set; }

    /// <summary>Copied from customer-service when the order is placed (used for the confirmation email).</summary>
    public string CustomerEmail { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public decimal Total => _lines.Sum(l => l.LineTotal);

    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public static Order Place(int customerId, string customerName, string customerEmail, IEnumerable<OrderLine> lines, DateTimeOffset createdAt)
    {
        var orderLines = lines.ToList();
        if (orderLines.Count == 0)
            throw new ArgumentException("An order needs at least one line.", nameof(lines));

        return new Order(customerId, customerName, customerEmail, orderLines, createdAt);
    }

    /// <summary>Returns false when the order was already cancelled.</summary>
    public bool Cancel(DateTimeOffset now)
    {
        if (Status == OrderStatus.Cancelled) return false;
        Status = OrderStatus.Cancelled;
        CancelledAt = now;
        return true;
    }
}
