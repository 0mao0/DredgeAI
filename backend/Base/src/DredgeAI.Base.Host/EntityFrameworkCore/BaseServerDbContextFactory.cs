using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shiw.Abp.BaseEntityFrameworkCore;
using Shiw.File;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace DredgeAI.EntityFrameworkCore;

public class BaseServerDbContextFactory : IDesignTimeDbContextFactory<BaseServerDbContext>
{
    public BaseServerDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        AbpCommonDbProperties.DbTablePrefix = "tab";
        AbpIdentityDbProperties.DbTablePrefix="tab_identity";
        DredgeAIBaseDbProperties.DbTablePrefix="tab";
        FileDbProperties.DbTablePrefix = "tab";
        var builder = new DbContextOptionsBuilder<BaseServerDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"));

        return new BaseServerDbContext(builder.Options, new DefaultShiwDbContextHandler());
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}