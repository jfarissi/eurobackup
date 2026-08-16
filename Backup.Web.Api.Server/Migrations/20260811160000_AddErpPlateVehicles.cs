using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260811160000_AddErpPlateVehicles")]
    public partial class AddErpPlateVehicles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpPlateVehicles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `PlateNumber` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  `Country` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
  `Vin` varchar(32) CHARACTER SET utf8mb4 NULL,
  `KType` varchar(64) CHARACTER SET utf8mb4 NULL,
  `Make` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Model` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Year` int NULL,
  `EngineCode` varchar(64) CHARACTER SET utf8mb4 NULL,
  `FuelType` varchar(64) CHARACTER SET utf8mb4 NULL,
  `PowerHP` int NULL,
  `Source` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  `HitCount` int NOT NULL DEFAULT 0,
  `LastHitAt` datetime(6) NULL,
  `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  `UpdatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpPlateVehicles_Company_Plate_Country` (`CompanyId`, `PlateNumber`, `Country`),
  KEY `IX_ErpPlateVehicles_Vin` (`Vin`),
  KEY `IX_ErpPlateVehicles_KType` (`KType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpPlateVehicles`;");
        }
    }
}
