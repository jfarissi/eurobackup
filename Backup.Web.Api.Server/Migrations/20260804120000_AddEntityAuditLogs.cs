using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>Journal CRUD (CreatedBy / UpdatedBy) — table EntityAuditLogs.</summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260804120000_AddEntityAuditLogs")]
    public partial class AddEntityAuditLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `EntityAuditLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `EntityType` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `EntityKey` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Action` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `Summary` varchar(500) CHARACTER SET utf8mb4 NULL,
    `Details` longtext CHARACTER SET utf8mb4 NULL,
    `Actor` varchar(128) CHARACTER SET utf8mb4 NULL,
    `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_EntityAuditLogs_CompanyId_CreatedAt` (`CompanyId`, `CreatedAt`),
    INDEX `IX_EntityAuditLogs_EntityType_EntityKey_CreatedAt` (`EntityType`, `EntityKey`, `CreatedAt`)
) CHARACTER SET=utf8mb4;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `EntityAuditLogs`;");
        }
    }
}
