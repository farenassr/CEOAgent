using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEOAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkerReliabilityIndexesAndConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_message_company_id_conversation_id_occurred_at_id",
                schema: "public",
                table: "message",
                columns: new[] { "company_id", "conversation_id", "occurred_at", "id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_message_company_id_conversation_id_occurred_at_id",
                schema: "public",
                table: "message");
        }
    }
}
