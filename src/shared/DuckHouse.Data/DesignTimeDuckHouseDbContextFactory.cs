using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DuckHouse.Data;

public class DesignTimeDuckHouseDbContextFactory : IDesignTimeDbContextFactory<DuckHouseDbContext>
{
    public DuckHouseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DuckHouseDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=duckhouse-ui;Username=postgres");
        return new DuckHouseDbContext(optionsBuilder.Options);
    }
}
