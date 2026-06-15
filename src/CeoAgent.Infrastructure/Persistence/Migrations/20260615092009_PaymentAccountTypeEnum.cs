using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEOAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAccountTypeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE public.company_payment_account
                SET account_type = CASE
                    WHEN lower(trim(account_type)) IN ('ahorro', 'ahorros', 'saving', 'savings') THEN 'Ahorros'
                    WHEN lower(trim(account_type)) IN ('corriente', 'current', 'checking', 'checkings') THEN 'Corriente'
                    ELSE 'Ahorros'
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "account_type",
                schema: "public",
                table: "company_payment_account",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_payment_account_account_type",
                schema: "public",
                table: "company_payment_account",
                sql: "account_type IN ('Ahorros', 'Corriente')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_company_payment_account_account_type",
                schema: "public",
                table: "company_payment_account");

            migrationBuilder.AlterColumn<string>(
                name: "account_type",
                schema: "public",
                table: "company_payment_account",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
