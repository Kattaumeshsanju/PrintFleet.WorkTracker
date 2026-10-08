using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrintFleet.WorkTracker.Api.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=worktracker;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseNpgsql(connectionString);
        return new AppDbContext(options.Options);
    }
}
