using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;

namespace NovaWallet.Infrastructure.Data.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
  public void Configure(EntityTypeBuilder<Wallet> builder)
  {
    builder.HasKey(w => w.Id);

    builder.Property(w => w.CustomerId)
        .IsRequired()
        .HasMaxLength(100);

    builder.HasIndex(w => new { w.CustomerId, w.Currency })
        .IsUnique();

    builder.Property(w => w.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(w => w.Currency)
        .HasMaxLength(3)
        .HasDefaultValue("NGN")
        .IsRequired();

    builder.Property(w => w.AvailableBalanceKobo)
        .HasDefaultValue(0L)
        .IsRequired();

    builder.Property(w => w.BookBalanceKobo)
        .HasDefaultValue(0L)
        .IsRequired();

    builder.Property(w => w.DailyOutboundTotalKobo)
        .HasDefaultValue(0L)
        .IsRequired();

    builder.Property(w => w.DailyLimitResetDate)
        .IsRequired();

    builder.Property(w => w.Version)
        .IsConcurrencyToken();

    builder.Property(w => w.CreatedAt)
        .HasDefaultValueSql("NOW()")
        .IsRequired();

    builder.Property(w => w.UpdatedAt)
        .HasDefaultValueSql("NOW()")
        .IsRequired();
  }
}