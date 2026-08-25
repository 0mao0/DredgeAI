using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace DredgeAI.Application;

public class DredgeAIBaseApplicationModule_Tests : DredgeAIBaseApplicationTestBase
{
    [Fact]
    public void Should_Resolve_ObjectMapper()
    {
        var mapper = ServiceProvider.GetRequiredService<IObjectMapper>();
        mapper.ShouldNotBeNull();
    }
}
