using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.AmountKobo)
            .IsRequired();

        builder.Property(a => a.BalanceBeforeKobo)
            .IsRequired();

        builder.Property(a => a.BalanceAfterKobo)
            .IsRequired();

        builder.Property(a => a.PerformedBy)
            .HasMaxLength(200);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Relationships
        builder.HasOne(a => a.Wallet)
            .WithMany(w => w.AuditLogs)
            .HasForeignKey(a => a.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Transaction)
            .WithMany(t => t.AuditLogs)
            .HasForeignKey(a => a.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for efficient querying during audits and reconciliations
        builder.HasIndex(a => new { a.WalletId, a.CreatedAt });
        builder.HasIndex(a => a.TransactionId);
        builder.HasIndex(a => a.EventType);
    }
}

