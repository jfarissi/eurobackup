using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260803173000_AddSupplierIdToSalesLines")]
    public partial class AddSupplierIdToSalesLines : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "QuoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "SalesInvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "SalesDeliveryNoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_SupplierId",
                table: "QuoteLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SupplierId",
                table: "SalesOrderLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_SupplierId",
                table: "SalesInvoiceLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryNoteLines_SupplierId",
                table: "SalesDeliveryNoteLines",
                column: "SupplierId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_QuoteLines_SupplierId", table: "QuoteLines");
            migrationBuilder.DropIndex(name: "IX_SalesOrderLines_SupplierId", table: "SalesOrderLines");
            migrationBuilder.DropIndex(name: "IX_SalesInvoiceLines_SupplierId", table: "SalesInvoiceLines");
            migrationBuilder.DropIndex(name: "IX_SalesDeliveryNoteLines_SupplierId", table: "SalesDeliveryNoteLines");

            migrationBuilder.DropColumn(name: "SupplierId", table: "QuoteLines");
            migrationBuilder.DropColumn(name: "SupplierId", table: "SalesOrderLines");
            migrationBuilder.DropColumn(name: "SupplierId", table: "SalesInvoiceLines");
            migrationBuilder.DropColumn(name: "SupplierId", table: "SalesDeliveryNoteLines");
        }
    }
}
