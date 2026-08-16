using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260814140000_AddGaragePortal")]
    public partial class AddGaragePortal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE `AspNetUsers`
  ADD COLUMN `CustomerId` int NULL;
""");
            migrationBuilder.Sql("""
CREATE INDEX `IX_AspNetUsers_CustomerId` ON `AspNetUsers` (`CustomerId`);
""");
            migrationBuilder.Sql("""
ALTER TABLE `ErpPlateVehicles`
  ADD COLUMN `CustomerId` int NULL;
""");
            migrationBuilder.Sql("""
CREATE INDEX `IX_ErpPlateVehicles_CustomerId` ON `ErpPlateVehicles` (`CustomerId`);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX `IX_AspNetUsers_CustomerId` ON `AspNetUsers`;");
            migrationBuilder.Sql("ALTER TABLE `AspNetUsers` DROP COLUMN `CustomerId`;");
            migrationBuilder.Sql("DROP INDEX `IX_ErpPlateVehicles_CustomerId` ON `ErpPlateVehicles`;");
            migrationBuilder.Sql("ALTER TABLE `ErpPlateVehicles` DROP COLUMN `CustomerId`;");
        }
    }
}
