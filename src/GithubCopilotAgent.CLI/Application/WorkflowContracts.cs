namespace GithubCopilotAgent.CLI.Application;

public sealed record PlanStep(string Title, string Description, string Acceptance);
public sealed record PlanResult(string Goal, IReadOnlyList<PlanStep> Steps);
public sealed record ExecutionResult(string StepTitle, IReadOnlyList<string> Artifacts, string Log);
public sealed record ReviewIssue(string File, string Severity, string Message, string Suggestion);
public sealed record ReviewResult(IReadOnlyList<ReviewIssue> Issues, string Summary);
public sealed record TestResult(string Command, string Output, bool Success);
public sealed record DocumentationResult(IReadOnlyList<string> Files, string Summary);
public sealed record WorkflowState(
    string Goal,
    string Constraints,
    PlanResult? Plan,
    IReadOnlyList<ExecutionResult>? Executions,
    ReviewResult? Review,
    TestResult? Tests,
    DocumentationResult? Documentation);
