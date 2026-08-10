using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260808220000_EnrichErpProductVehicles")]
    public partial class EnrichErpProductVehicles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Colonnes déjà présentes (PowerKW/HP/Ccm) : on ajoute le reste.
            // MySQL : ADD COLUMN échoue si déjà présent — migration one-shot.
            migrationBuilder.Sql("""
ALTER TABLE `ErpProductVehicles`
  ADD COLUMN `TypeName` varchar(256) NULL,
  ADD COLUMN `ExternalManufacturerId` varchar(64) NULL,
  ADD COLUMN `ExternalModelId` varchar(64) NULL,
  ADD COLUMN `DriveType` varchar(64) NULL,
  ADD COLUMN `Transmission` varchar(64) NULL,
  ADD COLUMN `Cylinders` int NULL,
  ADD COLUMN `Valves` int NULL,
  ADD COLUMN `RawJson` longtext NULL;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE `ErpProductVehicles`
  DROP COLUMN `TypeName`,
  DROP COLUMN `ExternalManufacturerId`,
  DROP COLUMN `ExternalModelId`,
  DROP COLUMN `DriveType`,
  DROP COLUMN `Transmission`,
  DROP COLUMN `Cylinders`,
  DROP COLUMN `Valves`,
  DROP COLUMN `RawJson`;
""");
        }
    }
}
