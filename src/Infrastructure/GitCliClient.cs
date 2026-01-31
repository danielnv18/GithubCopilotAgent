using System.Text;
using GithubCopilotAgent.Application;
using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Infrastructure;

public sealed class GitCliClient : IGitClient
{
  private readonly IProcessRunner _runner;
  private readonly ILogger<GitCliClient> _logger;

  public GitCliClient(IProcessRunner runner, ILogger<GitCliClient> logger)
  {
    _runner = runner;
    _logger = logger;
  }

  public async Task<IReadOnlyList<string>> StatusAsync(CancellationToken cancellationToken)
  {
    var result = await _runner.RunAsync(new ProcessLaunchInfo("git", "status -sb", Environment.CurrentDirectory, TimeSpan.FromSeconds(10), new Dictionary<string, string?>()), cancellationToken);

    if (result.ExitCode != 0)
    {
      throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "git status failed" : result.StandardError.Trim());
    }

    var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return lines;
  }

  public async Task<string> DiffAsync(GitDiffRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var pathsArg = request.Paths.Count > 0 ? " " + string.Join(' ', request.Paths.Select(EscapePath)) : string.Empty;

    if (request.StagedOnly)
    {
      var diff = await RunDiffAsync($"diff --cached --no-color --patch{pathsArg}", cancellationToken);
      return diff;
    }

    var staged = await RunDiffAsync($"diff --cached --no-color --patch{pathsArg}", cancellationToken);
    var unstaged = await RunDiffAsync($"diff --no-color --patch{pathsArg}", cancellationToken);

    if (string.IsNullOrWhiteSpace(staged))
    {
      return unstaged;
    }

    if (string.IsNullOrWhiteSpace(unstaged))
    {
      return staged;
    }

    var builder = new StringBuilder();
    builder.AppendLine("# Staged changes");
    builder.AppendLine(staged);
    builder.AppendLine("\n# Unstaged changes");
    builder.AppendLine(unstaged);
    return builder.ToString();
  }

  private async Task<string> RunDiffAsync(string args, CancellationToken cancellationToken)
  {
    var result = await _runner.RunAsync(new ProcessLaunchInfo("git", args, Environment.CurrentDirectory, TimeSpan.FromSeconds(15), new Dictionary<string, string?>()), cancellationToken);

    if (result.ExitCode != 0)
    {
      throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "git diff failed" : result.StandardError.Trim());
    }

    return result.StandardOutput.Trim();
  }

  private static string EscapePath(string path)
  {
    if (path.Contains(' '))
    {
      return $"\"{path}\"";
    }

    return path;
  }
}
