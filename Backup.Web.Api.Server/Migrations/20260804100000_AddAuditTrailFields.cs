using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>
    /// Ajoute CreatedAt/UpdatedAt/CreatedBy/UpdatedBy sur documents métier (en-têtes + lignes),
    /// clients, fournisseurs et catalogue.
    /// </summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260804100000_AddAuditTrailFields")]
    public partial class AddAuditTrailFields : Migration
    {
        private static readonly string[] HeaderTablesWithCreatedAt =
        {
            "Quotes", "SalesOrders", "SalesDeliveryNotes", "SalesInvoices", "CreditNotes",
            "SalesReturns", "Proformas", "DepositInvoices", "PurchaseOrders", "SupplierInvoices",
            "SupplierCreditNotes", "SupplierReturns", "SupplierRfqs"
        };

        private static readonly string[] HeaderTablesWithCreatedAtAndUpdatedAt =
        {
            "Customers", "Suppliers", "ErpProducts"
        };

        /// <summary>ErpReceipts a déjà CreatedAt + CreatedBy.</summary>
        private static readonly string[] HeaderTablesReceipt = { "ErpReceipts" };

        private static readonly string[] LineTablesAllNew =
        {
            "QuoteLines", "SalesOrderLines", "SalesDeliveryNoteLines", "SalesInvoiceLines",
            "CreditNoteLines", "SalesReturnLines", "ProformaLines", "PurchaseOrderLines",
            "ErpReceiptLines", "SupplierInvoiceLines", "SupplierCreditNoteLines",
            "SupplierReturnLines", "SupplierRfqLines"
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in HeaderTablesWithCreatedAt)
            {
                AddUpdatedAt(migrationBuilder, table);
                AddActorColumns(migrationBuilder, table);
            }

            foreach (var table in HeaderTablesWithCreatedAtAndUpdatedAt)
            {
                AddActorColumns(migrationBuilder, table);
            }

            foreach (var table in HeaderTablesReceipt)
            {
                AddUpdatedAt(migrationBuilder, table);
                migrationBuilder.AddColumn<string>(
                    name: "UpdatedBy",
                    table: table,
                    type: "varchar(128)",
                    maxLength: 128,
                    nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4");
            }

            foreach (var table in LineTablesAllNew)
            {
                AddCreatedAt(migrationBuilder, table);
                AddUpdatedAt(migrationBuilder, table);
                AddActorColumns(migrationBuilder, table);
            }

            // Backfill UpdatedAt depuis CreatedAt quand disponible
            foreach (var table in HeaderTablesWithCreatedAt)
            {
                migrationBuilder.Sql($"UPDATE `{table}` SET `UpdatedAt` = `CreatedAt` WHERE `UpdatedAt` < `CreatedAt` OR `UpdatedAt` IS NULL;");
            }

            migrationBuilder.Sql("UPDATE `ErpReceipts` SET `UpdatedAt` = `CreatedAt` WHERE `UpdatedAt` < `CreatedAt` OR `UpdatedAt` IS NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in HeaderTablesWithCreatedAt)
            {
                DropColumnSafe(migrationBuilder, table, "UpdatedAt");
                DropColumnSafe(migrationBuilder, table, "CreatedBy");
                DropColumnSafe(migrationBuilder, table, "UpdatedBy");
            }

            foreach (var table in HeaderTablesWithCreatedAtAndUpdatedAt)
            {
                DropColumnSafe(migrationBuilder, table, "CreatedBy");
                DropColumnSafe(migrationBuilder, table, "UpdatedBy");
            }

            foreach (var table in HeaderTablesReceipt)
            {
                DropColumnSafe(migrationBuilder, table, "UpdatedAt");
                DropColumnSafe(migrationBuilder, table, "UpdatedBy");
            }

            foreach (var table in LineTablesAllNew)
            {
                DropColumnSafe(migrationBuilder, table, "CreatedAt");
                DropColumnSafe(migrationBuilder, table, "UpdatedAt");
                DropColumnSafe(migrationBuilder, table, "CreatedBy");
                DropColumnSafe(migrationBuilder, table, "UpdatedBy");
            }
        }

        private static void AddCreatedAt(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: table,
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        private static void AddUpdatedAt(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: table,
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        private static void AddActorColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: table,
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: table,
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        private static void DropColumnSafe(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.DropColumn(name: column, table: table);
        }
    }
}
