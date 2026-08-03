using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801210000_AddCompanyStockAndFiscalFlags")]
    public partial class AddCompanyStockAndFiscalFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenFiscalPeriodStart",
                table: "Companies",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AllowNegativeStock", table: "Companies");
            migrationBuilder.DropColumn(name: "OpenFiscalPeriodStart", table: "Companies");
        }
    }
}
