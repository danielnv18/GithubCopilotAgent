using GithubCopilotAgent.Domain;

namespace GithubCopilotAgent.Application;

public interface ICopilotClient
{
  Task<CopilotResponse> SendAsync(CopilotRequest request, CancellationToken cancellationToken);

  Task<PreflightReport> PreflightAsync(CancellationToken cancellationToken);
}

public interface IGitClient
{
  Task<IReadOnlyList<string>> StatusAsync(CancellationToken cancellationToken);

  Task<string> DiffAsync(GitDiffRequest request, CancellationToken cancellationToken);
}

public interface IAuditSink
{
  Task WriteAsync(string kind, CopilotRequest request, CopilotResponse response, CancellationToken cancellationToken);
}

public interface IClock
{
  DateTimeOffset UtcNow { get; }
}

public interface IProcessRunner
{
  Task<ProcessResult> RunAsync(ProcessLaunchInfo info, CancellationToken cancellationToken);
}

public sealed record ProcessLaunchInfo(string FileName, string Arguments, string WorkingDirectory, TimeSpan Timeout, IReadOnlyDictionary<string, string?> EnvironmentVariables, string? StandardInput = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
