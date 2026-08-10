using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.BidCompare.ClauseTemplates;

public class ClauseTemplateAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IClauseTemplateAppService _appService;

    public ClauseTemplateAppServiceTests()
    {
        _appService = GetRequiredService<IClauseTemplateAppService>();
    }

    [Fact]
    public async Task Create_Should_Return_Full_Dto()
    {
        var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto
        {
            Text = "须提供 ISO9001 质量管理体系认证证书",
            Mandatory = true,
            Category = "资质"
        });

        created.Id.ShouldNotBe(Guid.Empty);
        created.Text.ShouldContain("ISO9001");
        created.Mandatory.ShouldBeTrue();
        created.Category.ShouldBe("资质");
        created.CreationTime.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Fact]
    public async Task GetList_Should_Page_And_Filter_By_Keyword()
    {
        await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "须提供营业执照", Category = "资质" });
        await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "报价不得高于最高限价", Category = "报价" });

        var all = await _appService.GetListAsync(new GetClauseTemplatesInput { MaxResultCount = 10 });
        all.TotalCount.ShouldBe(2);
        all.Items.Count.ShouldBe(2);

        var filtered = await _appService.GetListAsync(new GetClauseTemplatesInput { Keyword = "报价", MaxResultCount = 10 });
        filtered.TotalCount.ShouldBe(1);
        filtered.Items[0].Category.ShouldBe("报价");
    }

    [Fact]
    public async Task Update_Should_Be_Full_Replace_And_Return_Dto()
    {
        var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "旧文本" });

        var updated = await _appService.UpdateAsync(created.Id, new ClauseTemplateCreateUpdateDto
        {
            Text = "新文本",
            Mandatory = false,
            Category = "格式"
        });

        updated.Id.ShouldBe(created.Id);
        updated.Text.ShouldBe("新文本");
        updated.Mandatory.ShouldBeFalse();
        updated.Category.ShouldBe("格式");
    }

    [Fact]
    public async Task Delete_Should_Remove_Entity()
    {
        var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "待删除" });

        await _appService.DeleteAsync(created.Id);

        var repo = GetRequiredService<IRepository<Clauses.ClauseTemplate, Guid>>();
        (await repo.FindAsync(created.Id)).ShouldBeNull();
    }
}
