using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260722180000_AddSalesP3Extensions")]
    public partial class AddSalesP3Extensions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "SalesProjects",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "SalesProjects",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlanningText",
                table: "SalesProjects",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "SalesProjectId",
                table: "StoreChatQuotes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SalesProjectId",
                table: "StoreChatOrders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "SalesCustomerProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferredBrandsJson = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AverageBudget = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerProfiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerProfiles_CustomerId",
                table: "SalesCustomerProfiles",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesProjects_CustomerId",
                table: "SalesProjects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreChatQuotes_SalesProjectId",
                table: "StoreChatQuotes",
                column: "SalesProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreChatOrders_SalesProjectId",
                table: "StoreChatOrders",
                column: "SalesProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SalesCustomerProfiles");
            migrationBuilder.DropIndex(name: "IX_StoreChatOrders_SalesProjectId", table: "StoreChatOrders");
            migrationBuilder.DropIndex(name: "IX_StoreChatQuotes_SalesProjectId", table: "StoreChatQuotes");
            migrationBuilder.DropIndex(name: "IX_SalesProjects_CustomerId", table: "SalesProjects");
            migrationBuilder.DropColumn(name: "Style", table: "SalesProjects");
            migrationBuilder.DropColumn(name: "CustomerId", table: "SalesProjects");
            migrationBuilder.DropColumn(name: "PlanningText", table: "SalesProjects");
            migrationBuilder.DropColumn(name: "SalesProjectId", table: "StoreChatQuotes");
            migrationBuilder.DropColumn(name: "SalesProjectId", table: "StoreChatOrders");
        }
    }
}
