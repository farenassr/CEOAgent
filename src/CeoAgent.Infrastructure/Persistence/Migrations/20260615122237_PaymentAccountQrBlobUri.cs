using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEOAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAccountQrBlobUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "qr_blob_uri",
                schema: "public",
                table: "company_payment_account",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "qr_blob_uri",
                schema: "public",
                table: "company_payment_account");
        }
    }
}
