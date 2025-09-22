namespace RunningLog;

public class AppConfig
{
    public bool IsDarkMode { get; set; } = true;

    public string Repo { get; set; }

    public string RepoDir { get; set; } = @"E:\Data\MyRunningLog";

    public string MilesRepo { get; set; }

    public string MilesRepoDir { get; set; } = @"E:\Data\MyRunningMiles";

    public string Place1 { get; set; } = "地点1";

    public string Place2 { get; set; } = "地点2";
}