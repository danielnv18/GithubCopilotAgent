using System.Text;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using GithubCopilotAgent.CLI.Infrastructure;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var goalArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.OrdinalIgnoreCase));
var constraintsArg = string.Join(' ', args.Where(a => a.StartsWith("--constraints=", StringComparison.OrdinalIgnoreCase))
    .Select(a => a[("--constraints=".Length)..]));

var goal = string.IsNullOrWhiteSpace(goalArg)
    ? Prompt("Goal: ")
    : goalArg.Trim();

var constraints = string.IsNullOrWhiteSpace(constraintsArg)
    ? Prompt("Constraints (optional): ")
    : constraintsArg.Trim();

var workspaceRoot = Directory.GetCurrentDirectory();
var fileSystem = new DiskFileSystem(workspaceRoot, dryRun);
var (readFileTool, writeFileTool, deleteFileTool, listFilesTool) = FileSystemTools.Create(fileSystem);
var testRunnerTool = FileSystemTools.CreateTestRunner(workspaceRoot);

await using var copilotClient = new CopilotClient();
await copilotClient.StartAsync();

var planningAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are the PlanningAgent. Break the user goal into 4-8 ordered steps with acceptance criteria. Always return a concise plan first before any execution.",
    name: "plan-agent");

var executionAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are the ExecutionAgent. Take the plan and perform each step by editing files. Use the provided tools to read/write/list/delete files. Keep changes atomic, respect constraints, and annotate what you changed.",
    name: "exec-agent",
    tools: [readFileTool, writeFileTool, deleteFileTool, listFilesTool]);

var reviewAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are the ReviewAgent. Inspect the plan and execution output. Identify correctness, quality, and safety issues with concrete file references and fixes.",
    name: "review-agent",
    tools: [readFileTool, listFilesTool]);

var testingAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are the TestingAgent. Propose and run tests. Prefer dotnet test. Summarize results and failures succinctly.",
    name: "test-agent",
    tools: [readFileTool, listFilesTool, testRunnerTool]);

var documentationAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are the DocumentationAgent. Update or create docs (README, usage) reflecting the implemented goal. Keep docs concise and actionable.",
    name: "docs-agent",
    tools: [readFileTool, writeFileTool, listFilesTool]);

var workflow = AgentWorkflowBuilder.BuildSequential([
    planningAgent,
    executionAgent,
    reviewAgent,
    testingAgent,
    documentationAgent
]);

Console.WriteLine("🤖 Copilot multi-agent CLI (type 'exit' to quit)");
Console.WriteLine($"Workspace: {workspaceRoot}");
Console.WriteLine(dryRun ? "Running in dry-run mode (no writes)." : "Live mode (file writes enabled).");

while (!string.Equals(goal, "exit", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(goal))
    {
        goal = Prompt("Goal: ");
        continue;
    }

    var input = BuildPrompt(goal, constraints, dryRun);
    Console.WriteLine($"\nExecuting workflow for: {goal}\n");

    var combined = await RunWorkflowAsync(workflow, input);

    if (!string.IsNullOrWhiteSpace(combined))
    {
        var path = SaveRun(goal, combined);
        Console.WriteLine($"Saved conversation to {path}\n");
    }
    else
    {
        Console.WriteLine("No content was generated. Check Copilot credentials/config and try again.\n");
    }

    goal = Prompt("Goal (or 'exit'): ");
    constraints = Prompt("Constraints (optional): ");
}

static string Prompt(string label)
{
    Console.Write(label);
    return Console.ReadLine() ?? string.Empty;
}

static string BuildPrompt(string goal, string constraints, bool dryRun)
{
    var sb = new StringBuilder();
    sb.AppendLine($"Goal: {goal}");
    if (!string.IsNullOrWhiteSpace(constraints))
    {
        sb.AppendLine($"Constraints: {constraints}");
    }

    sb.AppendLine("Context: You are operating on the local workspace. Use tools to read/write/list/delete files. Keep outputs concise.");
    sb.AppendLine(dryRun ? "Dry-run: do not persist writes; describe intended changes." : "Live mode: writes allowed. Be careful.");
    sb.AppendLine("Finish with a short summary and next steps.");
    return sb.ToString();
}

static async Task<string> RunWorkflowAsync(Workflow workflow, string input)
{
    await using var run = await InProcessExecution.StreamAsync(workflow, input: input);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    using var spinner = StartSpinner("Working");
    var buffer = new StringBuilder();

    await foreach (var evt in run.WatchStreamAsync())
    {
        spinner.Ping();

        switch (evt)
        {
            case AgentResponseUpdateEvent update when !string.IsNullOrEmpty(update.Update?.Text):
                buffer.Append(update.Update.Text);
                Console.Write(update.Update.Text);
                break;

            case AgentResponseEvent response when !string.IsNullOrEmpty(response.Response?.Text):
                buffer.Append(response.Response.Text);
                Console.Write(response.Response.Text);
                break;

            case WorkflowErrorEvent error:
                Console.WriteLine($"[workflow error] {error}");
                break;

            default:
                Console.WriteLine($"[event] {evt.GetType().Name}");
                break;
        }
    }

    Console.WriteLine();
    return buffer.ToString();
}

static string SaveRun(string goal, string content)
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

static Spinner StartSpinner(string label) => new(label);
