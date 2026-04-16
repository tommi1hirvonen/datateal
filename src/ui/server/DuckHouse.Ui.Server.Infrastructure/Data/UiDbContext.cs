namespace DuckHouse.Ui.Server.Infrastructure.Data;

using DuckHouse.Ui.Server.Core.Environment;
using DuckHouse.Ui.Server.Core.RuntimePackages;
using DuckHouse.Ui.Server.Core.Workspace;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class UiDbContext(DbContextOptions<UiDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();
    public DbSet<WheelPackage> WheelPackages => Set<WheelPackage>();
    public DbSet<EnvironmentVariable> EnvironmentVariables => Set<EnvironmentVariable>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasDiscriminator<string>("ItemType")
                .HasValue<Notebook>("Notebook")
                .HasValue<Query>("Query");
            entity.Property("ItemType").HasMaxLength(32).IsRequired();
            entity.HasOne(e => e.Folder)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.FolderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique title within each folder (notebooks and queries share the same namespace).
            // Two partial indexes handle the nullable FolderId: one for items in a folder, one for root items.
            entity.HasIndex(e => new { e.Title, e.FolderId })
                .IsUnique()
                .HasFilter("\"FolderId\" IS NOT NULL")
                .HasDatabaseName("IX_WorkspaceItems_Title_FolderId");
            entity.HasIndex(e => e.Title)
                .IsUnique()
                .HasFilter("\"FolderId\" IS NULL")
                .HasDatabaseName("IX_WorkspaceItems_Title_Root");
        });

        modelBuilder.Entity<Query>(entity =>
        {
            entity.Property(e => e.LastResultStatus).HasMaxLength(16);
        });

        modelBuilder.Entity<WheelPackage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<EnvironmentVariable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<Secret>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}
