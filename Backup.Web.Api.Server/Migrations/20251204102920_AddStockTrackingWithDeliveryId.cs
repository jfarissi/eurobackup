using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTrackingWithDeliveryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastDeliveryId",
                table: "Stock",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProductKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuantityDelta = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockUpdates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StockUpdates_DeliveryId",
                table: "StockUpdates",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockUpdates_ProductKey",
                table: "StockUpdates",
                column: "ProductKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockUpdates");

            migrationBuilder.DropColumn(
                name: "LastDeliveryId",
                table: "Stock");
        }
    }
}
