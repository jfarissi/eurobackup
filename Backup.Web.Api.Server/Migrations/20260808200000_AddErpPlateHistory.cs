using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260808200000_AddErpPlateHistory")]
    public partial class AddErpPlateHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpPlateHistories` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `PlateNumber` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  `Country` varchar(8) CHARACTER SET utf8mb4 NULL,
  `Vin` varchar(32) CHARACTER SET utf8mb4 NULL,
  `Make` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Model` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Year` int NULL,
  `EngineCode` varchar(64) CHARACTER SET utf8mb4 NULL,
  `FuelType` varchar(64) CHARACTER SET utf8mb4 NULL,
  `PowerHP` int NULL,
  `ProductsFound` int NOT NULL DEFAULT 0,
  `SearchedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  `SearchedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpPlateHistories_CompanyId_SearchedAt` (`CompanyId`, `SearchedAt`),
  KEY `IX_ErpPlateHistories_PlateNumber` (`PlateNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpPlateHistories`;");
        }
    }
}
