using System.ComponentModel;
using GithubCopilotAgent.Application;
using GithubCopilotAgent.Application.Agents;
using GithubCopilotAgent.Domain;
using GithubCopilotAgent.Infrastructure;
using Microsoft.Agents.AI.Workflows;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GithubCopilotAgent.Cli;

public class GlobalSettings : CommandSettings
{
  [CommandOption("--output <FORMAT>")]
  [DefaultValue("text")]
  public string Output { get; init; } = "text";

  [CommandOption("--verbose")]
  public bool Verbose { get; init; }

  [CommandOption("--no-color")]
  public bool NoColor { get; init; }
}

public sealed class InitCommand : AsyncCommand<GlobalSettings>
{
  private readonly DoctorAgent _doctor;

  public InitCommand(DoctorAgent doctor)
  {
    _doctor = doctor;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var report = await _doctor.ExecuteAsync(CancellationToken.None);
    ResultRenderer.RenderPreflight(report, settings);
    return report.AllPassed ? 0 : 1;
  }
}

public class ReviewSettings : GlobalSettings
{
  [CommandOption("--staged")]
  public bool Staged { get; init; }

  [CommandOption("-p|--path <PATH>")]
  public string[] Paths { get; init; } = Array.Empty<string>();
}

public sealed class ReviewCommand : AsyncCommand<ReviewSettings>
{
  private readonly ReviewAgent _agent;

  public ReviewCommand(ReviewAgent agent)
  {
    _agent = agent;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ReviewSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var result = await _agent.ExecuteAsync(settings.Staged, settings.Paths, CancellationToken.None);
    ResultRenderer.RenderAgent(result, settings);
    return 0;
  }
}

public class SuggestSettings : GlobalSettings
{
  [CommandOption("--file <PATH>")]
  public string? File { get; init; }

  [CommandOption("--range <RANGE>")]
  public string? Range { get; init; }

  [CommandOption("--stdin")]
  public bool UseStdin { get; init; }
}

public sealed class SuggestCommand : AsyncCommand<SuggestSettings>
{
  private readonly SuggestAgent _agent;

  public SuggestCommand(SuggestAgent agent)
  {
    _agent = agent;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, SuggestSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var content = await InputLoader.LoadAsync(settings.File, settings.UseStdin, CancellationToken.None);
    var result = await _agent.ExecuteAsync(content, settings.File, settings.Range, CancellationToken.None);
    ResultRenderer.RenderAgent(result, settings);
    return 0;
  }
}

public class SummarizeSettings : GlobalSettings
{
  [CommandOption("--file <PATH>")]
  public string? File { get; init; }

  [CommandOption("--diff")]
  public bool Diff { get; init; }
}

public sealed class SummarizeCommand : AsyncCommand<SummarizeSettings>
{
  private readonly SummarizeAgent _agent;
  private readonly IGitClient _git;

  public SummarizeCommand(SummarizeAgent agent, IGitClient git)
  {
    _agent = agent;
    _git = git;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, SummarizeSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    string content;
    string? path = settings.File;

    if (settings.Diff)
    {
      var diff = await _git.DiffAsync(new GitDiffRequest(false, Array.Empty<string>()), CancellationToken.None);
      content = diff;
      path = null;
    }
    else
    {
      content = await InputLoader.LoadAsync(settings.File, useStdin: false, CancellationToken.None);
    }

    var result = await _agent.ExecuteAsync(content, path, CancellationToken.None);
    ResultRenderer.RenderAgent(result, settings);
    return 0;
  }
}

public class PlanSettings : GlobalSettings
{
}

public sealed class PlanCommand : AsyncCommand<PlanSettings>
{
  private readonly PlanAgent _agent;

  public PlanCommand(PlanAgent agent)
  {
    _agent = agent;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, PlanSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var result = await _agent.ExecuteAsync(CancellationToken.None);
    ResultRenderer.RenderAgent(result, settings);
    return 0;
  }
}

public class DoctorSettings : GlobalSettings
{
}

public sealed class DoctorCommand : AsyncCommand<DoctorSettings>
{
  private readonly DoctorAgent _doctor;

  public DoctorCommand(DoctorAgent doctor)
  {
    _doctor = doctor;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, DoctorSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var report = await _doctor.ExecuteAsync(CancellationToken.None);
    ResultRenderer.RenderPreflight(report, settings);
    return report.AllPassed ? 0 : 1;
  }
}

public class ConfigShowSettings : GlobalSettings
{
}

public sealed class ConfigShowCommand : Command<ConfigShowSettings>
{
  private readonly CopilotSettings _settings;

  public ConfigShowCommand(CopilotSettings settings)
  {
    _settings = settings;
  }

  public override int Execute(CommandContext context, ConfigShowSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    ResultRenderer.RenderSettings(_settings, settings);
    return 0;
  }
}

public sealed class WorkflowShowCommand : AsyncCommand<GlobalSettings>
{
  private readonly CopilotWorkflowFactory _factory;

  public WorkflowShowCommand(CopilotWorkflowFactory factory)
  {
    _factory = factory;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var agents = await _factory.CreateAgentsAsync();

    if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
      var json = System.Text.Json.JsonSerializer.Serialize(agents.Select(a => new { a.Id, a.Name, a.Description }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
      AnsiConsole.WriteLine(json);
      return 0;
    }

    var table = new Table().Border(TableBorder.Minimal);
    table.AddColumn("Id");
    table.AddColumn("Name");
    table.AddColumn("Description");
    table.AddColumn("Tools");

    foreach (var agent in agents)
    {
      var toolNames = agent.Tools ?? Array.Empty<string>();
      table.AddRow(agent.Id, agent.Name, agent.Description, string.Join(", ", toolNames));
    }

    AnsiConsole.Write(table);
    return 0;
  }
}

public sealed class WorkflowRunSettings : GlobalSettings
{
  [CommandOption("--name <NAME>")]
  [DefaultValue("plan-exec-review")]
  public string Name { get; init; } = "plan-exec-review";
}

public sealed class WorkflowRunCommand : AsyncCommand<WorkflowRunSettings>
{
  private readonly CopilotWorkflowFactory _factory;

  public WorkflowRunCommand(CopilotWorkflowFactory factory)
  {
    _factory = factory;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, WorkflowRunSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var workflow = await _factory.CreateSequentialWorkflowAsync(settings.Name);
    var runId = Guid.NewGuid().ToString("n");
    await using var run = await InProcessExecution.RunAsync(workflow, input: new object(), runId: runId, cancellationToken: CancellationToken.None);

    if (settings.Verbose)
    {
      AnsiConsole.MarkupLine($"[green]Workflow '{workflow.Name ?? settings.Name}' executed.[/] runId={runId}");
    }
    return 0;
  }
}

internal static class InputLoader
{
  public static async Task<string> LoadAsync(string? path, bool useStdin, CancellationToken cancellationToken)
  {
    if (useStdin)
    {
      using var reader = new StreamReader(Console.OpenStandardInput());
      return await reader.ReadToEndAsync(cancellationToken);
    }

    if (!string.IsNullOrWhiteSpace(path))
    {
      return await File.ReadAllTextAsync(path, cancellationToken);
    }

    throw new InvalidOperationException("Provide --file or --stdin.");
  }
}

internal static class ResultRenderer
{
  public static void RenderAgent(AgentResult result, GlobalSettings settings)
  {
    if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
      var payload = new { result.Title, result.Content, result.Metadata };
      var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
      AnsiConsole.WriteLine(json);
      return;
    }

    var panel = new Panel(result.Content)
    {
      Header = new PanelHeader(result.Title),
      Padding = new Padding(1, 1)
    };

    AnsiConsole.Write(panel);
  }

  public static void RenderPreflight(PreflightReport report, GlobalSettings settings)
  {
    if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
      var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
      AnsiConsole.WriteLine(json);
      return;
    }

    var table = new Table().Border(TableBorder.Minimal);
    table.AddColumn("Check");
    table.AddColumn("Status");
    table.AddColumn("Detail");

    foreach (var check in report.Checks)
    {
      table.AddRow(check.Name, check.Passed ? "[green]OK[/]" : "[red]FAIL[/]", check.Detail);
    }

    AnsiConsole.Write(table);
  }

  public static void RenderSettings(CopilotSettings config, GlobalSettings settings)
  {
    if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
      var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
      AnsiConsole.WriteLine(json);
      return;
    }

    var table = new Table().Border(TableBorder.Minimal);
    table.AddColumn("Key");
    table.AddColumn("Value");

    table.AddRow("copilot.enabled", config.Enabled.ToString());
    table.AddRow("copilot.timeoutSeconds", config.TimeoutSeconds.ToString());
    table.AddRow("copilot.maxPromptChars", config.MaxPromptChars.ToString());
    table.AddRow("copilot.allowPlannerCopilot", config.AllowPlannerCopilot.ToString());
    table.AddRow("copilot.plannerMaxFiles", config.PlannerMaxFiles.ToString());
    table.AddRow("copilot.auditPath", config.AuditPath ?? "(none)");

    AnsiConsole.Write(table);
  }
}
