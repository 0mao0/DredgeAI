using System.IO;

namespace DredgeAI.BidCompare;

/// <summary>测试用文件内容：满足上传魔数校验（.pdf=%PDF 头）。</summary>
public static class TestFiles
{
    /// <summary>合法 PDF 头的伪文件；marker 用于区分多份内容。</summary>
    public static MemoryStream Pdf(params byte[] extra)
    {
        var bytes = new byte[4 + extra.Length];
        bytes[0] = 0x25; // %
        bytes[1] = 0x50; // P
        bytes[2] = 0x44; // D
        bytes[3] = 0x46; // F
        extra.CopyTo(bytes, 4);
        return new MemoryStream(bytes);
    }

    /// <summary>合法 Word 97-2003（.doc / OLE2）文件头；marker 用于区分多份内容。</summary>
    public static MemoryStream Doc(params byte[] extra)
    {
        var bytes = new byte[8 + extra.Length];
        bytes[0] = 0xD0;
        bytes[1] = 0xCF;
        bytes[2] = 0x11;
        bytes[3] = 0xE0;
        bytes[4] = 0xA1;
        bytes[5] = 0xB1;
        bytes[6] = 0x1A;
        bytes[7] = 0xE1;
        extra.CopyTo(bytes, 8);
        return new MemoryStream(bytes);
    }
}
