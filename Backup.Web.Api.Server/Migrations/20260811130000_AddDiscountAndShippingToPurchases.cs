using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260811130000_AddDiscountAndShippingToPurchases")]
    public partial class AddDiscountAndShippingToPurchases : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountPercent",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountHt",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingVatRate",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 21.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "PurchaseOrderLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountPercent",
                table: "SupplierInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountHt",
                table: "SupplierInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingVatRate",
                table: "SupplierInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 21.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "SupplierInvoiceLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HeaderDiscountPercent", table: "PurchaseOrders");
            migrationBuilder.DropColumn(name: "ShippingAmountHt", table: "PurchaseOrders");
            migrationBuilder.DropColumn(name: "ShippingVatRate", table: "PurchaseOrders");
            migrationBuilder.DropColumn(name: "DiscountPercent", table: "PurchaseOrderLines");
            migrationBuilder.DropColumn(name: "HeaderDiscountPercent", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "ShippingAmountHt", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "ShippingVatRate", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "DiscountPercent", table: "SupplierInvoiceLines");
        }
    }
}
