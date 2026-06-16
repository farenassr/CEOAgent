using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReliabilityOutboxLedgerSharedEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_incoming_message_outbox_status_created_at",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "public",
                table: "incoming_message_outbox",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_at",
                schema: "public",
                table: "incoming_message_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claimed_by",
                schema: "public",
                table: "incoming_message_outbox",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_attempts",
                schema: "public",
                table: "incoming_message_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at",
                schema: "public",
                table: "incoming_message_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE public.incoming_message_outbox
                SET status = CASE status
                    WHEN 'Pending' THEN 'WaitingToBeQueued'
                    WHEN 'Failed' THEN 'QueueDispatchRetryScheduled'
                    WHEN 'Dispatched' THEN 'QueuedForWorkerProcessing'
                    ELSE status
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE public.tool_execution
                SET status = CASE status
                    WHEN 'Pending' THEN 'ToolExecutionWaitingToRun'
                    WHEN 'Succeeded' THEN 'ToolExecutionSucceeded'
                    WHEN 'Failed' THEN 'ToolExecutionFailed'
                    WHEN 'Denied' THEN 'ToolExecutionDenied'
                    ELSE status
                END;
                """);

            migrationBuilder.CreateTable(
                name: "outgoing_message_outbox",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outgoing_message_outbox", x => x.id);
                    table.ForeignKey(
                        name: "fk_outgoing_message_outbox_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_outgoing_message_outbox_messages_message_id",
                        column: x => x.message_id,
                        principalSchema: "public",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_send_ledger",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outgoing_message_outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_response_json = table.Column<string>(type: "text", nullable: true),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_send_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_provider_send_ledger_outgoing_message_outbox_outgoing_messa",
                        column: x => x.outgoing_message_outbox_id,
                        principalSchema: "public",
                        principalTable: "outgoing_message_outbox",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_status_next_attempt_at_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "status", "next_attempt_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_message_outbox_conversation_id",
                schema: "public",
                table: "outgoing_message_outbox",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_message_outbox_message_id",
                schema: "public",
                table: "outgoing_message_outbox",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_message_outbox_organization_id_created_at",
                schema: "public",
                table: "outgoing_message_outbox",
                columns: new[] { "organization_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_message_outbox_organization_id_idempotency_key",
                schema: "public",
                table: "outgoing_message_outbox",
                columns: new[] { "organization_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_message_outbox_status_next_attempt_at_created_at",
                schema: "public",
                table: "outgoing_message_outbox",
                columns: new[] { "status", "next_attempt_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_send_ledger_organization_id_created_at",
                schema: "public",
                table: "provider_send_ledger",
                columns: new[] { "organization_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_provider_send_ledger_organization_id_outgoing_message_outbo",
                schema: "public",
                table: "provider_send_ledger",
                columns: new[] { "organization_id", "outgoing_message_outbox_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_send_ledger_outgoing_message_outbox_id",
                schema: "public",
                table: "provider_send_ledger",
                column: "outgoing_message_outbox_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_send_ledger",
                schema: "public");

            migrationBuilder.DropTable(
                name: "outgoing_message_outbox",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_incoming_message_outbox_status_next_attempt_at_created_at",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.DropColumn(
                name: "claimed_by",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.DropColumn(
                name: "max_attempts",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "public",
                table: "incoming_message_outbox");

            migrationBuilder.Sql(
                """
                UPDATE public.incoming_message_outbox
                SET status = CASE status
                    WHEN 'WaitingToBeQueued' THEN 'Pending'
                    WHEN 'QueueDispatchInProgress' THEN 'Pending'
                    WHEN 'QueuedForWorkerProcessing' THEN 'Dispatched'
                    WHEN 'QueueDispatchRetryScheduled' THEN 'Failed'
                    WHEN 'QueueDispatchFailed' THEN 'Failed'
                    ELSE status
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE public.tool_execution
                SET status = CASE status
                    WHEN 'ToolExecutionWaitingToRun' THEN 'Pending'
                    WHEN 'ToolExecutionInProgress' THEN 'Pending'
                    WHEN 'ToolExecutionSucceeded' THEN 'Succeeded'
                    WHEN 'ToolExecutionFailed' THEN 'Failed'
                    WHEN 'ToolExecutionDenied' THEN 'Denied'
                    WHEN 'ToolExecutionRetryScheduled' THEN 'Failed'
                    ELSE status
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "public",
                table: "incoming_message_outbox",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_status_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "status", "created_at" });
        }
    }
}
