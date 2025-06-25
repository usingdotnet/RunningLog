using Dapper.Contrib.Extensions;

namespace RunningLog;

[Table("RunData")]
public class RunData
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public double Distance { get; set; }

    public string? Duration { get; set; }

    public string? Pace { get; set; }

    public double HeartRate { get; set; }

    public string VO2Max { get; set; } = "";

    public string? Notes { get; set; }

    public int Cadence { get; set; }
}