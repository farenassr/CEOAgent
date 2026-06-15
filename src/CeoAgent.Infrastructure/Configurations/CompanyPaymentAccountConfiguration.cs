using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class CompanyPaymentAccountConfiguration : IEntityTypeConfiguration<CompanyPaymentAccount>
{
    public void Configure(EntityTypeBuilder<CompanyPaymentAccount> builder)
    {
        builder.ToTable("company_payment_account", table =>
        {
            table.HasCheckConstraint(
                "ck_company_payment_account_account_type",
                "account_type IN ('Ahorros', 'Corriente')");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AccountNumber).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.AccountType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.AccountHolderName).HasMaxLength(200);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.ReservationPaymentAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.QrBlobContainer).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.QrBlobName).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.QrBlobUri).HasMaxLength(2048);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Currency })
            .IsUnique()
            .HasFilter("is_default AND is_active");
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.PaymentAccounts)
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Bank)
            .WithMany(entity => entity.PaymentAccounts)
            .HasForeignKey(entity => entity.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
