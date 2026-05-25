using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class AudioAssetConfiguration : IEntityTypeConfiguration<AudioAsset>
{
    public void Configure(EntityTypeBuilder<AudioAsset> builder)
    {
        builder.ToTable("audio_asset");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Direction).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.BlobUri).HasMaxLength(1_024).IsRequired();
        builder.Property(entity => entity.ContentType).HasMaxLength(120).IsRequired();
        builder.HasOne(entity => entity.Message)
            .WithMany()
            .HasForeignKey(entity => entity.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
