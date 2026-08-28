using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.BackgroundJobs;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

public class BidCompareHostDbContextFactory : IDesignTimeDbContextFactory<BidCompareDbContext>
{
    public BidCompareDbContext CreateDbContext(string[] args)
    {
        AbpBackgroundJobsDbProperties.DbTablePrefix = "tab_";
        var configuration = BuildConfiguration();
        var builder = new DbContextOptionsBuilder<BidCompareDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsAssembly("DredgeAI.BidCompare.Host"));

        return new BidCompareDbContext(builder.Options, new DefaultShiwDbContextHandler());
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
