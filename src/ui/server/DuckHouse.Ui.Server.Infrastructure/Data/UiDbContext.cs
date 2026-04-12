namespace DuckHouse.Ui.Server.Infrastructure.Data;

using DuckHouse.Ui.Server.Core.RuntimePackages;
using DuckHouse.Ui.Server.Core.Workspace;
using Microsoft.EntityFrameworkCore;

public class UiDbContext(DbContextOptions<UiDbContext> options) : DbContext(options)
{
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();
    public DbSet<WheelPackage> WheelPackages => Set<WheelPackage>();

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
    }
}
