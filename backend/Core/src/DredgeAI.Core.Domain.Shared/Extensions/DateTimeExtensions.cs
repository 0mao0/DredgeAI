using Volo.Abp;

namespace DredgeAI;

/// <summary>
/// DateTime 扩展方法与时区转换工具。
/// 约定：持久层统一存 UTC；与终端/前端交互的墙钟时间为中国标准时间（UTC+8）。
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// 时间戳开始时间
    /// </summary>
    private static readonly DateTime UtcInitDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>中国标准时间相对 UTC 的固定偏移。</summary>
    public static readonly TimeSpan ChinaTimeOffset = TimeSpan.FromHours(8);

    /// <summary>
    /// 获取当前时间的时间戳
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>精确到秒的时间戳</returns>
    public static long GetTimestamp(this DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new AbpException("时间戳只能转换UTC时间");
        }

        return (time - UtcInitDateTime).TotalSeconds.To<long>();
    }

    /// <summary>
    /// 获取当前时间的时间戳
    /// </summary>
    /// <param name="time">utc时间</param>
    /// <returns>精确到毫秒的时间戳</returns>
    public static long GetTimestampByMilliseconds(this DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new AbpException("时间戳只能转换UTC时间");
        }

        return (time - UtcInitDateTime).TotalMilliseconds.To<long>();
    }

    /// <summary>
    /// 时间戳转时间
    /// </summary>
    /// <param name="timestamp">秒级UTC时间戳</param>
    /// <returns>时间戳对应的时间UTC</returns>
    public static DateTime ToDateTime(this long timestamp)
    {
        return UtcInitDateTime.AddSeconds(timestamp);
    }

    /// <summary>
    /// 时间戳转时间
    /// </summary>
    /// <param name="timestamp">毫秒级UTC时间戳</param>
    /// <returns>时间戳对应的时间UTC</returns>
    public static DateTime ToDateTimeByMilliseconds(this long timestamp)
    {
        return UtcInitDateTime.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// 将时间间隔转化为字符串格式，如：00时01分02秒
    /// </summary>
    /// <param name="timeSpan">时间间隔</param>
    /// <returns></returns>
    public static string ToDisplayText(this TimeSpan timeSpan)
    {
        if (timeSpan.Days > 0)
        {
            return $"{timeSpan.Days:d}天 {timeSpan.Hours:00}时{timeSpan.Minutes:00}分{timeSpan.Seconds:00}秒";
        }

        if (timeSpan.Hours > 0)
        {
            return $"{timeSpan.Hours:00}时{timeSpan.Minutes:00}分{timeSpan.Seconds:00}秒";
        }

        if (timeSpan.Minutes > 0)
        {
            return $"{timeSpan.Minutes:00}分{timeSpan.Seconds:00}秒";
        }

        return $"{timeSpan.Seconds:00}秒";
    }

    /// <summary>
    /// 中国标准时间墙钟 <see cref="DateTime"/>（忽略其 Kind，一律按 UTC+8 墙钟解释）
    /// 转换为等价的 UTC <see cref="DateTimeOffset"/>（Offset 为零）。
    /// </summary>
    public static DateTimeOffset ChinaTimeToUtc(this DateTime dateTime)
        => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), ChinaTimeOffset)
            .ToUniversalTime();
}