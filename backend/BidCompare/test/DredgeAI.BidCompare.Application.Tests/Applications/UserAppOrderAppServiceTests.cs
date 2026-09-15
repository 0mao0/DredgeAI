using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Applications;

public class UserAppOrderAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IUserAppOrderAppService _appService;

    public UserAppOrderAppServiceTests()
    {
        _appService = GetRequiredService<IUserAppOrderAppService>();
    }

    [Fact]
    public async Task Set_Get_Reset_Roundtrip()
    {
        (await _appService.GetUserOrderAsync()).RouteIds.ShouldBeNull();

        await _appService.SetUserOrderAsync(new SetUserApplicationOrderInput { RouteIds = new List<string> { "/b", "/a" } });
        (await _appService.GetUserOrderAsync()).RouteIds.ShouldBe(["/b", "/a"]);

        await _appService.SetUserOrderAsync(new SetUserApplicationOrderInput { RouteIds = new List<string> { "/a" } });
        (await _appService.GetUserOrderAsync()).RouteIds.ShouldBe(["/a"]);

        var reset = await _appService.ResetUserOrdersAsync();
        reset.Count.ShouldBeGreaterThanOrEqualTo(1);
        (await _appService.GetUserOrderAsync()).RouteIds.ShouldBeNull();
    }
}
