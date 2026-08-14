using System.Text.Json;
using DredgeAI.BidCompare.Ir;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Ir;

public class DocumentIrDtoTests
{
    [Fact]
    public void Deserialize_Should_Accept_Pdf_Date_Format_In_Meta()
    {
        const string json = """
        {
          "schemaVersion":"2.0",
          "docId":"doc-a",
          "meta":{
            "fileName":"海港1.pdf",
            "pageCount":2,
            "author":null,
            "creatorTool":"Adobe Acrobat 9.3.2",
            "createdAt":"D:20251229164720+08'00'",
            "modifiedAt":null
          },
          "pages":[],
          "outline":[],
          "blocks":[]
        }
        """;

        var doc = JsonSerializer.Deserialize<DocumentIrDto>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        doc.ShouldNotBeNull();
        doc.Meta.CreatedAt.ShouldBe("D:20251229164720+08'00'");
        doc.Meta.ModifiedAt.ShouldBeNull();
    }
}
