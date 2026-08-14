using System;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Controllers;
using DredgeAI.BidCompare.Documents;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Controllers;

public class CompareTaskControllerTests
{
    [Fact]
    public async Task GetDocumentFileAsync_Should_Return_Inline_Preview_File_Not_Attachment()
    {
        var appService = Substitute.For<ICompareTaskAppService>();
        var taskId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        appService.GetDocumentFileAsync(taskId, docId)
            .Returns(new CompareDocumentFileResult
            {
                Content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }),
                ContentType = "application/pdf",
                FileName = "标书A.pdf",
            });

        var controller = new CompareTaskController(appService);
        var action = await controller.GetDocumentFileAsync(taskId, docId);

        var result = action.ShouldBeOfType<FileStreamResult>();
        result.FileDownloadName.ShouldBeNullOrEmpty();
        result.EnableRangeProcessing.ShouldBeTrue();
    }
}
