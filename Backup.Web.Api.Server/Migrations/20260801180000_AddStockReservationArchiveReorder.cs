using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801180000_AddStockReservationArchiveReorder")]
    public partial class AddStockReservationArchiveReorder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "Stock",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinStock",
                table: "Stock",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "SalesOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            AddArchive(migrationBuilder, "SalesOrders");
            AddArchive(migrationBuilder, "SalesDeliveryNotes");
            AddArchive(migrationBuilder, "SalesInvoices");
            AddArchive(migrationBuilder, "Quotes");
            AddArchive(migrationBuilder, "CreditNotes");
        }

        private static void AddArchive(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: table,
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: table,
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: table,
                type: "varchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: $"IX_{table}_IsArchived",
                table: table,
                column: "IsArchived");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropArchive(migrationBuilder, "SalesOrders");
            DropArchive(migrationBuilder, "SalesDeliveryNotes");
            DropArchive(migrationBuilder, "SalesInvoices");
            DropArchive(migrationBuilder, "Quotes");
            DropArchive(migrationBuilder, "CreditNotes");

            migrationBuilder.DropColumn(name: "ReservedQuantity", table: "SalesOrderLines");
            migrationBuilder.DropColumn(name: "MinStock", table: "Stock");
            migrationBuilder.DropColumn(name: "ReservedQuantity", table: "Stock");
        }

        private static void DropArchive(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropIndex(name: $"IX_{table}_IsArchived", table: table);
            migrationBuilder.DropColumn(name: "ArchivedBy", table: table);
            migrationBuilder.DropColumn(name: "ArchivedAt", table: table);
            migrationBuilder.DropColumn(name: "IsArchived", table: table);
        }
    }
}
