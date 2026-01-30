using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

await using var copilotClient = new CopilotClient();
await copilotClient.StartAsync();

var docOutlineTool = AIFunctionFactory.Create(
    ([Description("Topic to document")] string topic,
     [Description("Primary audience, e.g., 'API consumer' or 'maintainer'")] string audience) =>
    {
        return $"""
# {topic} Documentation

## Summary
- Purpose and scope
- Target audience: {audience}

## Architecture
- Key components
- Data flow and boundaries
- Dependencies and configuration

## Usage
- Setup prerequisites
- Run/build commands
- Key API surface with examples

## Observability
- Logging and metrics hooks
- Health/readiness considerations

## Security
- Inputs/validation
- Secrets/config handling

## Future Work
- Gaps and next steps
""";
    },
    name: "draft_outline",
    description: "Provide a structured Markdown outline for .NET app documentation."
);

var docWriterAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are a concise technical writer for .NET 10 solutions. Produce full Markdown docs that explain architecture, design decisions, and usage with .NET specifics. First call the draft_outline tool to plan, then produce the final doc. Keep answers focused and actionable.",
    name: "doc-writer",
    tools: [docOutlineTool]);

var reviewAgent = new GitHubCopilotAgent(
    copilotClient,
    instructions: "You are a thoughtful reviewer. Critique the previous assistant message for clarity, completeness, and .NET correctness. Respond in Markdown with a short list of improvements.",
    name: "doc-reviewer");

var docWorkflow = AgentWorkflowBuilder.BuildSequential([docWriterAgent, reviewAgent]);

Console.WriteLine("📝  .NET Documentation assistant (type 'exit' to quit)");
Console.WriteLine("   Enter a topic to generate documentation and an automatic review. Examples:");
Console.WriteLine("   - API docs for OrdersController");
Console.WriteLine("   - Architecture overview for CQRS layer\n");

while (true)
{
    Console.Write("Topic: ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    var topic = input.Trim();
    if (string.IsNullOrWhiteSpace(topic))
    {
        Console.WriteLine("Assistant: Please provide a documentation topic.");
        continue;
    }

    Console.WriteLine($"Assistant ({docWriterAgent.Name} -> {reviewAgent.Name}): running workflow for '{topic}'...\n");

    var combined = await RunWorkflowAsync(docWorkflow, topic);

    if (string.IsNullOrWhiteSpace(combined))
    {
        Console.WriteLine("No content was generated. Check Copilot credentials/config and try again.\n");
        continue;
    }

    var path = SaveDocument(topic, combined);
    Console.WriteLine($"\nSaved to {path}\n");
}

static async Task<string> RunWorkflowAsync(Workflow workflow, string topic)
{
    await using var run = await InProcessExecution.StreamAsync(workflow, input: topic);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    var buffer = new StringBuilder();

    await foreach (var evt in run.WatchStreamAsync())
    {
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
        }
    }

    Console.WriteLine();
    return buffer.ToString();
}

static string SaveDocument(string topic, string content)
{
    var docsDir = Path.Combine(AppContext.BaseDirectory, "docs");
    Directory.CreateDirectory(docsDir);

    var slug = Regex.Replace(topic.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    if (string.IsNullOrWhiteSpace(slug))
    {
        slug = "doc";
    }

    var fileName = $"{slug}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
    var path = Path.Combine(docsDir, fileName);

    File.WriteAllText(path, content);
    return path;
}
