using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260814120000_AddErpProductSupplierOffers")]
    public partial class AddErpProductSupplierOffers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE `Suppliers`
  ADD COLUMN `FeedCode` varchar(64) CHARACTER SET utf8mb4 NULL;
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS `ErpProductSupplierOffers` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NULL,
  `ProductId` int NOT NULL,
  `SupplierId` int NOT NULL,
  `SupplierSku` varchar(128) CHARACTER SET utf8mb4 NULL,
  `BuyPrice` decimal(18,4) NOT NULL,
  `StockQty` decimal(18,4) NOT NULL,
  `LeadDays` int NOT NULL DEFAULT 0,
  `Available` tinyint(1) NOT NULL DEFAULT 1,
  `Source` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  `QuotedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ErpProductSupplierOffers_Company_Product_Supplier` (`CompanyId`, `ProductId`, `SupplierId`),
  KEY `IX_ErpProductSupplierOffers_ProductId` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ErpProductSupplierOffers`;");
            migrationBuilder.Sql("ALTER TABLE `Suppliers` DROP COLUMN `FeedCode`;");
        }
    }
}
