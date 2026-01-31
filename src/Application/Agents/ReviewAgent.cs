using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Application.Agents;

public sealed class ReviewAgent
{
  private readonly IGitClient _git;
  private readonly ICopilotClient _copilot;
  private readonly IAuditSink _audit;
  private readonly CopilotSettings _settings;
  private readonly ILogger<ReviewAgent> _logger;

  public ReviewAgent(
    IGitClient git,
    ICopilotClient copilot,
    IAuditSink audit,
    CopilotSettings settings,
    ILogger<ReviewAgent> logger)
  {
    _git = git;
    _copilot = copilot;
    _audit = audit;
    _settings = settings;
    _logger = logger;
  }

  public async Task<AgentResult> ExecuteAsync(bool stagedOnly, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(paths);

    if (!_settings.Enabled)
    {
      return new AgentResult("copilot-disabled", "Copilot is disabled. Enable via environment variable to run.");
    }

    var diffRequest = new GitDiffRequest(stagedOnly, paths);
    var diff = await _git.DiffAsync(diffRequest, cancellationToken);

    if (string.IsNullOrWhiteSpace(diff))
    {
      return new AgentResult("no-diff", "No changes found to review.");
    }

    var prompt = BuildPrompt(diff, stagedOnly, paths);
    var trimmedPrompt = Trim(prompt, _settings.MaxPromptChars);

    var request = new CopilotRequest("review", trimmedPrompt, diff);
    var response = await _copilot.SendAsync(request, cancellationToken);

    await _audit.WriteAsync("review", request, response, cancellationToken);

    return new AgentResult("review", response.Content, response.Metadata);
  }

  private static string BuildPrompt(string diff, bool stagedOnly, IReadOnlyCollection<string> paths)
  {
    var scope = stagedOnly ? "staged changes" : "staged and unstaged changes";
    var pathHint = paths.Count > 0 ? $"Paths: {string.Join(", ", paths)}" : "All paths";

    return $"You are a code reviewer. Review the git diff below and provide concise, prioritized suggestions. Scope: {scope}. {pathHint}. Respond with bullet points and call out risks, tests to add, and quick wins.\n\n{diff}";
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
}
