using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260805120000_AddEmailSystem")]
    public partial class AddEmailSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `CompanyEmailSettings` (
    `CompanyId` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
    `Enabled` tinyint(1) NOT NULL DEFAULT 0,
    `SmtpHost` varchar(255) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
    `SmtpPort` int NOT NULL DEFAULT 587,
    `UseSsl` tinyint(1) NOT NULL DEFAULT 1,
    `Username` varchar(255) CHARACTER SET utf8mb4 NULL,
    `Password` varchar(512) CHARACTER SET utf8mb4 NULL,
    `FromEmail` varchar(255) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
    `FromDisplayName` varchar(255) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
    `DefaultReplyTo` varchar(255) CHARACTER SET utf8mb4 NULL,
    `MaxEmailsPerHour` int NOT NULL DEFAULT 500,
    `MaxAttachmentBytes` int NOT NULL DEFAULT 10485760,
    `FooterHtml` longtext CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    `UpdatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
    PRIMARY KEY (`CompanyId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `EmailMessages` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `CompanyId` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
    `TrackingId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `TemplateCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `DocumentType` varchar(64) CHARACTER SET utf8mb4 NULL,
    `DocumentId` int NULL,
    `DocumentNumber` varchar(128) CHARACTER SET utf8mb4 NULL,
    `ToEmail` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CcEmails` varchar(1024) CHARACTER SET utf8mb4 NULL,
    `ReplyTo` varchar(255) CHARACTER SET utf8mb4 NULL,
    `Subject` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `BodyHtml` longtext CHARACTER SET utf8mb4 NOT NULL,
    `BodyText` longtext CHARACTER SET utf8mb4 NULL,
    `AttachmentFileName` varchar(255) CHARACTER SET utf8mb4 NULL,
    `AttachmentBytes` longblob NULL,
    `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `ScheduledAt` datetime(6) NULL,
    `SentAt` datetime(6) NULL,
    `RetryCount` int NOT NULL DEFAULT 0,
    `LastError` varchar(500) CHARACTER SET utf8mb4 NULL,
    `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_EmailMessages_CompanyId_CreatedAt` (`CompanyId`, `CreatedAt`),
    INDEX `IX_EmailMessages_Status_ScheduledAt` (`Status`, `ScheduledAt`),
    INDEX `IX_EmailMessages_Document` (`DocumentType`, `DocumentId`)
) CHARACTER SET=utf8mb4;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `EmailMessages`; DROP TABLE IF EXISTS `CompanyEmailSettings`;");
        }
    }
}
