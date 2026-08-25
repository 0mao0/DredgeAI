namespace DredgeAI;

/// <summary>
/// 可枚举拓展
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// 遍历
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="ts">当前遍历对象</param>
    /// <param name="action">遍历时调用的函数</param>      
    public static void ForEach<T>(this IEnumerable<T> ts, Action<T> action)
        where T : class
    {
        foreach (var item in ts)
        {
            action(item);
        }
    }

    /// <summary>
    /// 异步遍历
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="ts">当前遍历对象</param>
    /// <param name="func">遍历时调用的函数</param>      
    /// <returns>任务对象</returns>
    public static async Task ForEachAsync<T>(this IEnumerable<T> ts, Func<T, Task> func)
        where T : class
    {
        foreach (var item in ts)
        {
            await func(item);
        }
    }
}
