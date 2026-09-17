using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
  public void Configure(EntityTypeBuilder<Transaction> builder)
  {
    builder.HasKey(t => t.Id);

    // Unique constraint on Reference — this is the business-level
    // idempotency guard. If two requests somehow bypass the
    // IdempotencyRecord check, the database itself will reject
    // the duplicate. Defence in depth.
    builder.Property(t => t.Reference)
        .IsRequired()
        .HasMaxLength(100);

    builder.HasIndex(t => t.Reference)
        .IsUnique();

    // All enums stored as strings
    builder.Property(t => t.Type)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(t => t.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(t => t.Channel)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(t => t.AmountKobo)
        .IsRequired();

    builder.Property(t => t.FeeAmountKobo)
        .HasDefaultValue(0L)
        .IsRequired();

    builder.Property(t => t.Currency)
        .HasMaxLength(3)
        .HasDefaultValue("NGN")
        .IsRequired();

    builder.Property(t => t.Narration)
        .HasMaxLength(500);

    builder.Property(t => t.InitiatedBy)
        .HasMaxLength(200);

    builder.Property(t => t.IpAddress)
        .HasMaxLength(45); // 45 covers IPv6

    builder.Property(t => t.Metadata)
        .HasColumnType("jsonb");

    builder.Property(t => t.CreatedAt)
        .HasDefaultValueSql("NOW()")
        .IsRequired();

    // Relationships
    // A transaction optionally has a source wallet (null for deposits)
    builder.HasOne(t => t.SourceWallet)
        .WithMany()
        .HasForeignKey(t => t.SourceWalletId)
        .OnDelete(DeleteBehavior.Restrict);

    // Always has a destination wallet
    builder.HasOne(t => t.DestinationWallet)
        .WithMany()
        .HasForeignKey(t => t.DestinationWalletId)
        .OnDelete(DeleteBehavior.Restrict);

    // Optionally has a fee wallet
    builder.HasOne(t => t.FeeWallet)
        .WithMany()
        .HasForeignKey(t => t.FeeWalletId)
        .OnDelete(DeleteBehavior.Restrict);

    // Self-referencing reversal tracking
    builder.HasOne(t => t.OriginalTransaction)
        .WithMany(t => t.ReversalTransactions)
        .HasForeignKey(t => t.OriginalTransactionId)
        .OnDelete(DeleteBehavior.Restrict);

    // Index for querying transactions by wallet — used for statements
    builder.HasIndex(t => t.SourceWalletId);
    builder.HasIndex(t => t.DestinationWalletId);
    builder.HasIndex(t => t.OriginalTransactionId);
    builder.HasIndex(t => new { t.Status, t.CreatedAt });
  }
}