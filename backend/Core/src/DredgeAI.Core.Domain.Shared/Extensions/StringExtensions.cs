using System.Security.Cryptography;
using System.Text;

namespace DredgeAI;

/// <summary>
/// 字符串拓展
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 判断字符串是否为null、""和空白字符串
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <returns>判断结果</returns>
    public static bool IsNull(this string? str)
    {
        return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
    }

    /// <summary>
    /// 判断字符串是否为可见字符
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <returns>判断结果</returns>
    public static bool IsNotNull(this string? str)
    {
        return !IsNull(str);
    }

    /// <summary>
    /// 根据原字符串长度，创建等长的星号*
    /// </summary>
    /// <param name="str">原字符串</param>
    /// <returns>与原字符串等长的*,如果原字符串为空，那么返回固定6位长度的*</returns>
    public static string ToAsterisk(this string? str)
    {
        if (str.IsNull()) return "******";

        var sb = new StringBuilder();
        foreach (var item in str!)
        {
            sb.Append("*");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 获取字符串SHA256加密后的Base64字符串
    /// </summary>
    /// <param name="str">原字符串</param>
    /// <returns>SHA256加密后的Base64字符串</returns>
    public static string ToSHA256WithBase64(this string str)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(str));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// 将标准的base64字符串转换成jwt格式的base64
    /// </summary>
    /// <param name="base64">原base64字符串</param>
    /// <returns></returns>
    public static string ToJwtBase64(this string base64)
    {
        return base64.Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// 将jwt格式的base64转换成标准的base64字符串
    /// </summary>
    /// <param name="jwtBase64">jwt格式base64字符串</param>
    /// <returns></returns>
    public static string FromJwtBase64(this string jwtBase64)
    {
        return jwtBase64.Replace('-', '+').Replace('_', '/');
    }

    /// <summary>
    /// 获取掩码字符串
    /// </summary>
    /// <param name="value">输入字符串</param>
    /// <param name="pl">前缀明码长度</param>
    /// <param name="sl">后缀明码长度</param>
    /// <remarks>
    /// 默认输入字符串截取3段，中间掩码
    ///     以设置长度为准
    /// </remarks>
    /// <returns></returns>
    public static string? ToAsterisk(this string? value, int? pl = null, int? sl = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        int l = value.Length;
        if (l / 3 == 0)
        {
            return value;
        }

        int p = l / 3;
        int s = l / 3 + (l % 3 >= 1 ? 1 : 0);

        if ((pl + sl) < value.Length)
        {
            if (pl.HasValue)
            {
                p = pl.Value;
            }

            if (sl.HasValue)
            {
                s = sl.Value;
            }
        }

        return $"{value.Substring(0, p)}{new string('*', l - p - s)}{value.Substring(l - s, s)}";
    }

    /// <summary>
    /// url添加参数
    /// </summary>
    /// <param name="url">原url地址</param>
    /// <param name="key">key</param>
    /// <param name="value">value</param>
    /// <returns></returns>
    public static string AddUrlParameter(this string url, string key, string value)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            return url;
        }

        return $"{url}{(url.Contains("?") ? "&" : " ? ")}{key}={value}";
    }
}
