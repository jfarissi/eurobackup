using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260811120000_AddShippingAmountToSalesDocuments")]
    public partial class AddShippingAmountToSalesDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountHt",
                table: "Quotes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingVatRate",
                table: "Quotes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 21.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountHt",
                table: "SalesOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingVatRate",
                table: "SalesOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 21.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountHt",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingVatRate",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 21.0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShippingAmountHt", table: "Quotes");
            migrationBuilder.DropColumn(name: "ShippingVatRate", table: "Quotes");
            migrationBuilder.DropColumn(name: "ShippingAmountHt", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "ShippingVatRate", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "ShippingAmountHt", table: "SalesInvoices");
            migrationBuilder.DropColumn(name: "ShippingVatRate", table: "SalesInvoices");
        }
    }
}
