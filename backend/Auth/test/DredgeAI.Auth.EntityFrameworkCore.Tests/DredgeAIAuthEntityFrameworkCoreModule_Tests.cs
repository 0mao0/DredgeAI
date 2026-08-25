using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Identity.EntityFrameworkCore;
using Xunit;

namespace DredgeAI;

public class DredgeAIAuthEntityFrameworkCoreModule_Tests : DredgeAIAuthEntityFrameworkCoreTestBase
{
    [Fact]
    public void Should_Resolve_IdentityDbContext()
    {
        var dbContext = ServiceProvider.GetRequiredService<IIdentityDbContext>();
        dbContext.ShouldNotBeNull();
    }
}
