using System.Collections.Generic;
using System.Linq;

namespace GithubCopilotAgent.Domain;

public sealed record CopilotRequest(string Kind, string Prompt, string? Payload = null, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CopilotResponse(string Content, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record AgentResult(string Title, string Content, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CopilotSettings
{
  public bool Enabled { get; init; } = true;

  public int TimeoutSeconds { get; init; } = 30;

  public int MaxPromptChars { get; init; } = 8000;

  public bool AllowPlannerCopilot { get; init; } = true;

  public int PlannerMaxFiles { get; init; } = 50;

  public string? AuditPath { get; init; }
}

public sealed record PreflightCheck(string Name, bool Passed, string Detail);

public sealed record PreflightReport(IReadOnlyList<PreflightCheck> Checks)
{
  public bool AllPassed => Checks.All(check => check.Passed);
}

public sealed record GitDiffRequest(bool StagedOnly, IReadOnlyCollection<string> Paths);
