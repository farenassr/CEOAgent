using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentProfileLlmBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "max_estimated_cost_usd_per_job",
                schema: "public",
                table: "agent_profile",
                type: "double precision",
                nullable: false,
                defaultValue: 0.050000000000000003);

            migrationBuilder.AddColumn<int>(
                name: "max_output_token_count",
                schema: "public",
                table: "agent_profile",
                type: "integer",
                nullable: false,
                defaultValue: 1024);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_estimated_cost_usd_per_job",
                schema: "public",
                table: "agent_profile");

            migrationBuilder.DropColumn(
                name: "max_output_token_count",
                schema: "public",
                table: "agent_profile");
        }
    }
}
