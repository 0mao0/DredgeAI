using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.MeetingBot;

public class WorkerProfileAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IWorkerProfileAppService _workerService;

    public WorkerProfileAppServiceTests()
    {
        _workerService = GetRequiredService<IWorkerProfileAppService>();
    }

    [Fact]
    public async Task Create_Should_Dedupe_By_EmployeeNo()
    {
        var first = await _workerService.CreateAsync(new WorkerCreateInput
        {
            Name = "王飞",
            EmployeeNo = "320482198704085913",
            Team = "钢筋班"
        });
        var second = await _workerService.CreateAsync(new WorkerCreateInput
        {
            Name = "王飞",
            EmployeeNo = "320482198704085913",
            Team = "模板班"
        });
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task Create_Should_Dedupe_By_Name_And_Birthday()
    {
        var first = await _workerService.CreateAsync(new WorkerCreateInput
        {
            Name = "王飞",
            EmployeeNo = "320482198704085913",
            Team = "钢筋班"
        });
        // 同一人但证件号被录错一位：姓名 + 出生日期相同 → 复用已有档案，避免重复登记
        var second = await _workerService.CreateAsync(new WorkerCreateInput
        {
            Name = "王飞",
            EmployeeNo = "32068219870408597X",
            Team = "模板班"
        });
        second.Id.ShouldBe(first.Id);
    }
}
