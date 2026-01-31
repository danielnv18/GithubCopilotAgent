using System.Text;
using GithubCopilotAgent.Application;
using Microsoft.Extensions.Logging;

using DiagnosticsProcess = System.Diagnostics.Process;

namespace GithubCopilotAgent.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
  private readonly ILogger<ProcessRunner> _logger;

  public ProcessRunner(ILogger<ProcessRunner> logger)
  {
    _logger = logger;
  }

  public async Task<ProcessResult> RunAsync(ProcessLaunchInfo info, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(info);

    using var process = new DiagnosticsProcess
    {
      StartInfo = new System.Diagnostics.ProcessStartInfo
      {
        FileName = info.FileName,
        Arguments = info.Arguments,
        WorkingDirectory = string.IsNullOrWhiteSpace(info.WorkingDirectory) ? Environment.CurrentDirectory : info.WorkingDirectory,
        RedirectStandardInput = info.StandardInput is not null,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      }
    };

    foreach (var kvp in info.EnvironmentVariables)
    {
      process.StartInfo.Environment[kvp.Key] = kvp.Value;
    }

    var stdout = new StringBuilder();
    var stderr = new StringBuilder();

    var stdoutTcs = new TaskCompletionSource<object?>();
    var stderrTcs = new TaskCompletionSource<object?>();

    process.OutputDataReceived += (_, e) =>
    {
      if (e.Data is null)
      {
        stdoutTcs.TrySetResult(null);
      }
      else
      {
        stdout.AppendLine(e.Data);
      }
    };

    process.ErrorDataReceived += (_, e) =>
    {
      if (e.Data is null)
      {
        stderrTcs.TrySetResult(null);
      }
      else
      {
        stderr.AppendLine(e.Data);
      }
    };

    if (!process.Start())
    {
      return new ProcessResult(-1, string.Empty, "Failed to start process.", false);
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    if (info.StandardInput is not null)
    {
      await process.StandardInput.WriteAsync(info.StandardInput);
      await process.StandardInput.FlushAsync();
      process.StandardInput.Close();
    }

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(info.Timeout);

    try
    {
      await Task.WhenAll(
        process.WaitForExitAsync(timeoutCts.Token),
        stdoutTcs.Task,
        stderrTcs.Task);
    }
    catch (OperationCanceledException)
    {
      try
      {
        if (!process.HasExited)
        {
          process.Kill(entireProcessTree: true);
        }
      }
      catch (Exception killEx)
      {
        _logger.LogWarning(killEx, "Failed to kill timed-out process {FileName}", info.FileName);
      }

      return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), true);
    }

    return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
  }
}
