using System;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Exports;

public class ExportJobTests
{
    [Fact]
    public void ExportJob_Lifecycle()
    {
        var job = new ExportJob(Guid.NewGuid(), Guid.NewGuid(), ExportFormat.Pdf);
        job.Status.ShouldBe(ExportJobStatus.Pending);

        job.MarkRunning();
        job.Status.ShouldBe(ExportJobStatus.Running);

        job.MarkSucceeded("compare/t/exports/e.pdf");
        job.Status.ShouldBe(ExportJobStatus.Succeeded);
        job.FileStorageKey.ShouldBe("compare/t/exports/e.pdf");
    }

    [Fact]
    public void ExportJob_Can_Fail_With_Reason()
    {
        var job = new ExportJob(Guid.NewGuid(), Guid.NewGuid(), ExportFormat.Word);
        job.MarkRunning();
        job.MarkFailed("soffice 退出码 1");

        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.Error.ShouldContain("soffice");
    }

    [Fact]
    public void EvidenceItem_Should_Keep_Payload()
    {
        var id = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var item = new EvidenceItem(
            id, taskId, EvidenceType.Similarity, EvidenceSeverity.High,
            docIdsJson: "[\"a\"]", locationsJson: "[]", metricsJson: "{\"similarity\":0.93}",
            title: "标书A与标书B大段雷同", description: "第3章相似度 0.93", aiGenerated: false);

        item.Id.ShouldBe(id);
        item.TaskId.ShouldBe(taskId);
        item.Type.ShouldBe(EvidenceType.Similarity);
        item.Severity.ShouldBe(EvidenceSeverity.High);
        item.AiGenerated.ShouldBeFalse();
        item.MetricsJson.ShouldContain("0.93");
    }
}
