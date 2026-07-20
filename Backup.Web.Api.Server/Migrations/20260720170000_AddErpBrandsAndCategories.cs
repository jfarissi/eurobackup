using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260720170000_AddErpBrandsAndCategories")]
    public partial class AddErpBrandsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpBrands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogoUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WebsiteUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpBrands", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ErpExternalId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameNl = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameFr = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameEn = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SlugNl = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SlugFr = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SlugEn = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpCategories_ErpCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ErpCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BrandId",
                table: "ErpProducts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ErpProducts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpBrands_Name",
                table: "ErpBrands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpBrands_Slug",
                table: "ErpBrands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpCategories_Level_ErpExternalId",
                table: "ErpCategories",
                columns: new[] { "Level", "ErpExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpCategories_ParentId",
                table: "ErpCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCategories_SlugNl",
                table: "ErpCategories",
                column: "SlugNl");

            migrationBuilder.CreateIndex(
                name: "IX_ErpProducts_BrandId",
                table: "ErpProducts",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpProducts_CategoryId",
                table: "ErpProducts",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ErpProducts_ErpBrands_BrandId",
                table: "ErpProducts",
                column: "BrandId",
                principalTable: "ErpBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ErpProducts_ErpCategories_CategoryId",
                table: "ErpProducts",
                column: "CategoryId",
                principalTable: "ErpCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ErpProducts_ErpBrands_BrandId",
                table: "ErpProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_ErpProducts_ErpCategories_CategoryId",
                table: "ErpProducts");

            migrationBuilder.DropTable(name: "ErpBrands");
            migrationBuilder.DropTable(name: "ErpCategories");

            migrationBuilder.DropIndex(name: "IX_ErpProducts_BrandId", table: "ErpProducts");
            migrationBuilder.DropIndex(name: "IX_ErpProducts_CategoryId", table: "ErpProducts");

            migrationBuilder.DropColumn(name: "BrandId", table: "ErpProducts");
            migrationBuilder.DropColumn(name: "CategoryId", table: "ErpProducts");
        }
    }
}
