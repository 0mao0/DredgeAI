using System.Security.Cryptography;
using System.Text;

namespace DredgeAI;

public static class ByteExtensions
{
    /// <summary>
    /// 转化为16进制
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string ToHex(this byte[]? bytes)
    {
        if (bytes == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (byte t in bytes)
        {
            sb.Append(t.ToString("X2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 转化为16进制（带0x前缀）
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string ToHexIs0X(this byte[]? bytes)
    {
        var hexString = bytes.ToHex();
        return string.IsNullOrEmpty(hexString) ? hexString : "0x" + hexString;
    }

    public static string ToMd5(this byte[] bytes)
    {
        var hashBytes = MD5.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
