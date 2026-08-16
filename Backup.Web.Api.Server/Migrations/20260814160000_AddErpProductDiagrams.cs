using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260814160000_AddErpProductDiagrams")]
    public partial class AddErpProductDiagrams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpProductDiagrams` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProductId` int NOT NULL,
  `Title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  `ImageUrl` varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
  `MediaKind` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  `Source` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpProductDiagrams_ProductId` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpDiagramHotspots` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `DiagramId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Label` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Shape` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  `CoordsJson` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `TargetProductId` int NOT NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_ErpDiagramHotspots_DiagramId` (`DiagramId`),
  KEY `IX_ErpDiagramHotspots_TargetProductId` (`TargetProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpDiagramHotspots`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpProductDiagrams`;");
        }
    }
}
