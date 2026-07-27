using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260725143000_AddStoreChatTurnCorrection")]
    public partial class AddStoreChatTurnCorrection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewSource",
                table: "StoreChatTurns",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrected",
                table: "StoreChatTurns",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectedAt",
                table: "StoreChatTurns",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreChatTurns_IsCorrected",
                table: "StoreChatTurns",
                column: "IsCorrected");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_StoreChatTurns_IsCorrected", table: "StoreChatTurns");
            migrationBuilder.DropColumn(name: "ReviewSource", table: "StoreChatTurns");
            migrationBuilder.DropColumn(name: "IsCorrected", table: "StoreChatTurns");
            migrationBuilder.DropColumn(name: "CorrectedAt", table: "StoreChatTurns");
        }
    }
}
