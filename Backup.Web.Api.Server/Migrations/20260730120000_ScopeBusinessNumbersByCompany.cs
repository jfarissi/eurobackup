using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    /// <summary>
    /// Unicité des numéros / codes métier par société (évite duplicate key cross-company).
    /// Inclut Quotes, SalesOrders, SalesInvoices, etc. + Stock + Documents.
    /// </summary>
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260730120000_ScopeBusinessNumbersByCompany")]
    public partial class ScopeBusinessNumbersByCompany : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop anciens index uniques globaux (tolérant si déjà absents)
            DropIndexIfExists(migrationBuilder, "Quotes", "IX_Quotes_QuoteNumber");
            DropIndexIfExists(migrationBuilder, "SalesOrders", "IX_SalesOrders_OrderNumber");
            DropIndexIfExists(migrationBuilder, "SalesInvoices", "IX_SalesInvoices_InvoiceNumber");
            DropIndexIfExists(migrationBuilder, "CreditNotes", "IX_CreditNotes_CreditNoteNumber");
            DropIndexIfExists(migrationBuilder, "PurchaseOrders", "IX_PurchaseOrders_OrderNumber");
            DropIndexIfExists(migrationBuilder, "CashSessions", "IX_CashSessions_SessionNumber");
            DropIndexIfExists(migrationBuilder, "Customers", "IX_Customers_CustomerCode");
            DropIndexIfExists(migrationBuilder, "Suppliers", "IX_Suppliers_SupplierCode");
            DropIndexIfExists(migrationBuilder, "SalesDeliveryNotes", "IX_SalesDeliveryNotes_DeliveryNumber");
            DropIndexIfExists(migrationBuilder, "ErpReceipts", "IX_ErpReceipts_ReceiptNumber");
            DropIndexIfExists(migrationBuilder, "SupplierInvoices", "IX_SupplierInvoices_InvoiceNumber");
            DropIndexIfExists(migrationBuilder, "Stock", "IX_Stock_ProductKey");
            DropIndexIfExists(migrationBuilder, "Documents", "IX_Documents_CompanyId");

            // CompanyId indexable (varchar) pour les index composites
            AlterCompanyId(migrationBuilder, "Quotes");
            AlterCompanyId(migrationBuilder, "SalesOrders");
            AlterCompanyId(migrationBuilder, "SalesInvoices");
            AlterCompanyId(migrationBuilder, "CreditNotes");
            AlterCompanyId(migrationBuilder, "PurchaseOrders");
            AlterCompanyId(migrationBuilder, "CashSessions");
            AlterCompanyId(migrationBuilder, "Customers");
            AlterCompanyId(migrationBuilder, "Suppliers");
            AlterCompanyId(migrationBuilder, "SalesDeliveryNotes");
            AlterCompanyId(migrationBuilder, "ErpReceipts");
            AlterCompanyId(migrationBuilder, "SupplierInvoices");
            AlterCompanyId(migrationBuilder, "StockMovements");
            AlterCompanyId(migrationBuilder, "Stock");
            AlterCompanyId(migrationBuilder, "Documents");

            CreateUniqueIfMissing(migrationBuilder, "Quotes", "IX_Quotes_QuoteNumber_CompanyId", "QuoteNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "SalesOrders", "IX_SalesOrders_OrderNumber_CompanyId", "OrderNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "SalesInvoices", "IX_SalesInvoices_InvoiceNumber_CompanyId", "InvoiceNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "CreditNotes", "IX_CreditNotes_CreditNoteNumber_CompanyId", "CreditNoteNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "PurchaseOrders", "IX_PurchaseOrders_OrderNumber_CompanyId", "OrderNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "CashSessions", "IX_CashSessions_SessionNumber_CompanyId", "SessionNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "Customers", "IX_Customers_CustomerCode_CompanyId", "CustomerCode", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "Suppliers", "IX_Suppliers_SupplierCode_CompanyId", "SupplierCode", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "SalesDeliveryNotes", "IX_SalesDeliveryNotes_DeliveryNumber_CompanyId", "DeliveryNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "ErpReceipts", "IX_ErpReceipts_ReceiptNumber_CompanyId", "ReceiptNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "SupplierInvoices", "IX_SupplierInvoices_InvoiceNumber_CompanyId", "InvoiceNumber", "CompanyId");
            CreateUniqueIfMissing(migrationBuilder, "Stock", "IX_Stock_ProductKey_CompanyId", "ProductKey", "CompanyId");

            CreateIndexIfMissing(
                migrationBuilder,
                "Documents",
                "IX_Documents_TypeDocument_Numero_CompanyId",
                "TypeDocument", "Numero", "CompanyId",
                unique: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "Quotes", "IX_Quotes_QuoteNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "SalesOrders", "IX_SalesOrders_OrderNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "SalesInvoices", "IX_SalesInvoices_InvoiceNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "CreditNotes", "IX_CreditNotes_CreditNoteNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "PurchaseOrders", "IX_PurchaseOrders_OrderNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "CashSessions", "IX_CashSessions_SessionNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "Customers", "IX_Customers_CustomerCode_CompanyId");
            DropIndexIfExists(migrationBuilder, "Suppliers", "IX_Suppliers_SupplierCode_CompanyId");
            DropIndexIfExists(migrationBuilder, "SalesDeliveryNotes", "IX_SalesDeliveryNotes_DeliveryNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "ErpReceipts", "IX_ErpReceipts_ReceiptNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "SupplierInvoices", "IX_SupplierInvoices_InvoiceNumber_CompanyId");
            DropIndexIfExists(migrationBuilder, "Stock", "IX_Stock_ProductKey_CompanyId");
            DropIndexIfExists(migrationBuilder, "Documents", "IX_Documents_TypeDocument_Numero_CompanyId");

            migrationBuilder.CreateIndex(name: "IX_Quotes_QuoteNumber", table: "Quotes", column: "QuoteNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SalesOrders_OrderNumber", table: "SalesOrders", column: "OrderNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SalesInvoices_InvoiceNumber", table: "SalesInvoices", column: "InvoiceNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_CreditNotes_CreditNoteNumber", table: "CreditNotes", column: "CreditNoteNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_PurchaseOrders_OrderNumber", table: "PurchaseOrders", column: "OrderNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_CashSessions_SessionNumber", table: "CashSessions", column: "SessionNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Customers_CustomerCode", table: "Customers", column: "CustomerCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Suppliers_SupplierCode", table: "Suppliers", column: "SupplierCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SalesDeliveryNotes_DeliveryNumber", table: "SalesDeliveryNotes", column: "DeliveryNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_ErpReceipts_ReceiptNumber", table: "ErpReceipts", column: "ReceiptNumber");
            migrationBuilder.CreateIndex(name: "IX_SupplierInvoices_InvoiceNumber", table: "SupplierInvoices", column: "InvoiceNumber");
            migrationBuilder.CreateIndex(name: "IX_Stock_ProductKey", table: "Stock", column: "ProductKey", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Documents_CompanyId", table: "Documents", column: "CompanyId");
        }

        private static void AlterCompanyId(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql($@"
UPDATE `{table}`
SET `CompanyId` = LEFT(`CompanyId`, 36)
WHERE `CompanyId` IS NOT NULL AND CHAR_LENGTH(`CompanyId`) > 36;
");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyId",
                table: table,
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string indexName)
        {
            migrationBuilder.Sql($@"
SET @idx_exists := (
  SELECT COUNT(1)
  FROM information_schema.statistics
  WHERE table_schema = DATABASE()
    AND table_name = '{table}'
    AND index_name = '{indexName}'
);
SET @sql := IF(@idx_exists > 0,
  'ALTER TABLE `{table}` DROP INDEX `{indexName}`',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }

        private static void CreateUniqueIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string indexName,
            string column1,
            string column2)
        {
            migrationBuilder.Sql($@"
SET @idx_exists := (
  SELECT COUNT(1)
  FROM information_schema.statistics
  WHERE table_schema = DATABASE()
    AND table_name = '{table}'
    AND index_name = '{indexName}'
);
SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE `{table}` ADD UNIQUE INDEX `{indexName}` (`{column1}`, `{column2}`)',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }

        private static void CreateIndexIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string indexName,
            string column1,
            string column2,
            string column3,
            bool unique)
        {
            var uniqueKw = unique ? "UNIQUE " : "";
            migrationBuilder.Sql($@"
SET @idx_exists := (
  SELECT COUNT(1)
  FROM information_schema.statistics
  WHERE table_schema = DATABASE()
    AND table_name = '{table}'
    AND index_name = '{indexName}'
);
SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE `{table}` ADD {uniqueKw}INDEX `{indexName}` (`{column1}`, `{column2}`, `{column3}`)',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }
    }
}
