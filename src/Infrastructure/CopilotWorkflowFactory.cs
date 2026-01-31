using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Infrastructure;

public sealed class CopilotWorkflowFactory : IAsyncDisposable
{
  private readonly CopilotClient _client;
  private readonly ILogger<CopilotWorkflowFactory> _logger;
  private readonly bool _autoStart;
  private Task? _startTask;

  public CopilotWorkflowFactory(ILogger<CopilotWorkflowFactory> logger, bool autoStart = true)
  {
    _logger = logger;
    _autoStart = autoStart;

    var options = new CopilotClientOptions
    {
      AutoStart = true,
      AutoRestart = true,
      UseStdio = true,
      CliPath = "copilot",
      CliArgs = Array.Empty<string>(),
      Cwd = Environment.CurrentDirectory,
      Logger = logger,
      LogLevel = "error"
    };

    _client = new CopilotClient(options);
  }

  public async Task<IReadOnlyList<AgentDefinition>> CreateAgentsAsync()
  {
    await EnsureStartedAsync().ConfigureAwait(false);

    var (readFile, writeFile, deleteFile, listFiles) = FileSystemTools.Create(Environment.CurrentDirectory, _logger);
    var fileTools = new List<AITool> { readFile, writeFile, deleteFile, listFiles };

    return new List<AgentDefinition>
    {
      new(
        Id: "plan-agent",
        Name: "PlanningAgent",
        Description: "Breaks goals into ordered steps with acceptance criteria",
        Agent: new GitHubCopilotAgent(
          copilotClient: _client,
          ownsClient: false,
          id: "plan-agent",
          name: "PlanningAgent",
          description: "Breaks goals into ordered steps with acceptance criteria",
          tools: Array.Empty<AITool>(),
          instructions: "You are the PlanningAgent. Break the user goal into 4-8 ordered steps with acceptance criteria. Always return a concise plan first before any execution."),
        Tools: Array.Empty<string>()),

      new(
        Id: "exec-agent",
        Name: "ExecutionAgent",
        Description: "Executes steps and edits files based on the plan",
        Agent: new GitHubCopilotAgent(
          copilotClient: _client,
          ownsClient: false,
          id: "exec-agent",
          name: "ExecutionAgent",
          description: "Executes steps and edits files based on the plan",
          tools: fileTools,
          instructions: "You are the ExecutionAgent. Take the plan and perform each step by editing files. Keep changes atomic, respect constraints, and annotate what you changed."),
        Tools: fileTools.Select(t => t.Name ?? string.Empty).ToArray()),

      new(
        Id: "review-agent",
        Name: "ReviewAgent",
        Description: "Reviews the plan and execution output for correctness and safety",
        Agent: new GitHubCopilotAgent(
          copilotClient: _client,
          ownsClient: false,
          id: "review-agent",
          name: "ReviewAgent",
          description: "Reviews the plan and execution output for correctness and safety",
          tools: fileTools,
          instructions: "You are the ReviewAgent. Inspect the plan and execution output. Identify correctness, quality, and safety issues with concrete file references and fixes."),
        Tools: fileTools.Select(t => t.Name ?? string.Empty).ToArray()),

      new(
        Id: "test-agent",
        Name: "TestingAgent",
        Description: "Proposes and runs tests, summarizes results",
        Agent: new GitHubCopilotAgent(
          copilotClient: _client,
          ownsClient: false,
          id: "test-agent",
          name: "TestingAgent",
          description: "Proposes and runs tests, summarizes results",
          tools: fileTools,
          instructions: "You are the TestingAgent. Propose and run tests. Prefer dotnet test. Summarize results and failures succinctly."),
        Tools: fileTools.Select(t => t.Name ?? string.Empty).ToArray()),

      new(
        Id: "docs-agent",
        Name: "DocumentationAgent",
        Description: "Updates or creates concise docs reflecting the implemented goal",
        Agent: new GitHubCopilotAgent(
          copilotClient: _client,
          ownsClient: false,
          id: "docs-agent",
          name: "DocumentationAgent",
          description: "Updates or creates concise docs reflecting the implemented goal",
          tools: fileTools,
          instructions: "You are the DocumentationAgent. Update or create docs (README, usage) reflecting the implemented goal. Keep docs concise and actionable."),
        Tools: fileTools.Select(t => t.Name ?? string.Empty).ToArray())
    };
  }

  public async Task<Workflow> CreateSequentialWorkflowAsync(string workflowName = "plan-exec-review")
  {
    var agents = await CreateAgentsAsync().ConfigureAwait(false);
    return AgentWorkflowBuilder.BuildSequential(workflowName, agents.Select(a => a.Agent));
  }

  private Task EnsureStartedAsync()
  {
    if (!_autoStart)
    {
      _startTask ??= Task.CompletedTask;
      return _startTask;
    }

    _startTask ??= _client.StartAsync();
    return _startTask;
  }

  public ValueTask DisposeAsync()
  {
    return _client.DisposeAsync();
  }
}

public sealed record AgentDefinition(string Id, string Name, string Description, GitHubCopilotAgent Agent, IReadOnlyList<string> Tools);
