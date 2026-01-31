using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Application.Agents;

public sealed class DoctorAgent
{
  private readonly ICopilotClient _copilot;
  private readonly IGitClient _git;
  private readonly CopilotSettings _settings;
  private readonly ILogger<DoctorAgent> _logger;

  public DoctorAgent(ICopilotClient copilot, IGitClient git, CopilotSettings settings, ILogger<DoctorAgent> logger)
  {
    _copilot = copilot;
    _git = git;
    _settings = settings;
    _logger = logger;
  }

  public async Task<PreflightReport> ExecuteAsync(CancellationToken cancellationToken)
  {
    var checks = new List<PreflightCheck>();

    var copilotReport = await _copilot.PreflightAsync(cancellationToken);
    checks.AddRange(copilotReport.Checks);

    checks.Add(new PreflightCheck("copilot-enabled", _settings.Enabled, _settings.Enabled ? "Copilot is enabled" : "Copilot disabled via settings/env"));

    try
    {
      var status = await _git.StatusAsync(cancellationToken);
      checks.Add(new PreflightCheck("git-status", true, status.Count == 0 ? "Working tree clean" : "Working tree has changes"));
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Git status check failed");
      checks.Add(new PreflightCheck("git-status", false, ex.Message));
    }

    return new PreflightReport(checks);
  }
}
