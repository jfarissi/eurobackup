using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260813160000_AddErpRapidApiKTypeCategoryCache")]
    public partial class AddErpRapidApiKTypeCategoryCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpRapidApiKTypeCategoryCache` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `KType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `CategoriesJson` longtext CHARACTER SET utf8mb4 NOT NULL,
  `CategoryCount` int NOT NULL DEFAULT 0,
  `FetchedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpRapidApiKTypeCategoryCache_KType` (`KType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpRapidApiKTypeCategoryCache`;");
        }
    }
}
