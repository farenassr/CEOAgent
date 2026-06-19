using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeoAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentRuntimeSessionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "agent_session_expires_at",
                schema: "public",
                table: "conversation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_session_json",
                schema: "public",
                table: "conversation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "agent_session_last_used_at",
                schema: "public",
                table: "conversation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_session_reset_reason",
                schema: "public",
                table: "conversation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "agent_session_started_at",
                schema: "public",
                table: "conversation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "agent_session_turn_count",
                schema: "public",
                table: "conversation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "llm_provider",
                schema: "public",
                table: "conversation",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                schema: "public",
                table: "conversation",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_conversation_id",
                schema: "public",
                table: "conversation",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_last_response_id",
                schema: "public",
                table: "conversation",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "llm_provider",
                schema: "public",
                table: "agent_profile",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "OpenAI");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_session_expires_at",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "agent_session_json",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "agent_session_last_used_at",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "agent_session_reset_reason",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "agent_session_started_at",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "agent_session_turn_count",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "llm_provider",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "model_name",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "provider_conversation_id",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "provider_last_response_id",
                schema: "public",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "llm_provider",
                schema: "public",
                table: "agent_profile");
        }
    }
}
