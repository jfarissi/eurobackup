using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260805160000_AddErpProductVariants")]
    public partial class AddErpProductVariants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(System.IO.File.Exists("scripts/add-erp-product-variants.sql")
                ? System.IO.File.ReadAllText("scripts/add-erp-product-variants.sql")
                : """
CREATE TABLE IF NOT EXISTS `ErpProductVariants` (
  `Id` CHAR(36) NOT NULL,
  `ProductId` INT NOT NULL,
  `Sku` VARCHAR(100) NOT NULL,
  `Barcode` VARCHAR(64) NULL,
  `CostPrice` DECIMAL(18,4) NULL,
  `PriceOverride` DECIMAL(18,4) NULL,
  `StockQuantity` DECIMAL(18,4) NOT NULL DEFAULT 0,
  `AttributesJson` VARCHAR(8000) NOT NULL DEFAULT '{}',
  `Weight` DECIMAL(18,4) NULL,
  `Length` DECIMAL(18,4) NULL,
  `Width` DECIMAL(18,4) NULL,
  `Height` DECIMAL(18,4) NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `CreatedBy` VARCHAR(128) NULL,
  `UpdatedBy` VARCHAR(128) NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `UpdatedAt` DATETIME(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpProductVariants_Sku` (`Sku`),
  UNIQUE KEY `IX_ErpProductVariants_Barcode` (`Barcode`),
  KEY `IX_ErpProductVariants_ProductId` (`ProductId`),
  CONSTRAINT `FK_ErpProductVariants_ErpProducts` FOREIGN KEY (`ProductId`) REFERENCES `ErpProducts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `ErpProductImages` (
  `Id` CHAR(36) NOT NULL,
  `ProductId` INT NOT NULL,
  `Url` VARCHAR(1024) NOT NULL,
  `AltText` VARCHAR(255) NOT NULL DEFAULT '',
  `IsMain` TINYINT(1) NOT NULL DEFAULT 0,
  `SortOrder` INT NOT NULL DEFAULT 0,
  `CreatedBy` VARCHAR(128) NULL,
  `UpdatedBy` VARCHAR(128) NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `UpdatedAt` DATETIME(6) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpProductImages_ProductId` (`ProductId`),
  KEY `IX_ErpProductImages_ProductId_IsMain` (`ProductId`, `IsMain`),
  CONSTRAINT `FK_ErpProductImages_ErpProducts` FOREIGN KEY (`ProductId`) REFERENCES `ErpProducts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `ErpProductAttributeDefinitions` (
  `Id` CHAR(36) NOT NULL,
  `CompanyId` VARCHAR(36) NOT NULL,
  `Code` VARCHAR(64) NOT NULL,
  `Name` VARCHAR(128) NOT NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `CreatedBy` VARCHAR(128) NULL,
  `UpdatedBy` VARCHAR(128) NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `UpdatedAt` DATETIME(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpProductAttributeDefinitions_Company_Code` (`CompanyId`, `Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `ErpProductAttributeValues` (
  `Id` CHAR(36) NOT NULL,
  `ProductId` INT NOT NULL,
  `AttributeId` CHAR(36) NOT NULL,
  `Value` LONGTEXT NOT NULL,
  `CreatedBy` VARCHAR(128) NULL,
  `UpdatedBy` VARCHAR(128) NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `UpdatedAt` DATETIME(6) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpProductAttributeValues_ProductId` (`ProductId`),
  UNIQUE KEY `IX_ErpProductAttributeValues_Product_Attr` (`ProductId`, `AttributeId`),
  CONSTRAINT `FK_ErpProductAttributeValues_ErpProducts` FOREIGN KEY (`ProductId`) REFERENCES `ErpProducts` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ErpProductAttributeValues_Definitions` FOREIGN KEY (`AttributeId`) REFERENCES `ErpProductAttributeDefinitions` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS `ErpProductAttributeValues`;
                DROP TABLE IF EXISTS `ErpProductAttributeDefinitions`;
                DROP TABLE IF EXISTS `ErpProductImages`;
                DROP TABLE IF EXISTS `ErpProductVariants`;
                """);
        }
    }
}
