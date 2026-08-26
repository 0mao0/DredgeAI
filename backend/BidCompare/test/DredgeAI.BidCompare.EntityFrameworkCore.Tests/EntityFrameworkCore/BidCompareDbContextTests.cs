using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

public class BidCompareDbContextTests : BidCompareEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task Should_Persist_All_BidCompare_Aggregates()
    {
        var taskId = Guid.NewGuid();
        var taskRepo = ServiceProvider.GetRequiredService<IRepository<CompareTask, Guid>>();
        var docRepo = ServiceProvider.GetRequiredService<IRepository<CompareDocument, Guid>>();
        var evidenceRepo = ServiceProvider.GetRequiredService<IRepository<EvidenceItem, Guid>>();
        var templateRepo = ServiceProvider.GetRequiredService<IRepository<ClauseTemplate, Guid>>();
        var exportRepo = ServiceProvider.GetRequiredService<IRepository<ExportJob, Guid>>();

        await WithUnitOfWorkAsync(async () =>
        {
            await taskRepo.InsertAsync(new CompareTask(taskId, "任务1"));
            await docRepo.InsertAsync(new CompareDocument(Guid.NewGuid(), taskId,
                DocumentRole.Bid, "标书A.pdf", 1024, "compare/t/d/origin.pdf"));
            await evidenceRepo.InsertAsync(new EvidenceItem(Guid.NewGuid(), taskId,
                EvidenceType.Similarity, EvidenceSeverity.High, "[]", "[]", null, "t", "d", false));
            await templateRepo.InsertAsync(new ClauseTemplate(Guid.NewGuid(), "须提供资质证书", true, "资质"));
            await exportRepo.InsertAsync(new ExportJob(Guid.NewGuid(), taskId, ExportFormat.Pdf));
        });

        (await taskRepo.GetCountAsync()).ShouldBe(1);
        (await docRepo.GetCountAsync()).ShouldBe(1);
        (await evidenceRepo.GetCountAsync()).ShouldBe(1);
        (await templateRepo.GetCountAsync()).ShouldBe(1);
        (await exportRepo.GetCountAsync()).ShouldBe(1);
    }
}
