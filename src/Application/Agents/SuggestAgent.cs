using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Application.Agents;

public sealed class SuggestAgent
{
  private readonly ICopilotClient _copilot;
  private readonly IAuditSink _audit;
  private readonly CopilotSettings _settings;
  private readonly ILogger<SuggestAgent> _logger;

  public SuggestAgent(ICopilotClient copilot, IAuditSink audit, CopilotSettings settings, ILogger<SuggestAgent> logger)
  {
    _copilot = copilot;
    _audit = audit;
    _settings = settings;
    _logger = logger;
  }

  public async Task<AgentResult> ExecuteAsync(string content, string? path, string? range, CancellationToken cancellationToken)
  {
    if (!_settings.Enabled)
    {
      return new AgentResult("copilot-disabled", "Copilot is disabled. Enable via environment variable to run.");
    }

    if (string.IsNullOrWhiteSpace(content))
    {
      return new AgentResult("empty", "No content provided for suggestion.");
    }

    var prompt = BuildPrompt(content, path, range);
    var trimmed = Trim(prompt, _settings.MaxPromptChars);

    var request = new CopilotRequest("suggest", trimmed, content, path is null ? null : new Dictionary<string, string> { { "path", path }, { "range", range ?? string.Empty } });
    var response = await _copilot.SendAsync(request, cancellationToken);
    await _audit.WriteAsync("suggest", request, response, cancellationToken);

    return new AgentResult("suggest", response.Content, response.Metadata);
  }

  private static string BuildPrompt(string content, string? path, string? range)
  {
    var location = path is null ? "" : $"File: {path}. ";
    var rangeText = range is null ? "" : $"Range: {range}. ";
    return $"You are a coding assistant. {location}{rangeText}Provide improved code and rationale. Keep it concise.\n\n{content}";
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
