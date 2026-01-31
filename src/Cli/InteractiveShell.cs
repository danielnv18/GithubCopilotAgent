using System.Text;
using System.Text.RegularExpressions;
using GithubCopilotAgent.Infrastructure;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.InProc;
using Spectre.Console;

namespace GithubCopilotAgent.Cli;

public sealed class InteractiveShell
{
  private readonly CopilotWorkflowFactory _factory;
  private readonly string _workspace;
  private readonly bool _verbose;
  private const string Banner = 
@"/$$$$$$                  /$$ /$$                            /$$$$$$                                  /$$    
 /$$__  $$                | $$|__/                           /$$__  $$                                | $$    
| $$  \__/  /$$$$$$   /$$$$$$$ /$$ /$$$$$$$   /$$$$$$       | $$  \ $$  /$$$$$$   /$$$$$$  /$$$$$$$  /$$$$$$  
| $$       /$$__  $$ /$$__  $$| $$| $$__  $$ /$$__  $$      | $$$$$$$$ /$$__  $$ /$$__  $$| $$__  $$|_  $$_/  
| $$      | $$  \ $$| $$  | $$| $$| $$  \ $$| $$  \ $$      | $$__  $$| $$  \ $$| $$$$$$$$| $$  \ $$  | $$    
| $$    $$| $$  | $$| $$  | $$| $$| $$  | $$| $$  | $$      | $$  | $$| $$  | $$| $$_____/| $$  | $$  | $$ /$$
|  $$$$$$/|  $$$$$$/|  $$$$$$$| $$| $$  | $$|  $$$$$$$      | $$  | $$|  $$$$$$$|  $$$$$$$| $$  | $$  |  $$$$/
 \______/  \______/  \_______/|__/|__/  |__/ \____  $$      |__/  |__/ \____  $$ \_______/|__/  |__/   \___/  
                                             /$$  \ $$                 /$$  \ $$                              
                                            |  $$$$$$/                |  $$$$$$/                               
                                             \______/                  \______/                               ";

  public InteractiveShell(CopilotWorkflowFactory factory, string workspace, bool verbose)
  {
    _factory = factory;
    _workspace = workspace;
    _verbose = verbose;
  }

  public async Task RunAsync()
  {
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[cyan]Copilot multi-agent CLI[/] (type 'exit' to quit)");
    AnsiConsole.WriteLine(Banner);
    if (_verbose)
    {
      AnsiConsole.MarkupLine($"Workspace: {_workspace}");
    }

    var goal = Prompt("Prompt: ");

    while (!string.Equals(goal, "exit", StringComparison.OrdinalIgnoreCase))
    {
      if (string.IsNullOrWhiteSpace(goal))
      {
        goal = Prompt("Prompt: ");
        continue;
      }

      var workflow = await _factory.CreateSequentialWorkflowAsync("interactive-workflow");
      var input = BuildPrompt(goal);

      if (_verbose)
      {
        AnsiConsole.MarkupLine($"\n[grey]Executing workflow for prompt:[/] {goal}\n");
      }

      var combined = await RunWorkflowAsync(workflow, input);

      if (_verbose)
      {
        if (!string.IsNullOrWhiteSpace(combined))
        {
          var path = SaveRun(goal, combined);
          AnsiConsole.MarkupLine($"Saved conversation to [green]{path}[/]\n");
        }
        else
        {
          AnsiConsole.MarkupLine("[yellow]No content generated. Check Copilot credentials/config and try again.[/]\n");
        }
      }

      goal = Prompt("Prompt (or 'exit'): ");
    }
  }

  private static string Prompt(string label)
  {
    return AnsiConsole.Ask<string>("[grey]" + label + "[/]");
  }

  private static string BuildPrompt(string goal)
  {
    var sb = new StringBuilder();
    sb.AppendLine($"Prompt: {goal}");
    sb.AppendLine("Context: You are operating on the local workspace. Use tools to read/write/list/delete files. Keep outputs concise.");
    sb.AppendLine("Finish with a short summary and next steps.");
    return sb.ToString();
  }

  private static async Task<string> RunWorkflowAsync(Workflow workflow, string input)
  {
    try
    {
      await using var run = await InProcessExecution.StreamAsync(workflow, input: input, runId: Guid.NewGuid().ToString("n"), cancellationToken: CancellationToken.None);
      await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

      var buffer = new StringBuilder();
      string? currentAgent = null;

      await foreach (var evt in run.WatchStreamAsync(CancellationToken.None))
      {
        var agentName = GetAgentName(evt);
        if (!string.IsNullOrEmpty(agentName) && !string.Equals(agentName, currentAgent, StringComparison.Ordinal))
        {
          currentAgent = agentName;
          var rule = new Rule($"[blue]{Markup.Escape(currentAgent)}[/]")
          {
            Justification = Justify.Left
          };
          rule = rule.RuleStyle("grey");
          AnsiConsole.Write(rule);
        }

        switch (evt)
        {
          case AgentResponseUpdateEvent update when !string.IsNullOrEmpty(update.Update?.Text):
            var delta = update.Update.Text;
            buffer.Append(delta);
            AnsiConsole.Markup(Markup.Escape(delta));
            break;

          case AgentResponseEvent response when !string.IsNullOrEmpty(response.Response?.Text):
            var text = response.Response.Text;
            buffer.Append(text);
            AnsiConsole.Markup(Markup.Escape(text));
            break;

          case WorkflowErrorEvent error:
            AnsiConsole.MarkupLine($"[red][workflow error][/]: {Markup.Escape(error.ToString())}");
            break;
        }
      }

      Console.WriteLine();
      return buffer.ToString();
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Copilot connection failed:[/] {Markup.Escape(ex.Message)}");
      return string.Empty;
    }
  }

  private static string SaveRun(string goal, string content)
  {
    var docsDir = Path.Combine(AppContext.BaseDirectory, "runs");
    Directory.CreateDirectory(docsDir);

    var slug = Regex.Replace(goal.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    if (string.IsNullOrWhiteSpace(slug))
    {
      slug = "goal";
    }

    var fileName = $"{slug}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
    var path = Path.Combine(docsDir, fileName);

    File.WriteAllText(path, content);
    return path;
  }

  private static string? GetAgentName(object evt)
  {
    var type = evt.GetType();
    var prop = type.GetProperty("AgentName") ?? type.GetProperty("Name") ?? type.GetProperty("AgentId");
    if (prop?.GetValue(evt) is string name && !string.IsNullOrWhiteSpace(name))
    {
      return name;
    }

    var responseProp = type.GetProperty("Response");
    if (responseProp?.GetValue(evt) is { } response)
    {
      var innerType = response.GetType();
      var innerProp = innerType.GetProperty("AgentName") ?? innerType.GetProperty("Name") ?? innerType.GetProperty("AgentId");
      if (innerProp?.GetValue(response) is string innerName && !string.IsNullOrWhiteSpace(innerName))
      {
        return innerName;
      }
    }

    return null;
  }
}
