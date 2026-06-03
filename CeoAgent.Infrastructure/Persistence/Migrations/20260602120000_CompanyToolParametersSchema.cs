using CeoAgent.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CeoAgentDbContext))]
    [Migration("20260602120000_CompanyToolParametersSchema")]
    public partial class CompanyToolParametersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "parameters_schema_json",
                schema: "public",
                table: "company_tool",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE public.company_tool
                SET parameters_schema_json = CASE tool_key
                    WHEN 'check_google_calendar_availability' THEN '{"type":"object","properties":{"date":{"type":"string","description":"Company-local date to check in yyyy-MM-dd format."},"partySize":{"type":"integer","description":"Number of guests for the reservation."},"preferredTime":{"type":["string","null"],"description":"Company-local preferred start time in HH:mm format, or null when no time was provided."}},"required":["date","partySize","preferredTime"],"additionalProperties":false}'::jsonb
                    WHEN 'create_google_calendar_reservation' THEN '{"type":"object","properties":{"start":{"type":"string","description":"Reservation start timestamp with offset."},"end":{"type":"string","description":"Reservation end timestamp with offset."},"summary":{"type":"string","description":"Short reservation summary."}},"required":["start","end","summary"],"additionalProperties":false}'::jsonb
                    ELSE parameters_schema_json
                END
                WHERE tool_key IN ('check_google_calendar_availability','create_google_calendar_reservation');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parameters_schema_json",
                schema: "public",
                table: "company_tool");
        }
    }
}
