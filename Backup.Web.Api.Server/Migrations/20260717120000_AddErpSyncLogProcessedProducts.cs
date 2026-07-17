using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(StorageBroker))]
    [Migration("20260717120000_AddErpSyncLogProcessedProducts")]
    public partial class AddErpSyncLogProcessedProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessedProducts",
                table: "ErpSyncLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessedProducts",
                table: "ErpSyncLogs");
        }
    }
}
