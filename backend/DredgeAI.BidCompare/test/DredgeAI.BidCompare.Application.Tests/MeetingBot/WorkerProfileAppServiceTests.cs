using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.MeetingBot;

public class WorkerProfileAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IWorkerProfileAppService _appService;
    private readonly FakeLlmGateway _llm;
    private readonly FakeMeetingBotClient _bot;

    public WorkerProfileAppServiceTests()
    {
        _appService = GetRequiredService<IWorkerProfileAppService>();
        _llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        _bot = (FakeMeetingBotClient)GetRequiredService<IMeetingBotClient>();
    }

    [Fact]
    public async Task Create_Should_Be_Idempotent_By_EmployeeNo()
    {
        var created = await _appService.CreateAsync(new WorkerCreateInput
        {
            Name = "赵四",
            EmployeeNo = "110101199001011234",
            Team = "钢筋班"
        });

        created.Id.ShouldNotBe(Guid.Empty);
        created.FaceStatus.ShouldBe(FaceStatus.Pending);

        var again = await _appService.CreateAsync(new WorkerCreateInput
        {
            Name = "赵四",
            EmployeeNo = "110101199001011234"
        });
        again.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task Create_Should_Require_Name_And_EmployeeNo()
    {
        var ex = await Should.ThrowAsync<BusinessException>(
            () => _appService.CreateAsync(new WorkerCreateInput { Name = "", EmployeeNo = "" }));
        ex.Code.ShouldBe("MEETING_WORKER_REQUIRED");
    }

    [Fact]
    public async Task RecognizeIdCard_Should_Parse_Fields_From_Llm_Json()
    {
        _llm.QueueResponse(
            "{\"name\":\"张三\",\"idCardNumber\":\"110101199001011234\",\"gender\":\"男\"," +
            "\"nation\":\"汉\",\"birthDate\":\"1990-01-01\",\"address\":\"北京市朝阳区\"}");

        var result = await _appService.RecognizeIdCardAsync(new byte[] { 1, 2, 3 });

        result.Name.ShouldBe("张三");
        result.IdCardNumber.ShouldBe("110101199001011234");
        result.Gender.ShouldBe("男");
        result.Nation.ShouldBe("汉");
        result.BirthDate.ShouldBe("1990-01-01");
        result.Address.ShouldBe("北京市朝阳区");
        _llm.MultimodalRequests.ShouldContain(r => r.ImageCount == 1 && r.Text.Contains("公民身份号码"));
    }

    [Fact]
    public async Task RecognizeIdCard_Should_Return_Raw_When_Llm_Not_Json()
    {
        _llm.QueueResponse("这张图片无法识别");

        var result = await _appService.RecognizeIdCardAsync(new byte[] { 1 });

        result.Name.ShouldBeEmpty();
        result.RawText.ShouldBe("这张图片无法识别");
    }

    [Fact]
    public async Task UpdateFace_Should_Enroll_And_Mark_Enrolled()
    {
        var worker = await _appService.CreateAsync(new WorkerCreateInput
        {
            Name = "王五",
            EmployeeNo = "A009",
            Team = "电工班"
        });

        var updated = await _appService.UpdateFaceAsync(worker.Id, new byte[] { 1, 2, 3 });

        updated.FaceStatus.ShouldBe(FaceStatus.Enrolled);
        _bot.Enrolled.ShouldContain(e => e.WorkerId == worker.Id.ToString() && e.Name == "王五");
    }
}
