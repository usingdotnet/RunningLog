namespace RunningLog;

public class RunRecordParams
{
    public DateTime Date { get; set; }

    public double Distance { get; set; }

    public string Duration { get; set; }

    public int DurationSeconds { get; set; }

    public string Pace { get; set; }

    public int Cadence { get; set; }

    public double HeartRate { get; set; }

    public double HeartRateMax { get; set; }

    public string Vo2max { get; set; }

    public double Temperature { get; set; }

    public double Humidity { get; set; }

    public string TimeOfDay { get; set; }

    public string Place { get; set; }

    public string Notes { get; set; }
}
