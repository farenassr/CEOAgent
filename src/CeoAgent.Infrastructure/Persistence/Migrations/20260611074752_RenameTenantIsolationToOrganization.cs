using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEOAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTenantIsolationToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agent_profile_companies_company_id",
                schema: "public",
                table: "agent_profile");

            migrationBuilder.DropForeignKey(
                name: "fk_company_channel_companies_company_id",
                schema: "public",
                table: "company_channel");

            migrationBuilder.DropForeignKey(
                name: "fk_company_tool_company_company_id",
                schema: "public",
                table: "company_tool");

            migrationBuilder.DropForeignKey(
                name: "fk_integration_credential_reference_company_company_id",
                schema: "public",
                table: "integration_credential_reference");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "tool_execution",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_tool_execution_company_id_idempotency_key",
                schema: "public",
                table: "tool_execution",
                newName: "ix_tool_execution_organization_id_idempotency_key");

            migrationBuilder.RenameIndex(
                name: "ix_tool_execution_company_id_created_at",
                schema: "public",
                table: "tool_execution",
                newName: "ix_tool_execution_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "message",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_message_company_id_provider_message_id",
                schema: "public",
                table: "message",
                newName: "ix_message_organization_id_provider_message_id");

            migrationBuilder.RenameIndex(
                name: "ix_message_company_id_created_at",
                schema: "public",
                table: "message",
                newName: "ix_message_organization_id_created_at");

            migrationBuilder.RenameIndex(
                name: "ix_message_company_id_conversation_id_occurred_at_id",
                schema: "public",
                table: "message",
                newName: "ix_message_organization_id_conversation_id_occurred_at_id");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "integration_credential_reference",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_integration_credential_reference_company_id_provider_purpose",
                schema: "public",
                table: "integration_credential_reference",
                newName: "ix_integration_credential_reference_organization_id_provider_p");

            migrationBuilder.RenameIndex(
                name: "ix_integration_credential_reference_company_id_created_at",
                schema: "public",
                table: "integration_credential_reference",
                newName: "ix_integration_credential_reference_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_incoming_message_outbox_company_id_message_id",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "ix_incoming_message_outbox_organization_id_message_id");

            migrationBuilder.RenameIndex(
                name: "ix_incoming_message_outbox_company_id_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "ix_incoming_message_outbox_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "customer",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_customer_company_id_created_at",
                schema: "public",
                table: "customer",
                newName: "ix_customer_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "conversation_state",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_state_company_id_created_at",
                schema: "public",
                table: "conversation_state",
                newName: "ix_conversation_state_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "conversation",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_company_id_customer_id_company_channel_id",
                schema: "public",
                table: "conversation",
                newName: "ix_conversation_organization_id_customer_id_company_channel_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_company_id_created_at",
                schema: "public",
                table: "conversation",
                newName: "ix_conversation_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "company_tool",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_company_tool_company_id_tool_key",
                schema: "public",
                table: "company_tool",
                newName: "ix_company_tool_organization_id_tool_key");

            migrationBuilder.RenameIndex(
                name: "ix_company_tool_company_id_created_at",
                schema: "public",
                table: "company_tool",
                newName: "ix_company_tool_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "company_channel",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_company_channel_company_id_created_at",
                schema: "public",
                table: "company_channel",
                newName: "ix_company_channel_organization_id_created_at");

            migrationBuilder.RenameColumn(
                name: "company_id",
                schema: "public",
                table: "agent_profile",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_agent_profile_company_id_created_at",
                schema: "public",
                table: "agent_profile",
                newName: "ix_agent_profile_organization_id_created_at");

            migrationBuilder.RenameIndex(
                name: "ix_agent_profile_company_id",
                schema: "public",
                table: "agent_profile",
                newName: "ix_agent_profile_organization_id");

            migrationBuilder.AddForeignKey(
                name: "fk_agent_profile_companies_organization_id",
                schema: "public",
                table: "agent_profile",
                column: "organization_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_channel_companies_organization_id",
                schema: "public",
                table: "company_channel",
                column: "organization_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_tool_company_organization_id",
                schema: "public",
                table: "company_tool",
                column: "organization_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_integration_credential_reference_company_organization_id",
                schema: "public",
                table: "integration_credential_reference",
                column: "organization_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agent_profile_companies_organization_id",
                schema: "public",
                table: "agent_profile");

            migrationBuilder.DropForeignKey(
                name: "fk_company_channel_companies_organization_id",
                schema: "public",
                table: "company_channel");

            migrationBuilder.DropForeignKey(
                name: "fk_company_tool_company_organization_id",
                schema: "public",
                table: "company_tool");

            migrationBuilder.DropForeignKey(
                name: "fk_integration_credential_reference_company_organization_id",
                schema: "public",
                table: "integration_credential_reference");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "tool_execution",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_tool_execution_organization_id_idempotency_key",
                schema: "public",
                table: "tool_execution",
                newName: "ix_tool_execution_company_id_idempotency_key");

            migrationBuilder.RenameIndex(
                name: "ix_tool_execution_organization_id_created_at",
                schema: "public",
                table: "tool_execution",
                newName: "ix_tool_execution_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "message",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_message_organization_id_provider_message_id",
                schema: "public",
                table: "message",
                newName: "ix_message_company_id_provider_message_id");

            migrationBuilder.RenameIndex(
                name: "ix_message_organization_id_created_at",
                schema: "public",
                table: "message",
                newName: "ix_message_company_id_created_at");

            migrationBuilder.RenameIndex(
                name: "ix_message_organization_id_conversation_id_occurred_at_id",
                schema: "public",
                table: "message",
                newName: "ix_message_company_id_conversation_id_occurred_at_id");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "integration_credential_reference",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_integration_credential_reference_organization_id_provider_p",
                schema: "public",
                table: "integration_credential_reference",
                newName: "ix_integration_credential_reference_company_id_provider_purpose");

            migrationBuilder.RenameIndex(
                name: "ix_integration_credential_reference_organization_id_created_at",
                schema: "public",
                table: "integration_credential_reference",
                newName: "ix_integration_credential_reference_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_incoming_message_outbox_organization_id_message_id",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "ix_incoming_message_outbox_company_id_message_id");

            migrationBuilder.RenameIndex(
                name: "ix_incoming_message_outbox_organization_id_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                newName: "ix_incoming_message_outbox_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "customer",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_customer_organization_id_created_at",
                schema: "public",
                table: "customer",
                newName: "ix_customer_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "conversation_state",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_state_organization_id_created_at",
                schema: "public",
                table: "conversation_state",
                newName: "ix_conversation_state_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "conversation",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_organization_id_customer_id_company_channel_id",
                schema: "public",
                table: "conversation",
                newName: "ix_conversation_company_id_customer_id_company_channel_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversation_organization_id_created_at",
                schema: "public",
                table: "conversation",
                newName: "ix_conversation_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "company_tool",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_company_tool_organization_id_tool_key",
                schema: "public",
                table: "company_tool",
                newName: "ix_company_tool_company_id_tool_key");

            migrationBuilder.RenameIndex(
                name: "ix_company_tool_organization_id_created_at",
                schema: "public",
                table: "company_tool",
                newName: "ix_company_tool_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "company_channel",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_company_channel_organization_id_created_at",
                schema: "public",
                table: "company_channel",
                newName: "ix_company_channel_company_id_created_at");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                schema: "public",
                table: "agent_profile",
                newName: "company_id");

            migrationBuilder.RenameIndex(
                name: "ix_agent_profile_organization_id_created_at",
                schema: "public",
                table: "agent_profile",
                newName: "ix_agent_profile_company_id_created_at");

            migrationBuilder.RenameIndex(
                name: "ix_agent_profile_organization_id",
                schema: "public",
                table: "agent_profile",
                newName: "ix_agent_profile_company_id");

            migrationBuilder.AddForeignKey(
                name: "fk_agent_profile_companies_company_id",
                schema: "public",
                table: "agent_profile",
                column: "company_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_channel_companies_company_id",
                schema: "public",
                table: "company_channel",
                column: "company_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_tool_company_company_id",
                schema: "public",
                table: "company_tool",
                column: "company_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_integration_credential_reference_company_company_id",
                schema: "public",
                table: "integration_credential_reference",
                column: "company_id",
                principalSchema: "public",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
