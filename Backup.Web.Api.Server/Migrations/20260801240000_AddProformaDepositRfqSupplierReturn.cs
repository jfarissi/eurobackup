using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>
    /// RG-DV7 (versionning devis) + RG-PF1–4 (proforma) + RG-AA1–4 (facture d'acompte)
    /// + RG-DPF1–4 (demande de prix fournisseur) + RG-BRF1–5 (retour fournisseur).
    /// </summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801240000_AddProformaDepositRfqSupplierReturn")]
    public partial class AddProformaDepositRfqSupplierReturn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RG-DV7 : versionning devis post-envoi.
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Quotes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // RG-PF1–4 : facture proforma (PRO-), sans effet GL/stock.
            migrationBuilder.CreateTable(
                name: "Proformas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProformaNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    QuoteId = table.Column<int>(type: "int", nullable: true),
                    SalesOrderId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalHT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proformas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proformas_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Proformas_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Proformas_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProformaLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProformaId = table.Column<int>(type: "int", nullable: false),
                    ProductKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalHT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProformaLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProformaLines_Proformas_ProformaId",
                        column: x => x.ProformaId,
                        principalTable: "Proformas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_ProformaLines_ProformaId", table: "ProformaLines", column: "ProformaId");
            migrationBuilder.CreateIndex(name: "IX_Proformas_CustomerId", table: "Proformas", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_Proformas_QuoteId", table: "Proformas", column: "QuoteId");
            migrationBuilder.CreateIndex(name: "IX_Proformas_SalesOrderId", table: "Proformas", column: "SalesOrderId");
            migrationBuilder.CreateIndex(name: "IX_Proformas_IsDeleted", table: "Proformas", column: "IsDeleted");
            migrationBuilder.CreateIndex(
                name: "IX_Proformas_ProformaNumber_CompanyId",
                table: "Proformas",
                columns: new[] { "ProformaNumber", "CompanyId" },
                unique: true);

            // RG-AA1–4 : facture d'acompte (AAC-), liée à une commande.
            migrationBuilder.CreateTable(
                name: "DepositInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DepositNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AmountHT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountTTC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppliedSalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepositInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepositInvoices_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositInvoices_SalesInvoices_AppliedSalesInvoiceId",
                        column: x => x.AppliedSalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_DepositInvoices_CustomerId", table: "DepositInvoices", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_DepositInvoices_SalesOrderId", table: "DepositInvoices", column: "SalesOrderId");
            migrationBuilder.CreateIndex(name: "IX_DepositInvoices_AppliedSalesInvoiceId", table: "DepositInvoices", column: "AppliedSalesInvoiceId");
            migrationBuilder.CreateIndex(
                name: "IX_DepositInvoices_DepositNumber_CompanyId",
                table: "DepositInvoices",
                columns: new[] { "DepositNumber", "CompanyId" },
                unique: true);

            // RG-DPF1–4 : demande de prix fournisseur (DPF-).
            migrationBuilder.CreateTable(
                name: "SupplierRfqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RfqNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRfqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRfqs_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SupplierRfqLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SupplierRfqId = table.Column<int>(type: "int", nullable: false),
                    ProductKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EstimatedUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRfqLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRfqLines_SupplierRfqs_SupplierRfqId",
                        column: x => x.SupplierRfqId,
                        principalTable: "SupplierRfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_SupplierRfqLines_SupplierRfqId", table: "SupplierRfqLines", column: "SupplierRfqId");
            migrationBuilder.CreateIndex(name: "IX_SupplierRfqs_SupplierId", table: "SupplierRfqs", column: "SupplierId");
            migrationBuilder.CreateIndex(name: "IX_SupplierRfqs_IsDeleted", table: "SupplierRfqs", column: "IsDeleted");
            migrationBuilder.CreateIndex(
                name: "IX_SupplierRfqs_RfqNumber_CompanyId",
                table: "SupplierRfqs",
                columns: new[] { "RfqNumber", "CompanyId" },
                unique: true);

            // RG-BRF1–5 : retour fournisseur (BRF-).
            migrationBuilder.CreateTable(
                name: "SupplierReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReturnNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalHT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    StockApplied = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_ErpReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "ErpReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SupplierReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SupplierReturnId = table.Column<int>(type: "int", nullable: false),
                    ProductKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalHT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    QualityStatus = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_SupplierReturnLines_SupplierReturnId", table: "SupplierReturnLines", column: "SupplierReturnId");
            migrationBuilder.CreateIndex(name: "IX_SupplierReturns_SupplierId", table: "SupplierReturns", column: "SupplierId");
            migrationBuilder.CreateIndex(name: "IX_SupplierReturns_PurchaseOrderId", table: "SupplierReturns", column: "PurchaseOrderId");
            migrationBuilder.CreateIndex(name: "IX_SupplierReturns_ReceiptId", table: "SupplierReturns", column: "ReceiptId");
            migrationBuilder.CreateIndex(name: "IX_SupplierReturns_SupplierInvoiceId", table: "SupplierReturns", column: "SupplierInvoiceId");
            migrationBuilder.CreateIndex(name: "IX_SupplierReturns_IsDeleted", table: "SupplierReturns", column: "IsDeleted");
            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_ReturnNumber_CompanyId",
                table: "SupplierReturns",
                columns: new[] { "ReturnNumber", "CompanyId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupplierReturnLines");
            migrationBuilder.DropTable(name: "SupplierReturns");

            migrationBuilder.DropTable(name: "SupplierRfqLines");
            migrationBuilder.DropTable(name: "SupplierRfqs");

            migrationBuilder.DropTable(name: "DepositInvoices");

            migrationBuilder.DropTable(name: "ProformaLines");
            migrationBuilder.DropTable(name: "Proformas");

            migrationBuilder.DropColumn(name: "Version", table: "Quotes");
        }
    }
}
