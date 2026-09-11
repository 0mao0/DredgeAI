using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace DredgeAI.BidCompare.Applications;

public class ApplicationCatalogAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IApplicationCatalogAppService _appService;

    public ApplicationCatalogAppServiceTests()
    {
        _appService = GetRequiredService<IApplicationCatalogAppService>();
    }

    [Fact]
    public async Task Seed_Should_Populate_Catalog()
    {
        var apps = await _appService.GetListAsync();

        apps.Count.ShouldBe(10);
        apps.Select(x => x.Name).ShouldBe(["规范问答", "AI视频", "AI 配音", "设计经验", "施工经验", "施组审核", "耙吸效率", "情报采集", "AI投标", "AI晨会"]);

        var app8 = apps.Single(x => x.Name == "情报采集");
        app8.SubApps.ShouldNotBeNull();
        app8.SubApps!.Select(x => x.Name).ShouldBe(["疏浚情报", "科技情报"]);
        app8.SubApps[0].Status.ShouldBe(AppCatalogStatus.Published);

        var app1 = apps.Single(x => x.Name == "规范问答");
        app1.SubApps.ShouldBeNull();
        app1.CreatedAt.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
        app1.Category.ShouldBe(AppCatalogCategory.General);
        app1.Status.ShouldBe(AppCatalogStatus.Online);
        app1.Scope.ShouldBe(AppCatalogScope.Public);
    }

    [Fact]
    public async Task UserList_Should_Reflect_Publish_Status()
    {
        var cards = await _appService.GetUserListAsync();

        var catalog = await _appService.GetListAsync();
        var subCard = cards.Single(x => x.Title == "疏浚情报");
        subCard.ParentAppId.ShouldBe(catalog.Single(x => x.Name == "情报采集").Id);
        subCard.Status.ShouldBe("已授权");

        var appCard = cards.Single(x => x.Title == "规范问答");
        appCard.Route.ShouldBe("/standard-query");

        await _appService.SetSubStatusAsync(new SetSubStatusInput { SubId = subCard.Id, Status = AppCatalogStatus.Unpublished });
        (await _appService.GetUserListAsync()).Any(x => x.Id == subCard.Id).ShouldBeFalse();

        await _appService.SetAppStatusAsync(new SetAppStatusInput { AppId = appCard.Id, Status = AppCatalogStatus.Offline });
        (await _appService.GetUserListAsync()).Single(x => x.Id == appCard.Id).Status.ShouldBe("已下架");
    }

    [Fact]
    public async Task Move_Should_Swap_SortOrder()
    {
        var catalog = await _appService.GetListAsync();
        var video = catalog.Single(x => x.Name == "AI视频");
        var intel = catalog.Single(x => x.Name == "情报采集");
        var tech = intel.SubApps!.Single(x => x.Name == "科技情报");

        var moved = await _appService.MoveAppAsync(new MoveAppOrderInput { AppId = video.Id, Direction = "up" });
        moved.Take(2).Select(x => x.Name).ShouldBe(["AI视频", "规范问答"]);

        // "1" 已在第二位，将其上移后仍在边界再移一次应不变
        var atTop = await _appService.MoveAppAsync(new MoveAppOrderInput { AppId = video.Id, Direction = "up" });
        atTop.Take(2).Select(x => x.Name).ShouldBe(["AI视频", "规范问答"]);

        var subMoved = await _appService.MoveSubAppAsync(new MoveSubAppOrderInput { SubId = tech.Id, Direction = "up" });
        subMoved.Single(x => x.Name == "情报采集").SubApps!.Select(x => x.Name).ShouldBe(["科技情报", "疏浚情报"]);
    }

    [Fact]
    public async Task SetStatus_Should_Throw_On_Unknown_Id()
    {
        var ex = await Should.ThrowAsync<BusinessException>(
            _appService.SetAppStatusAsync(new SetAppStatusInput { AppId = Guid.NewGuid(), Status = AppCatalogStatus.Offline }));
        ex.Code.ShouldBe("AppCatalog:AppNotFound");
    }

    [Fact]
    public async Task SetStatus_Should_Reject_Wrong_Kind()
    {
        var catalog = await _appService.GetListAsync();
        await Should.ThrowAsync<ArgumentException>(
            _appService.SetAppStatusAsync(new SetAppStatusInput { AppId = catalog.Single(x => x.Name == "规范问答").Id, Status = AppCatalogStatus.Published }));
        await Should.ThrowAsync<ArgumentException>(
            _appService.SetSubStatusAsync(new SetSubStatusInput { SubId = catalog.Single(x => x.Name == "情报采集").SubApps!.Single(x => x.Name == "疏浚情报").Id, Status = AppCatalogStatus.Online }));
    }

    [Fact]
    public async Task Seed_Should_Be_Idempotent()
    {
        var contributor = GetRequiredService<AppCatalogDataSeedContributor>();
        await contributor.SeedAsync(new DataSeedContext());

        var apps = await _appService.GetListAsync();
        apps.Count.ShouldBe(10);
        apps.Select(x => x.Name).ShouldBe(["规范问答", "AI视频", "AI 配音", "设计经验", "施工经验", "施组审核", "耙吸效率", "情报采集", "AI投标", "AI晨会"]);
    }
}
