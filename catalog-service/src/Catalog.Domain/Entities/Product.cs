namespace Catalog.Domain.Entities;

public sealed class Product
{
    public Product(string name, string category, decimal price, string description)
    {
        Name = name;
        Category = category;
        Price = price;
        Description = description;
    }

    /// <summary>Assigned by the repository when the product is stored.</summary>
    public int Id { get; set; }

    public string Name { get; private set; }

    public string Category { get; private set; }

    public decimal Price { get; private set; }

    public string Description { get; private set; }

    public void Update(string name, string category, decimal price, string description)
    {
        Name = name;
        Category = category;
        Price = price;
        Description = description;
    }
}
