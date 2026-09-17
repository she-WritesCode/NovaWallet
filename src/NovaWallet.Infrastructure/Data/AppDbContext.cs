using Microsoft.EntityFrameworkCore;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Data;

public class AppDbContext : DbContext
{
  public DbSet<Wallet> Wallets => Set<Wallet>();
  public DbSet<Transaction> Transactions => Set<Transaction>();
  public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
  public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
  }
}