using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "products",
                columns: new[] { "id", "category", "description", "name", "price" },
                values: new object[,]
                {
                    { 1, "Accessories", "Ergonomic 2.4 GHz wireless mouse.", "Wireless Mouse", 24.99m },
                    { 2, "Accessories", "Tenkeyless keyboard with brown switches.", "Mechanical Keyboard", 89.00m },
                    { 3, "Displays", "QHD IPS monitor, 75 Hz.", "27-inch Monitor", 279.99m },
                    { 4, "Accessories", "7-in-1 hub with HDMI and card reader.", "USB-C Hub", 39.50m },
                    { 5, "Audio", "Over-ear Bluetooth headphones.", "Noise-Cancelling Headphones", 199.00m },
                    { 6, "Video", "Full HD webcam with built-in microphone.", "1080p Webcam", 59.99m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                table: "products",
                column: "category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
