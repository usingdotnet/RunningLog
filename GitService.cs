using System.IO;
using CliWrap;
using CliWrap.Buffered;
using NLog;

namespace RunningLog;

public class GitService(string repoDir)
{
    public string RepoDir { get; set; } = repoDir;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public async Task<string> GetGitStatus()
    {
        var result = await Cli.Wrap("git")
            .WithArguments("status --porcelain")
            .WithWorkingDirectory(RepoDir)
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    public async Task ExecuteGitCommand(string arguments)
    {
        await Cli.Wrap("git")
            .WithArguments(arguments)
            .WithWorkingDirectory(RepoDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .ExecuteAsync();
    }

    public async Task Pull()
    {
        await ExecuteGitCommand($"pull");
    }

    public async Task CommitChanges(string message)
    {
        await ExecuteGitCommand($"commit -a -m \"{message}\"");
    }

    public async Task PushChanges()
    {
        await ExecuteGitCommand("push");
    }

    public async Task<string> GetUnpushedCommits()
    {
        var result = await Cli.Wrap("git")
            .WithArguments("log @{u}..HEAD --oneline")
            .WithWorkingDirectory(RepoDir)
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    public bool IsGitRepository()
    {
        return Directory.Exists(Path.Combine(RepoDir, ".git"));
    }
}