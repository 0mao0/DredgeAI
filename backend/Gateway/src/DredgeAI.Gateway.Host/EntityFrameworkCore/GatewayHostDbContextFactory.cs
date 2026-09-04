using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Shiw.Abp.BaseEntityFrameworkCore;

namespace DredgeAI.Gateway.EntityFrameworkCore;

public class GatewayHostDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var builder = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsAssembly("DredgeAI.Gateway.Host"));

        return new GatewayDbContext(builder.Options, new DefaultShiwDbContextHandler());
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
