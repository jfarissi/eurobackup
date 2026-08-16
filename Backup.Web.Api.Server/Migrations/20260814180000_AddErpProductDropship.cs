using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260814180000_AddErpProductDropship")]
    public partial class AddErpProductDropship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE `ErpProducts`
  ADD COLUMN `IsDropship` tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN `DropshipSupplierId` int NULL;
""");
            migrationBuilder.Sql("""
CREATE INDEX `IX_ErpProducts_IsDropship` ON `ErpProducts` (`IsDropship`);
""");
            migrationBuilder.Sql("""
CREATE INDEX `IX_ErpProducts_DropshipSupplierId` ON `ErpProducts` (`DropshipSupplierId`);
""");
            migrationBuilder.Sql("""
ALTER TABLE `PurchaseOrders`
  ADD COLUMN `SalesOrderId` int NULL;
""");
            migrationBuilder.Sql("""
CREATE INDEX `IX_PurchaseOrders_SalesOrderId` ON `PurchaseOrders` (`SalesOrderId`);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX `IX_PurchaseOrders_SalesOrderId` ON `PurchaseOrders`;");
            migrationBuilder.Sql("ALTER TABLE `PurchaseOrders` DROP COLUMN `SalesOrderId`;");
            migrationBuilder.Sql("DROP INDEX `IX_ErpProducts_DropshipSupplierId` ON `ErpProducts`;");
            migrationBuilder.Sql("DROP INDEX `IX_ErpProducts_IsDropship` ON `ErpProducts`;");
            migrationBuilder.Sql("ALTER TABLE `ErpProducts` DROP COLUMN `DropshipSupplierId`, DROP COLUMN `IsDropship`;");
        }
    }
}
