using Dapper.Contrib.Extensions;

namespace RunningLog;

public class AppConfig
{
    public bool IsDarkMode { get; set; } = true;

    public string RepoDir { get; set; } = @"E:\Data\MyRunningLog";
}

[Table("RunData")]
public class RunData
{
    public int Id { get; set; }

    public string Date { get; set; }

    public double Distance { get; set; }

    public string Duration { get; set; }

    public double HeartRate { get; set; }

    public string Pace { get; set; }

    public string Notes { get; set; }
}