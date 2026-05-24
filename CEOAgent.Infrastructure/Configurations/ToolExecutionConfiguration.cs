using CEOAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Configurations;

public sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_execution");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ToolKey).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(240);
        builder.Property(entity => entity.Request).HasJsonbConversion("request_json");
        builder.Property(entity => entity.Result).HasJsonbConversion("result_json");
        builder.HasIndex(entity => new { entity.CompanyId, entity.IdempotencyKey }).IsUnique();
        builder.HasOne(entity => entity.Conversation)
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CompanyTool)
            .WithMany(entity => entity.ToolExecutions)
            .HasForeignKey(entity => entity.CompanyToolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.TriggerMessage)
            .WithMany(entity => entity.TriggeredToolExecutions)
            .HasForeignKey(entity => entity.TriggerMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ResultMessage)
            .WithMany(entity => entity.ResultToolExecutions)
            .HasForeignKey(entity => entity.ResultMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
