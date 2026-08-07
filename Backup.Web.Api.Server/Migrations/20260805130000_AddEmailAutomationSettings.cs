using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260805130000_AddEmailAutomationSettings")]
    public partial class AddEmailAutomationSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: columns may already exist on some environments.
            migrationBuilder.Sql("""
                SET @db := DATABASE();
                SET @sql := (
                  SELECT IF(
                    (SELECT COUNT(*) FROM information_schema.COLUMNS
                     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'CompanyEmailSettings'
                       AND COLUMN_NAME = 'AutoPaymentRemindersEnabled') = 0,
                    'ALTER TABLE `CompanyEmailSettings`
                       ADD COLUMN `AutoPaymentRemindersEnabled` TINYINT(1) NOT NULL DEFAULT 0,
                       ADD COLUMN `PaymentReminderDaysN1` INT NOT NULL DEFAULT 5,
                       ADD COLUMN `PaymentReminderDaysN2` INT NOT NULL DEFAULT 15,
                       ADD COLUMN `PaymentReminderDaysN3` INT NOT NULL DEFAULT 30,
                       ADD COLUMN `AutoStockAlertsEnabled` TINYINT(1) NOT NULL DEFAULT 0,
                       ADD COLUMN `StockAlertRecipients` VARCHAR(1000) NULL,
                       ADD COLUMN `StockAlertCooldownHours` INT NOT NULL DEFAULT 24,
                       ADD COLUMN `AutoEmailOnPurchaseOrderSend` TINYINT(1) NOT NULL DEFAULT 1',
                    'SELECT 1'
                  )
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE `CompanyEmailSettings`
                  DROP COLUMN `AutoPaymentRemindersEnabled`,
                  DROP COLUMN `PaymentReminderDaysN1`,
                  DROP COLUMN `PaymentReminderDaysN2`,
                  DROP COLUMN `PaymentReminderDaysN3`,
                  DROP COLUMN `AutoStockAlertsEnabled`,
                  DROP COLUMN `StockAlertRecipients`,
                  DROP COLUMN `StockAlertCooldownHours`,
                  DROP COLUMN `AutoEmailOnPurchaseOrderSend`;
                """);
        }
    }
}
