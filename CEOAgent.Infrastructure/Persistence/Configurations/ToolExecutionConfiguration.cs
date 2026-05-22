using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

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
        builder.Property(entity => entity.ResultJson).HasColumnType("jsonb");
        builder.HasIndex(entity => new { entity.CompanyId, entity.IdempotencyKey }).IsUnique();
    }
}
