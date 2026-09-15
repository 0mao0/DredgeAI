using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DredgeAI.Permissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Security.Claims;
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
    public async Task PermissionTree_Should_Group_By_Category()
    {
        var tree = await _appService.GetPermissionTreeAsync();

        // 第一层：类型分组按枚举值升序，Key 为 snake_case wire 值，Title 为本地化描述（非空且不等于 Key）
        var categories = tree.Select(x => Enum.Parse<AppCatalogCategory>(x.Key, ignoreCase: true)).ToList();
        categories.ShouldBe(categories.OrderBy(x => x).ToList());
        tree.ShouldAllBe(x => !string.IsNullOrEmpty(x.Title) && x.Title != x.Key);

        // 第二层/第三层：Key 为应用 Id，含子应用的主应用带出子应用列表，无子应用的 Children 为 null
        var catalog = await _appService.GetListAsync();
        var intel = catalog.Single(x => x.Name == "情报采集");
        var intelNode = tree.SelectMany(x => x.Children!).Single(x => x.Key == intel.Id.ToString());
        intelNode.Title.ShouldBe("情报采集");
        intelNode.Children!.Select(x => x.Key).ShouldBe(intel.SubApps!.Select(x => x.Id.ToString()).ToList());

        var plain = catalog.Single(x => x.Name == "规范问答");
        tree.SelectMany(x => x.Children!).Single(x => x.Key == plain.Id.ToString()).Children.ShouldBeNull();
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

    [Fact]
    public async Task AuthorizedList_Should_Filter_By_Grant_And_Status()
    {
        var permissionQuery = (FakeInternalPermissionQueryAppService)GetRequiredService<IInternalPermissionQueryAppService>();
        permissionQuery.Grants = [];

        // 无任何授权 + 无当前用户 → 空
        (await _appService.GetAuthorizedListAsync()).ShouldBeEmpty();

        var catalog = await _appService.GetListAsync();
        var plain = catalog.Single(x => x.Name == "规范问答");
        var intel = catalog.Single(x => x.Name == "情报采集");
        var dredgeSub = intel.SubApps!.Single(x => x.Name == "疏浚情报");
        var other = catalog.Single(x => x.Name == "AI视频");

        // 角色 tester：规范问答 + 疏浚情报；角色 other-role：AI视频（干扰项，验证按当前用户角色过滤）
        permissionQuery.Grants =
        [
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "tester", ResourceKey = plain.Id.ToString() },
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "tester", ResourceKey = dredgeSub.Id.ToString() },
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "other-role", ResourceKey = other.Id.ToString() },
        ];

        using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(AbpClaimTypes.Role, "tester"),
        ], "Test"))))
        {
            var list = await _appService.GetAuthorizedListAsync();
            list.Select(x => x.Name).ShouldBe(["规范问答", "情报采集"]);
            list.Single(x => x.Name == "情报采集").SubApps!.Select(x => x.Name).ShouldBe(["疏浚情报"]);

            // 下架后即使仍授权也不返回
            await _appService.SetAppStatusAsync(new SetAppStatusInput { AppId = plain.Id, Status = AppCatalogStatus.Offline });
            (await _appService.GetAuthorizedListAsync()).Select(x => x.Name).ShouldBe(["情报采集"]);
        }

        permissionQuery.Grants = [];
    }

    [Fact]
    public async Task List_Should_Include_Granted_Role_Names()
    {
        var permissionQuery = (FakeInternalPermissionQueryAppService)GetRequiredService<IInternalPermissionQueryAppService>();
        var catalog = await _appService.GetListAsync();
        var plain = catalog.Single(x => x.Name == "规范问答");
        var dredgeSub = catalog.Single(x => x.Name == "情报采集").SubApps!.Single(x => x.Name == "疏浚情报");

        permissionQuery.Grants =
        [
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "admin", ResourceKey = plain.Id.ToString() },
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "tester", ResourceKey = plain.Id.ToString() },
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "admin", ResourceKey = plain.Id.ToString() }, // 重复条目验证去重
            new ResourcePermissionGrantItemDto { ProviderName = "U", ProviderKey = Guid.NewGuid().ToString(), ResourceKey = plain.Id.ToString() }, // 用户授权不进角色列
            new ResourcePermissionGrantItemDto { ProviderName = "R", ProviderKey = "ops", ResourceKey = dredgeSub.Id.ToString() },
        ];
        try
        {
            var list = await _appService.GetListAsync();
            list.Single(x => x.Name == "规范问答").GrantedRoles.ShouldBe(["admin", "tester"]);
            list.Single(x => x.Name == "情报采集").SubApps!.Single(x => x.Name == "疏浚情报").GrantedRoles.ShouldBe(["ops"]);
            // 无授权应用为空数组而非 null
            list.Single(x => x.Name == "AI视频").GrantedRoles.ShouldBeEmpty();
        }
        finally
        {
            permissionQuery.Grants = []; // Fake 是 Singleton，避免污染同项目其它测试
        }
    }
}
