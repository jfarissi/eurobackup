using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260808120000_AddSupplierInvoiceReceiptAndPayments")]
    public partial class AddSupplierInvoiceReceiptAndPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'SupplierInvoices'
    AND COLUMN_NAME = 'ReceiptId'
);
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `SupplierInvoices` ADD COLUMN `ReceiptId` int NULL, ADD INDEX `IX_SupplierInvoices_ReceiptId` (`ReceiptId`)',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `SupplierPayments` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `SupplierInvoiceId` int NOT NULL,
  `Amount` decimal(18,4) NOT NULL,
  `PaidAt` datetime(6) NOT NULL,
  `Method` varchar(64) CHARACTER SET utf8mb4 NULL,
  `Reference` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `UpdatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_SupplierPayments_SupplierInvoiceId` (`SupplierInvoiceId`),
  KEY `IX_SupplierPayments_CompanyId` (`CompanyId`),
  CONSTRAINT `FK_SupplierPayments_SupplierInvoices_SupplierInvoiceId`
    FOREIGN KEY (`SupplierInvoiceId`) REFERENCES `SupplierInvoices` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `SupplierPayments`;");
            migrationBuilder.Sql("""
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'SupplierInvoices'
    AND COLUMN_NAME = 'ReceiptId'
);
SET @sql := IF(@col_exists > 0,
  'ALTER TABLE `SupplierInvoices` DROP INDEX `IX_SupplierInvoices_ReceiptId`, DROP COLUMN `ReceiptId`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
""");
        }
    }
}
