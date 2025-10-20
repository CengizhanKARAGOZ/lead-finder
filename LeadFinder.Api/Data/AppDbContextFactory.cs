using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeadFinder.Api.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = cfg.GetConnectionString("Default")
                 ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cs, ServerVersion.AutoDetect(cs))
            .Options;

        return new AppDbContext(options);
    }
}