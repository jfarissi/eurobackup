using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260731140000_AddInvoiceLineOrderedDeliveredQty")]
    public partial class AddInvoiceLineOrderedDeliveredQty : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQuantity",
                table: "SalesInvoiceLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQuantity",
                table: "SalesInvoiceLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Backfill: existing invoices — ordered/delivered = invoiced qty
            migrationBuilder.Sql(@"
UPDATE SalesInvoiceLines
SET OrderedQuantity = Quantity, DeliveredQuantity = Quantity
WHERE OrderedQuantity = 0 AND DeliveredQuantity = 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OrderedQuantity", table: "SalesInvoiceLines");
            migrationBuilder.DropColumn(name: "DeliveredQuantity", table: "SalesInvoiceLines");
        }
    }
}
