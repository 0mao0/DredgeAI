using System;

namespace DredgeAI.BidCompare.Storage;

/// <summary>上传文件魔数校验：防止改扩展名绕过类型白名单。</summary>
public static class UploadFileSignature
{
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    private static readonly byte[] DocxMagic = { 0x50, 0x4B, 0x03, 0x04 }; // PK zip
    private static readonly byte[] DocMagic = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2 复合文档

    /// <summary>按扩展名校验文件头；header 不足签名长度时判定失败。</summary>
    public static bool Matches(string extension, ReadOnlySpan<byte> header)
        => extension switch
        {
            ".pdf" => header.StartsWith(PdfMagic),
            ".docx" => header.StartsWith(DocxMagic),
            ".doc" => header.StartsWith(DocMagic),
            _ => false
        };
}
