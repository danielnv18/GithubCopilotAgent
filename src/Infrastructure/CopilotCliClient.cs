using System.Text.Json;
using GithubCopilotAgent.Application;
using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Infrastructure;

public sealed class CopilotCliClient : ICopilotClient
{
  private readonly IProcessRunner _runner;
  private readonly CopilotSettings _settings;
  private readonly ILogger<CopilotCliClient> _logger;

  public CopilotCliClient(IProcessRunner runner, CopilotSettings settings, ILogger<CopilotCliClient> logger)
  {
    _runner = runner;
    _settings = settings;
    _logger = logger;
  }

  public async Task<CopilotResponse> SendAsync(CopilotRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var info = new ProcessLaunchInfo(
      FileName: "copilot",
      Arguments: "chat --input - --format text",
      WorkingDirectory: Environment.CurrentDirectory,
      Timeout: TimeSpan.FromSeconds(_settings.TimeoutSeconds),
      EnvironmentVariables: new Dictionary<string, string?>
      {
        { "COPILOT_PROMPT_KIND", request.Kind }
      },
      StandardInput: request.Prompt);

    var result = await _runner.RunAsync(info, cancellationToken);

    if (result.TimedOut)
    {
      throw new TimeoutException($"Copilot CLI timed out after {_settings.TimeoutSeconds} seconds.");
    }

    if (result.ExitCode != 0)
    {
      var errorMessage = string.IsNullOrWhiteSpace(result.StandardError) ? "Copilot CLI failed" : result.StandardError.Trim();
      throw new InvalidOperationException(errorMessage);
    }

    var content = string.IsNullOrWhiteSpace(result.StandardOutput)
      ? "(copilot returned no content)"
      : result.StandardOutput.Trim();

    var metadata = request.Metadata is null
      ? new Dictionary<string, string>()
      : new Dictionary<string, string>(request.Metadata);

    metadata["copilot.exitCode"] = result.ExitCode.ToString();

    return new CopilotResponse(content, metadata);
  }

  public async Task<PreflightReport> PreflightAsync(CancellationToken cancellationToken)
  {
    var checks = new List<PreflightCheck>();

    var versionResult = await _runner.RunAsync(
      new ProcessLaunchInfo("copilot", "--version", Environment.CurrentDirectory, TimeSpan.FromSeconds(10), new Dictionary<string, string?>()),
      cancellationToken);
    checks.Add(new PreflightCheck("copilot-cli", versionResult.ExitCode == 0, versionResult.ExitCode == 0 ? versionResult.StandardOutput.Trim() : versionResult.StandardError.Trim()));

    var authResult = await _runner.RunAsync(
      new ProcessLaunchInfo("copilot", "auth status", Environment.CurrentDirectory, TimeSpan.FromSeconds(10), new Dictionary<string, string?>()),
      cancellationToken);
    var authPassed = authResult.ExitCode == 0 && authResult.StandardOutput.Contains("Authenticated", StringComparison.OrdinalIgnoreCase);
    checks.Add(new PreflightCheck("copilot-auth", authPassed, authResult.StandardOutput.Trim().Length > 0 ? authResult.StandardOutput.Trim() : authResult.StandardError.Trim()));

    return new PreflightReport(checks);
  }
}
