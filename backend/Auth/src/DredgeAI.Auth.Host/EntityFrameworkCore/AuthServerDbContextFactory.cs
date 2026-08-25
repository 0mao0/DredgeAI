using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;

namespace DredgeAI.EntityFrameworkCore;

public class AuthServerDbContextFactory : IDesignTimeDbContextFactory<AuthServerDbContext>
{
    public AuthServerDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        AbpOpenIddictDbProperties.DbTablePrefix = "tab_openid_dict";
        var builder = new DbContextOptionsBuilder<AuthServerDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"));

        return new AuthServerDbContext(builder.Options, new DefaultShiwDbContextHandler());
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}