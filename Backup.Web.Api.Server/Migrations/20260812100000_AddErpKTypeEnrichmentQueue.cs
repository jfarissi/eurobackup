using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260812100000_AddErpKTypeEnrichmentQueue")]
    public partial class AddErpKTypeEnrichmentQueue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpKTypeEnrichmentQueue` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `KType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Vin` varchar(32) CHARACTER SET utf8mb4 NULL,
  `Make` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Model` varchar(128) CHARACTER SET utf8mb4 NULL,
  `Year` int NULL,
  `EngineCode` varchar(64) CHARACTER SET utf8mb4 NULL,
  `Source` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(16) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
  `HitCount` int NOT NULL DEFAULT 1,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `LastRequestedAt` datetime(6) NULL,
  `SyncedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpKTypeEnrichmentQueue_KType` (`KType`),
  KEY `IX_ErpKTypeEnrichmentQueue_Status_Hits` (`Status`, `HitCount` DESC),
  KEY `IX_ErpKTypeEnrichmentQueue_CompanyId` (`CompanyId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpKTypeEnrichmentQueue`;");
        }
    }
}
