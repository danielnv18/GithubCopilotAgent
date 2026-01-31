using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Application.Agents;

public sealed class SummarizeAgent
{
  private readonly ICopilotClient _copilot;
  private readonly IAuditSink _audit;
  private readonly CopilotSettings _settings;
  private readonly ILogger<SummarizeAgent> _logger;

  public SummarizeAgent(ICopilotClient copilot, IAuditSink audit, CopilotSettings settings, ILogger<SummarizeAgent> logger)
  {
    _copilot = copilot;
    _audit = audit;
    _settings = settings;
    _logger = logger;
  }

  public async Task<AgentResult> ExecuteAsync(string content, string? path, CancellationToken cancellationToken)
  {
    if (!_settings.Enabled)
    {
      return new AgentResult("copilot-disabled", "Copilot is disabled. Enable via environment variable to run.");
    }

    if (string.IsNullOrWhiteSpace(content))
    {
      return new AgentResult("empty", "No content provided to summarize.");
    }

    var prompt = BuildPrompt(content, path);
    var trimmed = Trim(prompt, _settings.MaxPromptChars);

    var request = new CopilotRequest("summarize", trimmed, content, path is null ? null : new Dictionary<string, string> { { "path", path } });
    var response = await _copilot.SendAsync(request, cancellationToken);
    await _audit.WriteAsync("summarize", request, response, cancellationToken);

    return new AgentResult("summarize", response.Content, response.Metadata);
  }

  private static string BuildPrompt(string content, string? path)
  {
    var location = path is null ? string.Empty : $"File: {path}. ";
    return $"You are summarizing code or text. {location}Provide a concise summary and key risks.\n\n{content}";
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
