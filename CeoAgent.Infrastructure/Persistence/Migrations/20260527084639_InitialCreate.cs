using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "company",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    working_hours_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_profile",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    prompt_override = table.Column<string>(type: "text", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_profile_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_credential_reference",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    purpose = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_credential_reference", x => x.id);
                    table.ForeignKey(
                        name: "fk_integration_credential_reference_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_channel",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_channel_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    credential_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_channel", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_channel_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_channel_integration_credential_references_credentia",
                        column: x => x.credential_reference_id,
                        principalSchema: "public",
                        principalTable: "integration_credential_reference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_tool",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    credential_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    configuration_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_tool", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_tool_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_tool_integration_credential_references_credential_r",
                        column: x => x.credential_reference_id,
                        principalSchema: "public",
                        principalTable: "integration_credential_reference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_customer_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_company_channel_company_channel_id",
                        column: x => x.company_channel_id,
                        principalSchema: "public",
                        principalTable: "company_channel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_agent_profile_agent_profile_id",
                        column: x => x.agent_profile_id,
                        principalSchema: "public",
                        principalTable: "agent_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversation_company_channel_company_channel_id",
                        column: x => x.company_channel_id,
                        principalSchema: "public",
                        principalTable: "company_channel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversation_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation_state",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_state", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_state_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message_text = table.Column<string>(type: "text", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tool_execution",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_tool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tool_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tool_execution", x => x.id);
                    table.ForeignKey(
                        name: "fk_tool_execution_company_tool_company_tool_id",
                        column: x => x.company_tool_id,
                        principalSchema: "public",
                        principalTable: "company_tool",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tool_execution_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tool_execution_message_result_message_id",
                        column: x => x.result_message_id,
                        principalSchema: "public",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tool_execution_message_trigger_message_id",
                        column: x => x.trigger_message_id,
                        principalSchema: "public",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_profile_company_id",
                schema: "public",
                table: "agent_profile",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_company_id",
                schema: "public",
                table: "company_channel",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_credential_reference_id",
                schema: "public",
                table: "company_channel",
                column: "credential_reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_provider",
                schema: "public",
                table: "company_channel",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ix_company_channel_provider_provider_channel_id",
                schema: "public",
                table: "company_channel",
                columns: new[] { "provider", "provider_channel_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_tool_company_id_tool_key",
                schema: "public",
                table: "company_tool",
                columns: new[] { "company_id", "tool_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_tool_credential_reference_id",
                schema: "public",
                table: "company_tool",
                column: "credential_reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_agent_profile_id",
                schema: "public",
                table: "conversation",
                column: "agent_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_company_channel_id",
                schema: "public",
                table: "conversation",
                column: "company_channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_company_id_customer_id_company_channel_id",
                schema: "public",
                table: "conversation",
                columns: new[] { "company_id", "customer_id", "company_channel_id" },
                unique: true,
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_customer_id",
                schema: "public",
                table: "conversation",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_state_conversation_id",
                schema: "public",
                table: "conversation_state",
                column: "conversation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_company_channel_id_external_customer_id",
                schema: "public",
                table: "customer",
                columns: new[] { "company_channel_id", "external_customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_credential_reference_company_id_provider_purpose",
                schema: "public",
                table: "integration_credential_reference",
                columns: new[] { "company_id", "provider", "purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_message_company_id_provider_message_id",
                schema: "public",
                table: "message",
                columns: new[] { "company_id", "provider_message_id" },
                unique: true,
                filter: "provider_message_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_message_conversation_id",
                schema: "public",
                table: "message",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_company_id_idempotency_key",
                schema: "public",
                table: "tool_execution",
                columns: new[] { "company_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_company_tool_id",
                schema: "public",
                table: "tool_execution",
                column: "company_tool_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_conversation_id",
                schema: "public",
                table: "tool_execution",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_result_message_id",
                schema: "public",
                table: "tool_execution",
                column: "result_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_execution_trigger_message_id",
                schema: "public",
                table: "tool_execution",
                column: "trigger_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_state",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tool_execution",
                schema: "public");

            migrationBuilder.DropTable(
                name: "company_tool",
                schema: "public");

            migrationBuilder.DropTable(
                name: "message",
                schema: "public");

            migrationBuilder.DropTable(
                name: "conversation",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agent_profile",
                schema: "public");

            migrationBuilder.DropTable(
                name: "customer",
                schema: "public");

            migrationBuilder.DropTable(
                name: "company_channel",
                schema: "public");

            migrationBuilder.DropTable(
                name: "integration_credential_reference",
                schema: "public");

            migrationBuilder.DropTable(
                name: "company",
                schema: "public");
        }
    }
}
