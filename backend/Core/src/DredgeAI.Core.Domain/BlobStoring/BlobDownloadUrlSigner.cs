using System.Security.Cryptography;
using System.Text;

namespace DredgeAI.BlobStoring;

/// <summary>FileSystem blob 签名下载 URL 的签名/验签工具；验签供宿主下载端点调用。</summary>
public static class BlobDownloadUrlSigner
{
    /// <summary>生成 {endpointPath}?key=...&amp;expires=...&amp;sig=...；secret/path 空白抛 InvalidOperationException。</summary>
    public static string BuildUrl(string endpointPath, string secret, string key, long expires)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("BlobFileSystemSigningOptions.SigningSecret 未配置，无法生成签名下载 URL。");
        }
        if (string.IsNullOrWhiteSpace(endpointPath))
        {
            throw new InvalidOperationException("BlobFileSystemSigningOptions.DownloadEndpointPath 未配置，无法生成签名下载 URL。");
        }
        return $"{endpointPath}?key={Uri.EscapeDataString(key)}&expires={expires}&sig={ComputeSignature(secret, key, expires)}";
    }

    /// <summary>验签：secret/signature 空白、已过期、签名不符均 false；常量时间比较。</summary>
    public static bool Validate(string? secret, string key, long expires, string? signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }
        var expectedBytes = Encoding.ASCII.GetBytes(ComputeSignature(secret, key, expires));
        var actualBytes = Encoding.ASCII.GetBytes(signature);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string ComputeSignature(string secret, string key, long expires)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(key + "\n" + expires));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
