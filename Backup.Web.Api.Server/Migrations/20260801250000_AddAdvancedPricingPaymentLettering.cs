using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>
    /// RG-CP1 (devise figée) + RG-CP3 (remise pied de page) + RG-RM1–5 (remise ligne) + RG-LS1–5 (n° de lot lite)
    /// + RG-RS2 (allocation stricte) + RG-RG2 (allocation de règlement / paiement par lot) + RG-LT1–4 (lettrage lite)
    /// + RG-PT1–5 (tarif client lite).
    /// </summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801250000_AddAdvancedPricingPaymentLettering")]
    public partial class AddAdvancedPricingPaymentLettering : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RG-CP1 : devise figée à la création, gelée hors Draft.
            foreach (var table in new[]
            {
                "Quotes", "SalesOrders", "SalesInvoices", "CreditNotes", "PurchaseOrders",
                "SupplierInvoices", "Proformas", "DepositInvoices", "SalesReturns", "SupplierReturns"
            })
            {
                migrationBuilder.AddColumn<string>(
                    name: "CurrencyCode",
                    table: table,
                    type: "varchar(8)",
                    maxLength: 8,
                    nullable: false,
                    defaultValue: "EUR")
                    .Annotation("MySql:CharSet", "utf8mb4");
            }

            // RG-CP3 : remise pied de page (%).
            foreach (var table in new[] { "Quotes", "SalesOrders", "SalesInvoices" })
            {
                migrationBuilder.AddColumn<decimal>(
                    name: "HeaderDiscountPercent",
                    table: table,
                    type: "decimal(9,4)",
                    precision: 9,
                    scale: 4,
                    nullable: false,
                    defaultValue: 0m);
            }

            // RG-RM1–5 : remise ligne (%), 0-100.
            foreach (var table in new[] { "QuoteLines", "SalesOrderLines", "SalesInvoiceLines" })
            {
                migrationBuilder.AddColumn<decimal>(
                    name: "DiscountPercent",
                    table: table,
                    type: "decimal(9,4)",
                    precision: 9,
                    scale: 4,
                    nullable: false,
                    defaultValue: 0m);
            }

            // RG-LS1–5 lite : n° de lot (BL -> facture), traçabilité simple sans FEFO.
            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "SalesDeliveryNoteLines",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "SalesInvoiceLines",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // RG-RS2 lite : allocation stricte à la confirmation de commande.
            migrationBuilder.AddColumn<bool>(
                name: "RequireHardAllocation",
                table: "Companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // RG-RG2 : audit d'allocation de règlement (paiement par lot).
            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // RG-LT1–4 lite : lettrage client (regroupement manuel de factures/paiements/avoirs).
            migrationBuilder.CreateTable(
                name: "LetteringGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LetteringCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnletteredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UnletteredBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetteringGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetteringGroups_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LetteringLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LetteringGroupId = table.Column<int>(type: "int", nullable: false),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetteringLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetteringLines_LetteringGroups_LetteringGroupId",
                        column: x => x.LetteringGroupId,
                        principalTable: "LetteringGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // RG-PT1–5 lite : tarif spécifique client par référence produit.
            migrationBuilder.CreateTable(
                name: "CustomerPriceListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ProductKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPriceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPriceListItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_BatchId",
                table: "PaymentAllocations",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_SalesInvoiceId",
                table: "PaymentAllocations",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LetteringGroups_CustomerId",
                table: "LetteringGroups",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LetteringGroups_LetteringCode_CompanyId",
                table: "LetteringGroups",
                columns: new[] { "LetteringCode", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetteringLines_LetteringGroupId",
                table: "LetteringLines",
                column: "LetteringGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceListItems_CustomerId_ProductKey",
                table: "CustomerPriceListItems",
                columns: new[] { "CustomerId", "ProductKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentAllocations");
            migrationBuilder.DropTable(name: "LetteringLines");
            migrationBuilder.DropTable(name: "LetteringGroups");
            migrationBuilder.DropTable(name: "CustomerPriceListItems");

            migrationBuilder.DropColumn(name: "RequireHardAllocation", table: "Companies");

            migrationBuilder.DropColumn(name: "LotNumber", table: "SalesInvoiceLines");
            migrationBuilder.DropColumn(name: "LotNumber", table: "SalesDeliveryNoteLines");

            foreach (var table in new[] { "QuoteLines", "SalesOrderLines", "SalesInvoiceLines" })
            {
                migrationBuilder.DropColumn(name: "DiscountPercent", table: table);
            }

            foreach (var table in new[] { "Quotes", "SalesOrders", "SalesInvoices" })
            {
                migrationBuilder.DropColumn(name: "HeaderDiscountPercent", table: table);
            }

            foreach (var table in new[]
            {
                "Quotes", "SalesOrders", "SalesInvoices", "CreditNotes", "PurchaseOrders",
                "SupplierInvoices", "Proformas", "DepositInvoices", "SalesReturns", "SupplierReturns"
            })
            {
                migrationBuilder.DropColumn(name: "CurrencyCode", table: table);
            }
        }
    }
}
