using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260807120000_AddCompanyModules")]
    public partial class AddCompanyModules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `CompanyModules` (
  `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
  `ModuleCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ModuleName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `ConfigJson` longtext CHARACTER SET utf8mb4 NULL,
  `ActivatedAt` datetime(6) NOT NULL,
  `ExpiresAt` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_CompanyModules_Company_Module` (`CompanyId`, `ModuleCode`),
  KEY `IX_CompanyModules_CompanyId` (`CompanyId`),
  KEY `IX_CompanyModules_ModuleCode` (`ModuleCode`),
  CONSTRAINT `FK_CompanyModules_Companies_CompanyId`
    FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `CompanyModules`;");
        }
    }
}
