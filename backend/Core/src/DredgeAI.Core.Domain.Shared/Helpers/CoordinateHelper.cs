namespace DredgeAI;

/// <summary>
/// 坐标计算辅助类
/// </summary>
public static class CoordinateHelper
{
    /// <summary>
    /// 经纬度转web墨卡托
    /// </summary>
    /// <param name="lon">经度</param>
    /// <param name="lat">纬度</param>
    /// <returns>[x, y]</returns>
    public static double[] LL2Mecator(double lon, double lat)
    {
        double x = lon * 20037508.34 / 180;
        double y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
        y = y * 20037508.34 / 180;

        return new double[2] { Math.Round(x, 3), Math.Round(y, 3) };
    }

    /// <summary>
    /// web墨卡托转经纬度
    /// </summary>
    /// <param name="x">经度</param>
    /// <param name="y">纬度</param>
    /// <returns>[x, y]</returns>
    public static double[] Mecator2LL(double x, double y)
    {
        double lon = x / 20037508.34 * 180;
        double lat = y / 20037508.34 * 180;
        lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180)) - Math.PI / 2);

        return new double[2] { Math.Round(lon, 7), Math.Round(lat, 7) };
    }

    /// <summary>
    /// 计算两个点之间的距离
    /// </summary>
    /// <param name="start">起点</param>
    /// <param name="end">终点</param>
    /// <returns></returns>
    public static double CalcTwoPointDistance(double[] start, double[] end)
    {
        if (start.Length != 2) throw new ArgumentException($"参数{nameof(start)}长度必须是2位");
        if (end.Length != 2) throw new ArgumentException($"参数{nameof(end)}长度必须是2位");

        var a = end[0] - start[0];
        var b = end[1] - start[1];

        return Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
    }
}
