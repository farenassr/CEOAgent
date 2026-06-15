using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_payment_account",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    account_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    account_holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reservation_payment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    qr_blob_container = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    qr_blob_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_payment_account", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_payment_account_bank_bank_id",
                        column: x => x.bank_id,
                        principalSchema: "public",
                        principalTable: "bank",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_payment_account_company_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "public",
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_country_code_name",
                schema: "public",
                table: "bank",
                columns: new[] { "country_code", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_is_active",
                schema: "public",
                table: "bank",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_company_payment_account_bank_id",
                schema: "public",
                table: "company_payment_account",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_payment_account_organization_id_created_at",
                schema: "public",
                table: "company_payment_account",
                columns: new[] { "organization_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_company_payment_account_organization_id_currency",
                schema: "public",
                table: "company_payment_account",
                columns: new[] { "organization_id", "currency" },
                unique: true,
                filter: "is_default AND is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_payment_account",
                schema: "public");

            migrationBuilder.DropTable(
                name: "bank",
                schema: "public");
        }
    }
}
