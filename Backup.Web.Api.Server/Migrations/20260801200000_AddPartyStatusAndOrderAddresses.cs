using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801200000_AddPartyStatusAndOrderAddresses")]
    public partial class AddPartyStatusAndOrderAddresses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Customers",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "BillingAddress",
                table: "SalesOrders",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "SalesOrders",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true);

            // Alignement IsActive historique → Status Bloqué si inactif.
            migrationBuilder.Sql(
                "UPDATE Suppliers SET Status = 'Blocked' WHERE IsActive = 0 AND (Status IS NULL OR Status = 'Active');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BillingAddress", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "ShippingAddress", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "Status", table: "Suppliers");
            migrationBuilder.DropColumn(name: "Status", table: "Customers");
        }
    }
}
