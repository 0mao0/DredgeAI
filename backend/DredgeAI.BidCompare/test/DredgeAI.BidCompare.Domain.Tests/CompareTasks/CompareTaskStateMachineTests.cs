using System;
using DredgeAI.BidCompare.CompareTasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTaskStateMachineTests
{
    [Fact]
    public void New_Task_Should_Start_As_Parsing()
    {
        var task = new CompareTask(Guid.NewGuid(), "比标任务A");

        task.Status.ShouldBe(CompareTaskStatus.Parsing);
        task.ProgressStage.ShouldBe("parsing");
        task.ProgressPercent.ShouldBe(0);
    }

    [Fact]
    public void Name_Should_Be_Required()
    {
        Should.Throw<ArgumentException>(() => new CompareTask(Guid.NewGuid(), "  "));
    }

    [Fact]
    public void Happy_Path_With_Clause_Confirmation()
    {
        var task = new CompareTask(Guid.NewGuid(), "t");

        task.MarkParsed();
        task.Status.ShouldBe(CompareTaskStatus.Parsed);

        task.MarkAwaitingClauses();
        task.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);

        task.LockClauseSnapshot("[{\"clauseId\":\"c1\"}]");
        task.MarkComparing();
        task.Status.ShouldBe(CompareTaskStatus.Comparing);

        task.MarkAnalyzing();
        task.Status.ShouldBe(CompareTaskStatus.Analyzing);

        task.MarkDone();
        task.Status.ShouldBe(CompareTaskStatus.Done);
    }

    [Fact]
    public void Partial_Should_Behave_Like_Parsed_For_Further_Transitions()
    {
        // spec §9: 单份解析失败 → 部分完成，其余文档照常对比
        var task = new CompareTask(Guid.NewGuid(), "t");

        task.MarkPartial("标书C.pdf: OCR 失败");
        task.Status.ShouldBe(CompareTaskStatus.Partial);
        task.FailureReason.ShouldContain("标书C.pdf");

        task.MarkAwaitingClauses();
        task.MarkComparing();
        task.MarkAnalyzing();
        task.MarkDone();
        task.Status.ShouldBe(CompareTaskStatus.Done);
    }

    [Fact]
    public void Failed_Should_Be_Terminal_From_Parsing_And_Comparing()
    {
        var task1 = new CompareTask(Guid.NewGuid(), "t");
        task1.MarkFailed("全部文档解析失败");
        task1.Status.ShouldBe(CompareTaskStatus.Failed);

        var task2 = new CompareTask(Guid.NewGuid(), "t");
        task2.MarkParsed();
        task2.MarkComparing();
        task2.MarkFailed("算法服务不可用");
        task2.Status.ShouldBe(CompareTaskStatus.Failed);
        task2.FailureReason.ShouldContain("算法服务不可用");
    }

    [Fact]
    public void Invalid_Transitions_Should_Throw_BusinessException()
    {
        var task = new CompareTask(Guid.NewGuid(), "t");

        Should.Throw<BusinessException>(() => task.MarkComparing())
            .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
        Should.Throw<BusinessException>(() => task.MarkDone())
            .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
        Should.Throw<BusinessException>(() => task.MarkAnalyzing())
            .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
    }

    [Fact]
    public void SetTenderDocument_Should_Only_Be_Set_During_Early_Stages()
    {
        var task = new CompareTask(Guid.NewGuid(), "t");
        var docId = Guid.NewGuid();

        task.SetTenderDocument(docId);
        task.TenderDocumentId.ShouldBe(docId);
    }

    [Fact]
    public void SetReport_Should_Require_Done()
    {
        var task = new CompareTask(Guid.NewGuid(), "t");
        Should.Throw<BusinessException>(() => task.SetReport("{}", DateTime.UtcNow))
            .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
    }
}
