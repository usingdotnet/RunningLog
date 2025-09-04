namespace RunningLog;

/// <summary>
/// 月度跑步记录数据实体
/// </summary>
public class MonthlyRunningRecord
{

    /// <summary>
    /// 月份（格式：yyyy-MM）
    /// </summary>
    public string Month { get; set; }

    /// <summary>
    /// 本月跑步天数
    /// </summary>
    public int DaysRun { get; set; }

    /// <summary>
    /// 累计跑步天数
    /// </summary>
    public int CumulativeDaysRun { get; set; }

    /// <summary>
    /// 本月跑步总距离（公里）
    /// </summary>
    public double TotalDistance { get; set; }

    /// <summary>
    /// 累计跑步总距离（公里）
    /// </summary>
    public double CumulativeDistance { get; set; }
}