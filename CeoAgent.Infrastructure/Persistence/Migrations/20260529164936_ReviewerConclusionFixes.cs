using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewerConclusionFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_company_channel_company_id",
                schema: "public",
                table: "company_channel");

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_company_id_created_at",
                schema: "public",
                table: "tool_execution",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_message_company_id_created_at",
                schema: "public",
                table: "message",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_integration_credential_reference_company_id_created_at",
                schema: "public",
                table: "integration_credential_reference",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_customer_company_id_created_at",
                schema: "public",
                table: "customer",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_state_company_id_created_at",
                schema: "public",
                table: "conversation_state",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_company_id_created_at",
                schema: "public",
                table: "conversation",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_company_tool_company_id_created_at",
                schema: "public",
                table: "company_tool",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_company_id_created_at",
                schema: "public",
                table: "company_channel",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_agent_profile_company_id_created_at",
                schema: "public",
                table: "agent_profile",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tool_execution_company_id_created_at",
                schema: "public",
                table: "tool_execution");

            migrationBuilder.DropIndex(
                name: "ix_message_company_id_created_at",
                schema: "public",
                table: "message");

            migrationBuilder.DropIndex(
                name: "ix_integration_credential_reference_company_id_created_at",
                schema: "public",
                table: "integration_credential_reference");

            migrationBuilder.DropIndex(
                name: "ix_customer_company_id_created_at",
                schema: "public",
                table: "customer");

            migrationBuilder.DropIndex(
                name: "ix_conversation_state_company_id_created_at",
                schema: "public",
                table: "conversation_state");

            migrationBuilder.DropIndex(
                name: "ix_conversation_company_id_created_at",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropIndex(
                name: "ix_company_tool_company_id_created_at",
                schema: "public",
                table: "company_tool");

            migrationBuilder.DropIndex(
                name: "ix_company_channel_company_id_created_at",
                schema: "public",
                table: "company_channel");

            migrationBuilder.DropIndex(
                name: "ix_agent_profile_company_id_created_at",
                schema: "public",
                table: "agent_profile");

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_company_id",
                schema: "public",
                table: "company_channel",
                column: "company_id");
        }
    }
}
