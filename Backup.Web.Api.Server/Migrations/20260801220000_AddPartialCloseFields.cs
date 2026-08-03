using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801220000_AddPartialCloseFields")]
    public partial class AddPartialCloseFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RG-CF6 : cohérence qté commande/réception/facture côté achat.
            migrationBuilder.AddColumn<decimal>(
                name: "InvoicedQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // RG-DV3 : conversion partielle devis → commande(s).
            migrationBuilder.AddColumn<decimal>(
                name: "ConvertedQuantity",
                table: "QuoteLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // RG-CO3 : fin de l'exercice comptable ouvert.
            migrationBuilder.AddColumn<System.DateTime>(
                name: "OpenFiscalPeriodEnd",
                table: "Companies",
                type: "datetime(6)",
                nullable: true);

            // RG-S3 : durée de rétention (mois) avant archivage auto.
            migrationBuilder.AddColumn<int>(
                name: "RetentionMonths",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 24);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "InvoicedQuantity", table: "PurchaseOrderLines");
            migrationBuilder.DropColumn(name: "ConvertedQuantity", table: "QuoteLines");
            migrationBuilder.DropColumn(name: "OpenFiscalPeriodEnd", table: "Companies");
            migrationBuilder.DropColumn(name: "RetentionMonths", table: "Companies");
        }
    }
}
