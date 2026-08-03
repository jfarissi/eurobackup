using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801170000_AddDocumentAuditAndSoftDelete")]
    public partial class AddDocumentAuditAndSoftDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "longtext", nullable: true),
                    Actor = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_DocumentType_DocumentId_CreatedAt",
                table: "DocumentAuditLogs",
                columns: new[] { "DocumentType", "DocumentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_CompanyId",
                table: "DocumentAuditLogs",
                column: "CompanyId");

            AddSoftDelete(migrationBuilder, "SalesOrders");
            AddSoftDelete(migrationBuilder, "SalesDeliveryNotes");
            AddSoftDelete(migrationBuilder, "SalesInvoices");
            AddSoftDelete(migrationBuilder, "Quotes");
            AddSoftDelete(migrationBuilder, "CreditNotes");
        }

        private static void AddSoftDelete(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: table,
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: table,
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: table,
                type: "varchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: $"IX_{table}_IsDeleted",
                table: table,
                column: "IsDeleted");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropSoftDelete(migrationBuilder, "SalesOrders");
            DropSoftDelete(migrationBuilder, "SalesDeliveryNotes");
            DropSoftDelete(migrationBuilder, "SalesInvoices");
            DropSoftDelete(migrationBuilder, "Quotes");
            DropSoftDelete(migrationBuilder, "CreditNotes");
            migrationBuilder.DropTable(name: "DocumentAuditLogs");
        }

        private static void DropSoftDelete(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropIndex(name: $"IX_{table}_IsDeleted", table: table);
            migrationBuilder.DropColumn(name: "DeletedBy", table: table);
            migrationBuilder.DropColumn(name: "DeletedAt", table: table);
            migrationBuilder.DropColumn(name: "IsDeleted", table: table);
        }
    }
}
