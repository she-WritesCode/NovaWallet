using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Data.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
  public void Configure(EntityTypeBuilder<LedgerEntry> builder)
  {
    builder.HasKey(e => e.Id);

    builder.Property(e => e.EntryType)
        .HasConversion<string>()
        .HasMaxLength(10)
        .IsRequired();

    builder.Property(e => e.AmountKobo)
        .IsRequired();

    builder.Property(e => e.BalanceAfterKobo)
        .IsRequired();

    builder.Property(e => e.CreatedAt)
        .HasDefaultValueSql("NOW()")
        .IsRequired();

    // Relationships
    builder.HasOne(e => e.Transaction)
        .WithMany(t => t.LedgerEntries)
        .HasForeignKey(e => e.TransactionId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(e => e.Wallet)
        .WithMany(w => w.LedgerEntries)
        .HasForeignKey(e => e.WalletId)
        .OnDelete(DeleteBehavior.Restrict);

    // This is the statement query index — "give me all entries
    // for this wallet, newest first." Without it, every statement
    // request does a full table scan.
    builder.HasIndex(e => new { e.WalletId, e.CreatedAt });

    // For finding both sides of a double-entry pair
    builder.HasIndex(e => e.TransactionId);
  }
}