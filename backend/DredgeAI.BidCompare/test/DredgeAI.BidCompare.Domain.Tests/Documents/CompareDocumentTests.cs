using System;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Documents;

public class CompareDocumentTests
{
    private static CompareDocument NewDoc() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        DocumentRole.Bid,
        "C.docx",
        1024,
        "compare/task/doc/origin.docx");

    [Fact]
    public void MarkParseFailed_With_Long_Error_Should_Truncate_To_2048()
    {
        var doc = NewDoc();

        doc.MarkParseFailed(new string('X', 3000));

        doc.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        doc.ParseError.ShouldNotBeNull();
        doc.ParseError!.Length.ShouldBe(2048);
        doc.ParseError.ShouldStartWith("XXX");
    }
}
