using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.AnGineer;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.MeetingBot;

public class MeetingProjectAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IMeetingProjectAppService _projects;

    public MeetingProjectAppServiceTests()
    {
        _projects = GetRequiredService<IMeetingProjectAppService>();
    }

    [Fact]
    public async Task Create_And_Extract_Should_Store_Project_Info()
    {
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        llm.QueueResponse(
            "{\"projectName\":\"基坑支护项目\",\"projectInfo\":\"上海市，工期 90 天\"," +
            "\"mainContent\":\"基坑开挖与支护施工，含降水与监测。\"}");

        var created = await _projects.CreateAsync(new CreateMeetingProjectInput
        {
            Name = "基坑支护项目",
            DocId = "doc-proj-1"
        });
        created.Status.ShouldBe("ready");

        var extracted = await _projects.ExtractAsync(created.Id);
        extracted.Summary.ShouldContain("基坑开挖与支护");
        extracted.ProjectInfoJson.ShouldContain("projectName");
        extracted.Summary.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Extract_Should_Throw_When_Parse_Not_Ready()
    {
        var anGineer = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
        anGineer.RepeatingState = new AnGineerJobStatus(AnGineerJobState.Processing, 10, "parsing", "解析中");

        var created = await _projects.CreateAsync(new CreateMeetingProjectInput
        {
            Name = "某项目",
            DocId = "doc-not-ready"
        });
        created.Status.ShouldBe("processing");

        await Should.ThrowAsync<BusinessException>(() => _projects.ExtractAsync(created.Id));
    }

    [Fact]
    public async Task Update_Should_Rename_And_Replace_Docs()
    {
        var created = await _projects.CreateAsync(new CreateMeetingProjectInput
        {
            Name = "旧项目名",
            DocId = "doc-old"
        });

        var updated = await _projects.UpdateAsync(created.Id, new UpdateMeetingProjectInput
        {
            Name = "新项目名",
            DocIds = ["doc-new-1", "doc-new-2"]
        });

        updated.Name.ShouldBe("新项目名");
        updated.DocIds.ShouldBe(["doc-new-1", "doc-new-2"]);

        var cleared = await _projects.UpdateAsync(created.Id, new UpdateMeetingProjectInput
        {
            Name = "新项目名",
            DocIds = []
        });
        cleared.DocIds.ShouldBeEmpty();
        cleared.AnGineerDocId.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_Should_Remove_Project()
    {
        var anGineer = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
        var created = await _projects.CreateAsync(new CreateMeetingProjectInput
        {
            Name = "待删除项目",
            DocId = "doc-delete"
        });

        await _projects.DeleteAsync(created.Id);

        anGineer.DeletedDocs.ShouldContain("doc-delete");
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            () => _projects.GetAsync(created.Id));
    }
}
