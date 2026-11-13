namespace Ordering.Domain.ValueObjects;

public sealed record OrderLine(int ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
