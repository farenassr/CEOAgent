using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncomingMessageOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incoming_message_outbox",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_company_id_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_incoming_message_outbox_company_id_message_id",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "company_id", "message_id" },
                unique: true);

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
                name: "ix_incoming_message_outbox_status_created_at",
                schema: "public",
                table: "incoming_message_outbox",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incoming_message_outbox",
                schema: "public");
        }
    }
}
