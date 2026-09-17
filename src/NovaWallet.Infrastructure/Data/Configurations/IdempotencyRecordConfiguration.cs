using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Data.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
  public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
  {
    builder.HasKey(i => i.Id);

    builder.Property(i => i.IdempotencyKey)
        .IsRequired()
        .HasMaxLength(100);

    // This is the lookup index — every incoming request checks
    // "has this key been used before?" It must be fast.
    builder.HasIndex(i => i.IdempotencyKey)
        .IsUnique();

    builder.Property(i => i.RequestHash)
        .IsRequired()
        .HasMaxLength(64); // SHA-256 hex = 64 chars

    builder.Property(i => i.StatusCode)
        .IsRequired();

    builder.Property(i => i.ResponseBody)
        .HasColumnType("text");

    builder.Property(i => i.CreatedAt)
        .HasDefaultValueSql("NOW()")
        .IsRequired();

    builder.Property(i => i.ExpiresAt)
        .IsRequired();

    // Relationship to transaction — optional because a failed
    // validation still creates an idempotency record (to cache
    // the error response) but produces no transaction
    builder.HasOne(i => i.Transaction)
        .WithOne(t => t.IdempotencyRecord)
        .HasForeignKey<IdempotencyRecord>(i => i.TransactionId)
        .OnDelete(DeleteBehavior.SetNull);

    // For cleanup jobs that delete expired records
    builder.HasIndex(i => i.ExpiresAt);
  }
}