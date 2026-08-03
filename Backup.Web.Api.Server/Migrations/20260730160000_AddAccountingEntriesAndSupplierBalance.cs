using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260730160000_AddAccountingEntriesAndSupplierBalance")]
    public partial class AddAccountingEntriesAndSupplierBalance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Suppliers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AccountingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EntryNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    JournalType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ReferenceType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    CompanyId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEntryLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AccountingEntryId = table.Column<int>(type: "int", nullable: false),
                    AccountCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    AccountLabel = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEntryLines_AccountingEntries_AccountingEntryId",
                        column: x => x.AccountingEntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_EntryNumber_CompanyId",
                table: "AccountingEntries",
                columns: new[] { "EntryNumber", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_ReferenceType_ReferenceId_CompanyId",
                table: "AccountingEntries",
                columns: new[] { "ReferenceType", "ReferenceId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryLines_AccountingEntryId",
                table: "AccountingEntryLines",
                column: "AccountingEntryId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccountingEntryLines");
            migrationBuilder.DropTable(name: "AccountingEntries");
            migrationBuilder.DropColumn(name: "Balance", table: "Suppliers");
        }
    }
}
