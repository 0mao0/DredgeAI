using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.Gateway.Proxying;

public class ProxyClusterAppService_Tests : GatewayApplicationTestBase<GatewayApplicationTestModule>
{
    private readonly IProxyClusterAppService _appService;
    private readonly DatabaseProxyConfigProvider _configProvider;

    public ProxyClusterAppService_Tests()
    {
        _appService = GetRequiredService<IProxyClusterAppService>();
        _configProvider = GetRequiredService<DatabaseProxyConfigProvider>();
    }

    [Fact]
    public async Task Delete_Should_Reject_Cluster_In_Use()
    {
        var list = await _appService.GetListAsync();
        var clusterA = list.Items.Single(x => x.ClusterId == "cluster-a");

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.DeleteAsync(clusterA.Id));

        ex.Code.ShouldBe(GatewayErrorCodes.ClusterInUse);
    }

    [Fact]
    public async Task Create_Should_Reject_Invalid_Destination_Address()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new ProxyClusterCreateUpdateDto
        {
            ClusterId = "cluster-bad",
            Destinations = new() { ["destination1"] = new ClusterDestinationDto { Address = "not-a-uri" } }
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.InvalidDestinationAddress);
    }

    [Fact]
    public async Task Create_Then_GetList_Should_Roundtrip_Destinations()
    {
        await _appService.CreateAsync(new ProxyClusterCreateUpdateDto
        {
            ClusterId = "cluster-c",
            Destinations = new() { ["destination1"] = new ClusterDestinationDto { Address = "http://c:8080/" } }
        });

        var list = await _appService.GetListAsync();
        var clusterC = list.Items.Single(x => x.ClusterId == "cluster-c");
        clusterC.Destinations["destination1"].Address.ShouldBe("http://c:8080/");
    }

    [Fact]
    public async Task Create_With_IsEnabled_False_Should_Persist_And_Exclude_From_Snapshot()
    {
        await _appService.CreateAsync(new ProxyClusterCreateUpdateDto
        {
            ClusterId = "cluster-disabled",
            Destinations = new() { ["destination1"] = new ClusterDestinationDto { Address = "http://d:8080/" } },
            IsEnabled = false
        });

        var list = await _appService.GetListAsync();
        list.Items.Single(x => x.ClusterId == "cluster-disabled").IsEnabled.ShouldBeFalse();

        _configProvider.GetConfig().Clusters.ShouldNotContain(x => x.ClusterId == "cluster-disabled");
    }
}
