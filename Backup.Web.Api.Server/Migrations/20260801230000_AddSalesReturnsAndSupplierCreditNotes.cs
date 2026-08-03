using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>RG-BR1–5 (retour client BRC) + RG-AC4 (lien avoir↔retour) + RG-AF1–5 (avoir fournisseur).</summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801230000_AddSalesReturnsAndSupplierCreditNotes")]
    public partial class AddSalesReturnsAndSupplierCreditNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RG-BR1–5 : bon de retour client (BRC).
            migrationBuilder.CreateTable(
                name: "SalesReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReturnNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SalesDeliveryNoteId = table.Column<int>(type: "int", nullable: false),
                    SalesOrderId = table.Column<int>(type: "int", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QualityStatus = table.Column<string>(type: "longtext", nullable: true)
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
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ArchivedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    StockApplied = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesReturns_SalesDeliveryNotes_SalesDeliveryNoteId",
                        column: x => x.SalesDeliveryNoteId,
                        principalTable: "SalesDeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalesReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SalesReturnId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SalesReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesReturnLines_SalesReturns_SalesReturnId",
                        column: x => x.SalesReturnId,
                        principalTable: "SalesReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_SalesReturnLines_SalesReturnId", table: "SalesReturnLines", column: "SalesReturnId");
            migrationBuilder.CreateIndex(name: "IX_SalesReturns_CustomerId", table: "SalesReturns", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_SalesReturns_SalesDeliveryNoteId", table: "SalesReturns", column: "SalesDeliveryNoteId");
            migrationBuilder.CreateIndex(name: "IX_SalesReturns_SalesOrderId", table: "SalesReturns", column: "SalesOrderId");
            migrationBuilder.CreateIndex(name: "IX_SalesReturns_IsDeleted", table: "SalesReturns", column: "IsDeleted");
            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ReturnNumber_CompanyId",
                table: "SalesReturns",
                columns: new[] { "ReturnNumber", "CompanyId" },
                unique: true);

            // RG-AC4 : lien avoir client ↔ retour d'origine.
            migrationBuilder.AddColumn<int>(
                name: "SalesReturnId",
                table: "CreditNotes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(name: "IX_CreditNotes_SalesReturnId", table: "CreditNotes", column: "SalesReturnId");
            migrationBuilder.AddForeignKey(
                name: "FK_CreditNotes_SalesReturns_SalesReturnId",
                table: "CreditNotes",
                column: "SalesReturnId",
                principalTable: "SalesReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // RG-AF1–5 : avoir fournisseur (AF).
            migrationBuilder.CreateTable(
                name: "SupplierCreditNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreditNoteNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: false),
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SupplierCreditNoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SupplierCreditNoteEntityId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SupplierCreditNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNoteLines_SupplierCreditNotes_SupplierCreditNoteEntityId",
                        column: x => x.SupplierCreditNoteEntityId,
                        principalTable: "SupplierCreditNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_SupplierCreditNoteLines_SupplierCreditNoteEntityId", table: "SupplierCreditNoteLines", column: "SupplierCreditNoteEntityId");
            migrationBuilder.CreateIndex(name: "IX_SupplierCreditNotes_SupplierId", table: "SupplierCreditNotes", column: "SupplierId");
            migrationBuilder.CreateIndex(name: "IX_SupplierCreditNotes_SupplierInvoiceId", table: "SupplierCreditNotes", column: "SupplierInvoiceId");
            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_CreditNoteNumber_CompanyId",
                table: "SupplierCreditNotes",
                columns: new[] { "CreditNoteNumber", "CompanyId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupplierCreditNoteLines");
            migrationBuilder.DropTable(name: "SupplierCreditNotes");

            migrationBuilder.DropForeignKey(name: "FK_CreditNotes_SalesReturns_SalesReturnId", table: "CreditNotes");
            migrationBuilder.DropIndex(name: "IX_CreditNotes_SalesReturnId", table: "CreditNotes");
            migrationBuilder.DropColumn(name: "SalesReturnId", table: "CreditNotes");

            migrationBuilder.DropTable(name: "SalesReturnLines");
            migrationBuilder.DropTable(name: "SalesReturns");
        }
    }
}
