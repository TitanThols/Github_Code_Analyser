using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Repository> Repositories { get; set; }
    public DbSet<Dependency> Dependencies { get; set; }
    public DbSet<Vulnerability> Vulnerabilities { get; set; }
    public DbSet<VulnerabilitySnapshot> VulnerabilitySnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Dependency>()
            .HasOne(d => d.Repository)
            .WithMany(r => r.Dependencies)
            .HasForeignKey(d => d.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vulnerability>()
            .HasOne(v => v.Dependency)
            .WithMany(d => d.Vulnerabilities)
            .HasForeignKey(v => v.DependencyId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<VulnerabilitySnapshot>()
            .HasOne(vs => vs.Repository)
            .WithMany(r => r.VulnerabilitySnapshots)
            .HasForeignKey(vs => vs.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Repository>()
            .HasIndex(r => r.GithubUrl)
            .IsUnique();

        modelBuilder.Entity<Dependency>()
            .HasIndex(d => d.RepositoryId);

        modelBuilder.Entity<Dependency>()
            .HasIndex(d => new { d.RepositoryId, d.PackageName });

        modelBuilder.Entity<Vulnerability>()
            .HasIndex(v => v.DependencyId);

        modelBuilder.Entity<Vulnerability>()
            .HasIndex(v => v.CveId);

        modelBuilder.Entity<Vulnerability>()
            .HasIndex(v => v.Severity);

        modelBuilder.Entity<VulnerabilitySnapshot>()
            .HasIndex(vs => vs.RepositoryId);

        modelBuilder.Entity<VulnerabilitySnapshot>()
            .HasIndex(vs => vs.SnapshotDate);
    }
}