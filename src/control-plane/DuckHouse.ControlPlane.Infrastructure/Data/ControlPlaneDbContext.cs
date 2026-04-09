using DuckHouse.ControlPlane.Core.Nodes;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.ControlPlane.Infrastructure.Data;

public class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public DbSet<NodeConfig> NodeConfigs => Set<NodeConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NodeConfig>(entity =>
        {
            entity.HasKey(e => e.NodeName);
            entity.Property(e => e.NodeName).HasMaxLength(256);
        });
    }
}
