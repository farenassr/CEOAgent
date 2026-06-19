using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifyMessageDispatchState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "message_dispatch",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    succeeded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_dispatch", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_dispatch_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_message_dispatch_message_message_id",
                        column: x => x.message_id,
                        principalSchema: "public",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_conversation_id",
                schema: "public",
                table: "message_dispatch",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_message_id",
                schema: "public",
                table: "message_dispatch",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_operation_status_next_attempt_at_created_at",
                schema: "public",
                table: "message_dispatch",
                columns: new[] { "operation", "status", "next_attempt_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_organization_id_created_at",
                schema: "public",
                table: "message_dispatch",
                columns: new[] { "organization_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_organization_id_message_id_operation",
                schema: "public",
                table: "message_dispatch",
                columns: new[] { "organization_id", "message_id", "operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_message_dispatch_organization_id_operation_idempotency_key",
                schema: "public",
                table: "message_dispatch",
                columns: new[] { "organization_id", "operation", "idempotency_key" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO public.message_dispatch (
                    id,
                    conversation_id,
                    message_id,
                    operation,
                    provider,
                    status,
                    idempotency_key,
                    attempt_count,
                    max_attempts,
                    next_attempt_at,
                    last_attempt_at,
                    claimed_at,
                    claimed_by,
                    succeeded_at,
                    provider_message_id,
                    last_error,
                    correlation_id,
                    organization_id,
                    created_at,
                    updated_at)
                SELECT
                    id,
                    conversation_id,
                    message_id,
                    'InboundQueueDispatch',
                    'azure_queue',
                    CASE status
                        WHEN 'WaitingToBeQueued' THEN 'Pending'
                        WHEN 'QueueDispatchInProgress' THEN 'InProgress'
                        WHEN 'QueuedForWorkerProcessing' THEN 'Succeeded'
                        WHEN 'QueueDispatchRetryScheduled' THEN 'RetryScheduled'
                        WHEN 'QueueDispatchFailed' THEN 'Failed'
                        ELSE 'Failed'
                    END,
                    'inbound-queue:' || replace(message_id::text, '-', ''),
                    attempt_count,
                    max_attempts,
                    next_attempt_at,
                    last_attempt_at,
                    claimed_at,
                    claimed_by,
                    dispatched_at,
                    NULL,
                    failure_reason,
                    correlation_id,
                    organization_id,
                    created_at,
                    updated_at
                FROM public.incoming_message_outbox;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO public.message_dispatch (
                    id,
                    conversation_id,
                    message_id,
                    operation,
                    provider,
                    status,
                    idempotency_key,
                    attempt_count,
                    max_attempts,
                    next_attempt_at,
                    last_attempt_at,
                    claimed_at,
                    claimed_by,
                    succeeded_at,
                    provider_message_id,
                    last_error,
                    correlation_id,
                    organization_id,
                    created_at,
                    updated_at)
                SELECT
                    id,
                    conversation_id,
                    message_id,
                    'OutboundProviderSend',
                    provider,
                    CASE status
                        WHEN 'WaitingToSendToProvider' THEN 'Pending'
                        WHEN 'SendingToProvider' THEN 'InProgress'
                        WHEN 'SentToProvider' THEN 'Succeeded'
                        WHEN 'ProviderSendRetryScheduled' THEN 'RetryScheduled'
                        WHEN 'ProviderSendFailed' THEN 'Failed'
                        WHEN 'DeliveryCancelled' THEN 'Cancelled'
                        ELSE 'Failed'
                    END,
                    idempotency_key,
                    attempt_count,
                    max_attempts,
                    next_attempt_at,
                    claimed_at,
                    claimed_at,
                    claimed_by,
                    COALESCE(completed_at, sent_at),
                    provider_message_id,
                    last_error,
                    correlation_id,
                    organization_id,
                    created_at,
                    updated_at
                FROM public.outgoing_message_outbox;
                """);

            migrationBuilder.DropTable(
                name: "incoming_message_outbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "provider_send_ledger",
                schema: "public");

            migrationBuilder.DropTable(
                name: "outgoing_message_outbox",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_dispatch",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "incoming_message_outbox",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incoming_message_outbox", x => x.id);
                    table.ForeignKey(
                        name: "fk_incoming_message_outbox_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "public",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incoming_message_outbox_messages_message_id",
                        column: x => x.message_id,
                        principalSchema: "public",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outgoing_message_outbox",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_response_json = table.Column<string>(type: "text", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    request_hash = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                name: "ix_incoming_message_outbox_conversation_id",
                schema: "public",
                table: "incoming_message_outbox",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_message_id",
                schema: "public",
                table: "incoming_message_outbox",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_organization_id_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "organization_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_organization_id_message_id",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "organization_id", "message_id" },
                unique: true);

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
    }
}
