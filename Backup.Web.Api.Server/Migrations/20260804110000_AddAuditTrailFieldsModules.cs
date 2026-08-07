using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>
    /// Traçabilité CreatedAt/UpdatedAt/CreatedBy/UpdatedBy pour parsing, caisse, compta,
    /// numérotation, associations, stock, marques/catégories, aide, tenancy, paiements, lettrage, changements.
    /// Colonnes ajoutées de façon idempotente (IGNORE si déjà présentes).
    /// </summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260804110000_AddAuditTrailFieldsModules")]
    public partial class AddAuditTrailFieldsModules : Migration
    {
        private const string DefaultAtSql = "''2026-01-01 00:00:00.000000''";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Parsing / upload / association ──────────────────────────────
            // Documents.CreatedAt peut déjà exister (modèle historique / DateAdded).
            AddAllFour(migrationBuilder, "Documents");
            migrationBuilder.Sql("UPDATE `Documents` SET `CreatedAt` = `DateAdded` WHERE `DateAdded` IS NOT NULL;");
            migrationBuilder.Sql("UPDATE `Documents` SET `UpdatedAt` = `CreatedAt`;");

            AddAllFour(migrationBuilder, "DocumentLines");

            AddUpdatedAt(migrationBuilder, "DocumentRelations");
            AddActors(migrationBuilder, "DocumentRelations");
            migrationBuilder.Sql("UPDATE `DocumentRelations` SET `UpdatedAt` = `CreatedAt`;");

            AddUpdatedAt(migrationBuilder, "DeliveryLineAdjustments");
            AddUpdatedByOnly(migrationBuilder, "DeliveryLineAdjustments");
            migrationBuilder.Sql("UPDATE `DeliveryLineAdjustments` SET `UpdatedAt` = `CreatedAt`;");

            // ── Caisse ─────────────────────────────────────────────────────
            AddAllFour(migrationBuilder, "CashSessions");
            migrationBuilder.Sql("UPDATE `CashSessions` SET `CreatedAt` = `OpenedAt`, `UpdatedAt` = COALESCE(`ClosedAt`, `OpenedAt`), `CreatedBy` = `OpenedBy` WHERE `OpenedAt` IS NOT NULL;");

            AddUpdatedAt(migrationBuilder, "CashOperations");
            AddUpdatedByOnly(migrationBuilder, "CashOperations");
            migrationBuilder.Sql("UPDATE `CashOperations` SET `UpdatedAt` = `CreatedAt`;");

            // ── Comptabilité ───────────────────────────────────────────────
            AddUpdatedAt(migrationBuilder, "AccountingEntries");
            AddUpdatedByOnly(migrationBuilder, "AccountingEntries");
            migrationBuilder.Sql("UPDATE `AccountingEntries` SET `UpdatedAt` = `CreatedAt`;");

            AddAllFour(migrationBuilder, "AccountingEntryLines");

            // ── Numérotation ───────────────────────────────────────────────
            AddAllFour(migrationBuilder, "DocumentNumberSequences");

            // ── Stock ──────────────────────────────────────────────────────
            AddAllFour(migrationBuilder, "Stock");
            migrationBuilder.Sql("UPDATE `Stock` SET `CreatedAt` = `LastUpdated`, `UpdatedAt` = `LastUpdated` WHERE `LastUpdated` IS NOT NULL;");

            AddUpdatedAt(migrationBuilder, "StockMovements");
            AddUpdatedByOnly(migrationBuilder, "StockMovements");
            migrationBuilder.Sql("UPDATE `StockMovements` SET `UpdatedAt` = `CreatedAt`;");

            AddCreatedAt(migrationBuilder, "StockUpdates");
            AddActors(migrationBuilder, "StockUpdates");
            migrationBuilder.Sql("UPDATE `StockUpdates` SET `CreatedAt` = `UpdatedAt`;");

            // ── Produits (marques / catégories / changements) ───────────────
            AddActors(migrationBuilder, "ErpBrands");
            AddActors(migrationBuilder, "ErpCategories");

            AddAllFour(migrationBuilder, "ErpProductChangeLogs");
            migrationBuilder.Sql("UPDATE `ErpProductChangeLogs` SET `CreatedAt` = `DetectedAt`, `UpdatedAt` = `DetectedAt` WHERE `DetectedAt` IS NOT NULL;");

            // ── Aide / administration ──────────────────────────────────────
            AddColumnIfMissing(migrationBuilder, "HelpContents", "CreatedBy", "varchar(128) NULL");

            AddUpdatedAt(migrationBuilder, "Companies");
            AddActors(migrationBuilder, "Companies");
            migrationBuilder.Sql("UPDATE `Companies` SET `UpdatedAt` = `CreatedAt`;");

            AddUpdatedAt(migrationBuilder, "Tenants");
            AddActors(migrationBuilder, "Tenants");
            migrationBuilder.Sql("UPDATE `Tenants` SET `UpdatedAt` = `CreatedAt`;");

            AddAllFour(migrationBuilder, "UserCompanies");

            // ── Paiements / lettrage / tarifs ───────────────────────────────
            AddUpdatedAt(migrationBuilder, "Payments");
            AddUpdatedByOnly(migrationBuilder, "Payments");
            migrationBuilder.Sql("UPDATE `Payments` SET `UpdatedAt` = `CreatedAt`;");

            AddUpdatedAt(migrationBuilder, "PaymentAllocations");
            AddActors(migrationBuilder, "PaymentAllocations");
            migrationBuilder.Sql("UPDATE `PaymentAllocations` SET `UpdatedAt` = `CreatedAt`;");

            AddUpdatedAt(migrationBuilder, "LetteringGroups");
            AddUpdatedByOnly(migrationBuilder, "LetteringGroups");
            migrationBuilder.Sql("UPDATE `LetteringGroups` SET `UpdatedAt` = `CreatedAt`;");

            AddAllFour(migrationBuilder, "LetteringLines");

            AddUpdatedAt(migrationBuilder, "CustomerPriceListItems");
            AddActors(migrationBuilder, "CustomerPriceListItems");
            migrationBuilder.Sql("UPDATE `CustomerPriceListItems` SET `UpdatedAt` = `CreatedAt`;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down volontairement no-op-safe : ne droppe que si présent (manuel si besoin).
        }

        private static void AddAllFour(MigrationBuilder mb, string table)
        {
            AddCreatedAt(mb, table);
            AddUpdatedAt(mb, table);
            AddActors(mb, table);
        }

        private static void AddCreatedAt(MigrationBuilder mb, string table) =>
            AddColumnIfMissing(mb, table, "CreatedAt", $"datetime(6) NOT NULL DEFAULT {DefaultAtSql}");

        private static void AddUpdatedAt(MigrationBuilder mb, string table) =>
            AddColumnIfMissing(mb, table, "UpdatedAt", $"datetime(6) NOT NULL DEFAULT {DefaultAtSql}");

        private static void AddActors(MigrationBuilder mb, string table)
        {
            AddColumnIfMissing(mb, table, "CreatedBy", "varchar(128) NULL");
            AddColumnIfMissing(mb, table, "UpdatedBy", "varchar(128) NULL");
        }

        private static void AddUpdatedByOnly(MigrationBuilder mb, string table) =>
            AddColumnIfMissing(mb, table, "UpdatedBy", "varchar(128) NULL");

        private static void AddColumnIfMissing(MigrationBuilder mb, string table, string column, string definition)
        {
            // Guillemets simples dans definition doivent être doublés (''...') car @sql est une chaîne SQL.
            mb.Sql($@"
SET @db := DATABASE();
SET @exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'
);
SET @sql := IF(@exists = 0,
  CONCAT('ALTER TABLE `{table}` ADD COLUMN `{column}` ', '{definition}'),
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }
    }
}
