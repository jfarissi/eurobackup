using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Web.Api.Server.Migrations
{
    [DbContext(typeof(Backup.Web.Api.Server.Brokers.Storage.StorageBroker))]
    [Migration("20260801190000_AddCustomerPaymentTerms")]
    public partial class AddCustomerPaymentTerms : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Customers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentTerms", table: "Customers");
        }
    }
}
