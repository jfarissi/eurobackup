using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260806160000_AddCompanyEnableErpCatalogSync")]
    public partial class AddCompanyEnableErpCatalogSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @col_exists := (
                  SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'Companies'
                    AND COLUMN_NAME = 'EnableErpCatalogSync'
                );
                SET @sql := IF(@col_exists = 0,
                  'ALTER TABLE `Companies` ADD COLUMN `EnableErpCatalogSync` tinyint(1) NOT NULL DEFAULT 0',
                  'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @col_exists := (
                  SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'Companies'
                    AND COLUMN_NAME = 'EnableErpCatalogSync'
                );
                SET @sql := IF(@col_exists = 1,
                  'ALTER TABLE `Companies` DROP COLUMN `EnableErpCatalogSync`',
                  'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
