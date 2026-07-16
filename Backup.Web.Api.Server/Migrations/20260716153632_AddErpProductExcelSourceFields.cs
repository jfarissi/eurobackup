using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddErpProductExcelSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "ErpProducts",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "FromExcel",
                table: "ErpProducts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceFile",
                table: "ErpProducts",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ErpProducts_FromExcel",
                table: "ErpProducts",
                column: "FromExcel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErpProducts_FromExcel",
                table: "ErpProducts");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "ErpProducts");

            migrationBuilder.DropColumn(
                name: "FromExcel",
                table: "ErpProducts");

            migrationBuilder.DropColumn(
                name: "SourceFile",
                table: "ErpProducts");
        }
    }
}
