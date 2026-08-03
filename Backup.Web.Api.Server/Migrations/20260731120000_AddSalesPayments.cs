using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260731120000_AddSalesPayments")]
    public partial class AddSalesPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RoundingDifference = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Method = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Bank = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    TerminalTransactionId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId_SalesInvoiceId",
                table: "Payments",
                columns: new[] { "CompanyId", "SalesInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SalesInvoiceId",
                table: "Payments",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments",
                column: "PaidAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Payments");
        }
    }
}
