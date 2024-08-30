namespace RunningLog;

public class AppConfig
{
    public bool IsDarkMode { get; set; } = true;

    public string RepoDir { get; set; } = @"E:\Data\MyRunningLog";
}

public class RunData
{
    public double Distance { get; set; }

    public TimeSpan Duration { get; set; }

    public double HeartRate { get; set; }

    public string Pace { get; set; }

    public string Notes { get; set; }
}