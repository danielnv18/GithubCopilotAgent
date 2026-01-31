using System.Text;
using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Application.Agents;

public sealed class PlanAgent
{
  private readonly IGitClient _git;
  private readonly ICopilotClient _copilot;
  private readonly IAuditSink _audit;
  private readonly CopilotSettings _settings;
  private readonly ILogger<PlanAgent> _logger;

  public PlanAgent(
    IGitClient git,
    ICopilotClient copilot,
    IAuditSink audit,
    CopilotSettings settings,
    ILogger<PlanAgent> logger)
  {
    _git = git;
    _copilot = copilot;
    _audit = audit;
    _settings = settings;
    _logger = logger;
  }

  public async Task<AgentResult> ExecuteAsync(CancellationToken cancellationToken)
  {
    var status = await _git.StatusAsync(cancellationToken);
    var context = BuildContext(status);

    if (_settings.Enabled && _settings.AllowPlannerCopilot)
    {
      var prompt = BuildPrompt(context);
      var trimmed = Trim(prompt, _settings.MaxPromptChars);
      var request = new CopilotRequest("plan", trimmed, context);
      var response = await _copilot.SendAsync(request, cancellationToken);
      await _audit.WriteAsync("plan", request, response, cancellationToken);
      return new AgentResult("plan", response.Content, response.Metadata);
    }

    var local = BuildLocalPlan(status);
    return new AgentResult("plan-local", local);
  }

  private static string BuildContext(IReadOnlyList<string> status)
  {
    if (status.Count == 0)
    {
      return "Working tree clean.";
    }

    var sb = new StringBuilder();
    sb.AppendLine("Git status signals:");
    foreach (var line in status.Take(100))
    {
      sb.AppendLine(line);
    }

    return sb.ToString();
  }

  private static string BuildPrompt(string context)
  {
    return $"You are a planner agent. Based on the repository status below, propose the next 3-5 actionable steps with priorities and owners. Keep each step short.\n\n{context}";
  }

  private static string Trim(string value, int max)
  {
    if (value.Length <= max)
    {
      return value;
    }

    var head = value[..Math.Max(0, max - 200)];
    return head + "\n\n[truncated for length]";
  }

  private static string BuildLocalPlan(IReadOnlyList<string> status)
  {
    if (status.Count == 0)
    {
      return "Working tree is clean. Consider running tests or adding a new task.";
    }

    var staged = status.Count(line => line.StartsWith("A ") || line.StartsWith("M ") || line.StartsWith("D "));
    var unstaged = status.Count(line => line.StartsWith(" M") || line.StartsWith(" D") || line.StartsWith("??"));

    return $"Local plan (offline): staged={staged}, unstaged={unstaged}. Next steps: 1) review diffs, 2) run tests, 3) commit or discard.";
  }
}
