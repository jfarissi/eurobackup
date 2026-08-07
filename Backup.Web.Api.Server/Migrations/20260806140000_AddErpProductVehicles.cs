using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260806140000_AddErpProductVehicles")]
    public partial class AddErpProductVehicles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(System.IO.File.Exists("scripts/add-erp-product-vehicles.sql")
                ? System.IO.File.ReadAllText("scripts/add-erp-product-vehicles.sql")
                : """
CREATE TABLE IF NOT EXISTS `ErpProductVehicles` (
  `Id` CHAR(36) NOT NULL,
  `ProductId` INT NOT NULL,
  `Make` VARCHAR(128) NOT NULL,
  `Model` VARCHAR(128) NOT NULL,
  `YearFrom` INT NULL,
  `YearTo` INT NULL,
  `EngineCode` VARCHAR(64) NULL,
  `KType` VARCHAR(64) NULL,
  `BodyType` VARCHAR(64) NULL,
  `FuelType` VARCHAR(64) NULL,
  `PowerKW` INT NULL,
  `PowerHP` INT NULL,
  `Ccm` INT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpProductVehicles_ProductId` (`ProductId`),
  KEY `IX_ErpProductVehicles_Make_Model` (`Make`, `Model`),
  KEY `IX_ErpProductVehicles_KType` (`KType`),
  KEY `IX_ErpProductVehicles_YearRange` (`YearFrom`, `YearTo`),
  CONSTRAINT `FK_ErpProductVehicles_ErpProducts`
    FOREIGN KEY (`ProductId`) REFERENCES `ErpProducts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `ErpOemCrossReferences` (
  `Id` CHAR(36) NOT NULL,
  `ProductId` INT NOT NULL,
  `OemNumber` VARCHAR(128) NOT NULL,
  `Brand` VARCHAR(128) NULL,
  `IsOriginal` TINYINT(1) NOT NULL DEFAULT 0,
  `CreatedAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_OemCross_Product_Oem` (`ProductId`, `OemNumber`),
  KEY `IX_OemCross_OemNumber` (`OemNumber`),
  CONSTRAINT `FK_OemCross_Products`
    FOREIGN KEY (`ProductId`) REFERENCES `ErpProducts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS `ErpOemCrossReferences`;
                DROP TABLE IF EXISTS `ErpProductVehicles`;
                """);
        }
    }
}
