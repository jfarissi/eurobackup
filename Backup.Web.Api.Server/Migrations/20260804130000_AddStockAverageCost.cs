using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>CMUP : Stock.AverageCost + StockMovements.UnitCost/StockValue.</summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260804130000_AddStockAverageCost")]
    public partial class AddStockAverageCost : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @db := DATABASE();
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Stock' AND COLUMN_NAME = 'AverageCost');
SET @sql := IF(@exists = 0, 'ALTER TABLE `Stock` ADD COLUMN `AverageCost` decimal(18,4) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'StockMovements' AND COLUMN_NAME = 'UnitCost');
SET @sql := IF(@exists = 0, 'ALTER TABLE `StockMovements` ADD COLUMN `UnitCost` decimal(18,4) NULL', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'StockMovements' AND COLUMN_NAME = 'StockValue');
SET @sql := IF(@exists = 0, 'ALTER TABLE `StockMovements` ADD COLUMN `StockValue` decimal(18,4) NULL', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE `Stock` DROP COLUMN IF EXISTS `AverageCost`;
ALTER TABLE `StockMovements` DROP COLUMN IF EXISTS `UnitCost`;
ALTER TABLE `StockMovements` DROP COLUMN IF EXISTS `StockValue`;
");
        }
    }
}
