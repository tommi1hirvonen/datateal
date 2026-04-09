using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DuckHouse.ControlPlane.Infrastructure.Data;

internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ControlPlaneDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=duckhouse-control-plane;Username=postgres");
        return new ControlPlaneDbContext(optionsBuilder.Options);
    }
}
