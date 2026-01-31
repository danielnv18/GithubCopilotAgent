using GithubCopilotAgent.Application;
using GithubCopilotAgent.Cli;
using GithubCopilotAgent.Domain;
using GithubCopilotAgent.Infrastructure;
using GithubCopilotAgent.Application.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Collections.Generic;

var services = new ServiceCollection();

var copilotSettings = CopilotSettingsLoader.Load();

services.AddSingleton(copilotSettings);

services.AddLogging(builder =>
{
  builder.AddSimpleConsole(options =>
  {
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
  });
  var minLevel = LogLevelInformationFromEnv();
  builder.SetMinimumLevel(minLevel);
  builder.AddFilter((category, level) =>
  {
    if (string.IsNullOrEmpty(category))
    {
      return level >= minLevel;
    }

    if (category.StartsWith("GithubCopilotAgent.Infrastructure.CopilotWorkflowFactory", StringComparison.Ordinal) ||
        category.StartsWith("GitHub.Copilot.SDK", StringComparison.Ordinal))
    {
      return level >= LogLevel.Error;
    }

    return level >= minLevel;
  });
});

services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<ICopilotClient, CopilotCliClient>();
services.AddSingleton<IGitClient, GitCliClient>();
services.AddSingleton<IAuditSink>(sp =>
{
  if (string.IsNullOrWhiteSpace(copilotSettings.AuditPath))
  {
    return new NullAuditSink();
  }

  return new FileAuditSink(copilotSettings.AuditPath, sp.GetRequiredService<IClock>(), sp.GetRequiredService<ILogger<FileAuditSink>>());
});

services.AddSingleton<ReviewAgent>();
services.AddSingleton<SuggestAgent>();
services.AddSingleton<SummarizeAgent>();
services.AddSingleton<PlanAgent>();
services.AddSingleton<DoctorAgent>();
services.AddSingleton<CopilotWorkflowFactory>();
services.AddSingleton(sp => new InteractiveShell(sp.GetRequiredService<CopilotWorkflowFactory>(), Environment.CurrentDirectory, verbose: false));

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
  config.SetApplicationName("githubcopilotagent");
  config.AddCommand<InitCommand>("init").WithDescription("Run preflight checks");
  config.AddCommand<ReviewCommand>("review").WithDescription("Review git changes with Copilot");
  config.AddCommand<SuggestCommand>("suggest").WithDescription("Request code suggestions");
  config.AddCommand<SummarizeCommand>("summarize").WithDescription("Summarize a file or diff");
  config.AddCommand<PlanCommand>("plan").WithDescription("Generate a plan from repo state");
  config.AddCommand<DoctorCommand>("doctor").WithDescription("Verify prerequisites and configuration");
  config.AddBranch("config", branch =>
  {
    branch.SetDescription("Configuration commands");
    branch.AddCommand<ConfigShowCommand>("show").WithDescription("Show effective settings");
  });
  config.AddBranch("workflow", branch =>
  {
    branch.SetDescription("Workflow orchestration commands");
    branch.AddCommand<WorkflowShowCommand>("show").WithDescription("Show configured Copilot agents/workflow");
    branch.AddCommand<WorkflowRunCommand>("run").WithDescription("Run the sequential Copilot workflow");
  });
});

try
{
  if (args.Length == 0)
  {
    var shell = services.BuildServiceProvider().GetRequiredService<InteractiveShell>();
    await shell.RunAsync();
    return 0;
  }

  return app.Run(args);
}
catch (Exception ex)
{
  Console.Error.WriteLine($"Error: {ex.Message}");
  return 1;
}

static LogLevel LogLevelInformationFromEnv()
{
  var value = Environment.GetEnvironmentVariable("GITHUBCOPILOTAGENT_LOGLEVEL") ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
  if (Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level))
  {
    return level;
  }

  return LogLevel.Warning;
}


internal static class CopilotSettingsLoader
{
  public static CopilotSettings Load()
  {
    var settings = new CopilotSettings();

    if (TryParseBool("COPILOT_ENABLED", out var enabled))
    {
      settings = settings with { Enabled = enabled };
    }

    if (TryParseInt("COPILOT_TIMEOUT_SECONDS", out var timeout))
    {
      settings = settings with { TimeoutSeconds = timeout };
    }

    if (TryParseInt("COPILOT_MAX_PROMPT_CHARS", out var maxChars))
    {
      settings = settings with { MaxPromptChars = maxChars };
    }

    if (TryParseBool("COPILOT_ALLOW_PLANNER", out var allowPlanner))
    {
      settings = settings with { AllowPlannerCopilot = allowPlanner };
    }

    if (TryParseInt("COPILOT_PLANNER_MAX_FILES", out var maxFiles))
    {
      settings = settings with { PlannerMaxFiles = maxFiles };
    }

    var auditPath = Environment.GetEnvironmentVariable("COPILOT_AUDIT_PATH");
    if (!string.IsNullOrWhiteSpace(auditPath))
    {
      settings = settings with { AuditPath = auditPath };
    }

    return settings;
  }

  private static bool TryParseBool(string name, out bool value)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(raw))
    {
      value = default;
      return false;
    }

    return bool.TryParse(raw, out value);
  }

  private static bool TryParseInt(string name, out int value)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(raw))
    {
      value = default;
      return false;
    }

    return int.TryParse(raw, out value);
  }
}
