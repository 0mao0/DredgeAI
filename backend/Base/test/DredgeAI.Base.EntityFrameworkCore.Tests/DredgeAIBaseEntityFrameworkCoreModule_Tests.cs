using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DredgeAI.EntityFrameworkCore;

public class DredgeAIBaseEntityFrameworkCoreModule_Tests : DredgeAIBaseEntityFrameworkCoreTestBase
{
    [Fact]
    public void Should_Resolve_DbContext()
    {
        var dbContext = ServiceProvider.GetRequiredService<DredgeAIBaseDbContext>();
        dbContext.ShouldNotBeNull();
    }
}
