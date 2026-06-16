using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class ProviderSendLedgerConfiguration : IEntityTypeConfiguration<ProviderSendLedger>
{
    public void Configure(EntityTypeBuilder<ProviderSendLedger> builder)
    {
        builder.ToTable("provider_send_ledger");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.RequestHash).HasMaxLength(96);
        builder.Property(entity => entity.ProviderMessageId).HasMaxLength(200);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(120);
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(500);
        builder.Property(entity => entity.CorrelationId).HasMaxLength(120);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.OutgoingMessageOutboxId, entity.AttemptNumber }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.OutgoingMessageOutbox)
            .WithMany(entity => entity.ProviderSendLedgers)
            .HasForeignKey(entity => entity.OutgoingMessageOutboxId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
