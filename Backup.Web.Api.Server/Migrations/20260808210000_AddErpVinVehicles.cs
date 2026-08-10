using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260808210000_AddErpVinVehicles")]
    public partial class AddErpVinVehicles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpVinVehicles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `Vin` varchar(17) CHARACTER SET utf8mb4 NOT NULL,
  `Make` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Model` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Year` int NULL,
  `EngineCode` varchar(64) CHARACTER SET utf8mb4 NULL,
  `FuelType` varchar(64) CHARACTER SET utf8mb4 NULL,
  `PowerHP` int NULL,
  `ExternalVehicleId` varchar(64) CHARACTER SET utf8mb4 NULL,
  `ExternalModelId` varchar(64) CHARACTER SET utf8mb4 NULL,
  `ExternalManufacturerId` varchar(64) CHARACTER SET utf8mb4 NULL,
  `Source` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'RapidApi',
  `RawJson` longtext CHARACTER SET utf8mb4 NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `HitCount` int NOT NULL DEFAULT 0,
  `LastHitAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpVinVehicles_Vin` (`Vin`),
  KEY `IX_ErpVinVehicles_Make_Model_Year` (`Make`, `Model`, `Year`),
  KEY `IX_ErpVinVehicles_CompanyId` (`CompanyId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpVinVehicles`;");
        }
    }
}
