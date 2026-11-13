using Catalog.Domain.Entities;

namespace Catalog.Infrastructure.Persistence;

/// <summary>Sample products, inserted by the initial migration. Ids 1-6 match the stock seeded by inventory-service.</summary>
internal static class SeedData
{
    public static IEnumerable<Product> Products() =>
    [
        new("Wireless Mouse", "Accessories", 24.99m, "Ergonomic 2.4 GHz wireless mouse.") { Id = 1 },
        new("Mechanical Keyboard", "Accessories", 89.00m, "Tenkeyless keyboard with brown switches.") { Id = 2 },
        new("27-inch Monitor", "Displays", 279.99m, "QHD IPS monitor, 75 Hz.") { Id = 3 },
        new("USB-C Hub", "Accessories", 39.50m, "7-in-1 hub with HDMI and card reader.") { Id = 4 },
        new("Noise-Cancelling Headphones", "Audio", 199.00m, "Over-ear Bluetooth headphones.") { Id = 5 },
        new("1080p Webcam", "Video", 59.99m, "Full HD webcam with built-in microphone.") { Id = 6 },
    ];
}
