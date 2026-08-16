using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260811150000_AddErpProductVehicleFacetIndexes")]
    public partial class AddErpProductVehicleFacetIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddIndexIfMissing(migrationBuilder, "IX_ErpProductVehicles_EngineCode", "EngineCode");
            AddIndexIfMissing(migrationBuilder, "IX_ErpProductVehicles_FuelType", "FuelType");
            AddIndexIfMissing(migrationBuilder, "IX_ErpProductVehicles_BodyType", "BodyType");
            AddIndexIfMissing(migrationBuilder, "IX_ErpProductVehicles_DriveType", "DriveType");
            AddIndexIfMissing(migrationBuilder, "IX_ErpProductVehicles_Transmission", "Transmission");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "IX_ErpProductVehicles_EngineCode");
            DropIndexIfExists(migrationBuilder, "IX_ErpProductVehicles_FuelType");
            DropIndexIfExists(migrationBuilder, "IX_ErpProductVehicles_BodyType");
            DropIndexIfExists(migrationBuilder, "IX_ErpProductVehicles_DriveType");
            DropIndexIfExists(migrationBuilder, "IX_ErpProductVehicles_Transmission");
        }

        private static void AddIndexIfMissing(MigrationBuilder migrationBuilder, string indexName, string columnName)
        {
            migrationBuilder.Sql($"""
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'ErpProductVehicles'
    AND INDEX_NAME = '{indexName}'
);
SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE `ErpProductVehicles` ADD INDEX `{indexName}` (`{columnName}`)',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
""");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string indexName)
        {
            migrationBuilder.Sql($"""
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'ErpProductVehicles'
    AND INDEX_NAME = '{indexName}'
);
SET @sql := IF(@idx_exists > 0,
  'ALTER TABLE `ErpProductVehicles` DROP INDEX `{indexName}`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
""");
        }
    }
}
