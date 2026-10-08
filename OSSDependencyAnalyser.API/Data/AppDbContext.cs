using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Data;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<VulnerabilitySnapshot> VulnerabilitySnapshots => Set<VulnerabilitySnapshot>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    /// <summary>ASP.NET Data Protection keys, persisted so auth cookies survive container restarts.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Repository>(entity =>
        {
            entity.Property(r => r.GithubUrl).HasMaxLength(300);
            entity.Property(r => r.RepositoryOwner).HasMaxLength(100);
            entity.Property(r => r.RepositoryName).HasMaxLength(100);
            entity.Property(r => r.Language).HasMaxLength(100);
            entity.Property(r => r.Description).HasMaxLength(1000);
            entity.Ignore(r => r.IsInProgress);

            entity.HasIndex(r => new { r.UserId, r.GithubUrl }).IsUnique();
            entity.HasIndex(r => r.Status);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Dependency>(entity =>
        {
            entity.HasOne(d => d.Repository)
                .WithMany(r => r.Dependencies)
                .HasForeignKey(d => d.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.RepositoryId, d.PackageName });
        });

        modelBuilder.Entity<Vulnerability>(entity =>
        {
            entity.HasOne(v => v.Dependency)
                .WithMany(d => d.Vulnerabilities)
                .HasForeignKey(v => v.DependencyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(v => v.DependencyId);
            entity.HasIndex(v => v.CveId);
            entity.HasIndex(v => v.Severity);
        });

        modelBuilder.Entity<VulnerabilitySnapshot>(entity =>
        {
            entity.HasOne(vs => vs.Repository)
                .WithMany(r => r.VulnerabilitySnapshots)
                .HasForeignKey(vs => vs.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(vs => new { vs.RepositoryId, vs.SnapshotDate });
        });

        modelBuilder.Entity<UserAccount>()
            .HasIndex(user => user.Email)
            .IsUnique();
    }
}
