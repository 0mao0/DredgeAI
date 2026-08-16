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
}
