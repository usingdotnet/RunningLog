namespace RunningLog;

public class RunDataBase
{
    public double Distance { get; set; }

    public string Duration { get; set; }

    public int DurationSeconds { get; set; }

    /// <summary>
    /// 配速
    /// </summary>
    public string Pace { get; set; }

    /// <summary>
    /// 步频
    /// </summary>
    public int Cadence { get; set; }

    public double HeartRate { get; set; }

    public double HeartRateMax { get; set; }

    public string VO2Max { get; set; }

    public double Temperature { get; set; }

    public double Humidity { get; set; }

    /// <summary>
    /// 时间段，如早晨、上午、下午、晚上
    /// </summary>
    public string TimeOfDay { get; set; }

    public string Place { get; set; }

    public string Notes { get; set; }
}