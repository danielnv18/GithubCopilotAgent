## Project Overview
Building a .NET 10 solution that exercises the GitHub Copilot SDK to explore code-generation and workflow automation capabilities. The project should demonstrate modern .NET idioms, SOLID-aligned design, and production-ready practices.

## Goals
- Validate Copilot SDK integration points in a .NET 10 environment
- Showcase modern framework features (top-level statements where appropriate, primary constructors, required members, interceptors, AOT readiness)
- Demonstrate clean architecture boundaries and testability
- Provide reference patterns for future Copilot-assisted automation

## Tech & Conventions
- Runtime: .NET 10
- Language: C# 13 (nullable enabled, file-scoped namespaces)
- Project layout: solution-level `src/`, `tests/`, `tools/`
- Package management: `dotnet` CLI; pin versions via `Directory.Packages.props`
- Code style: `dotnet format` + `.editorconfig`; prefer analyzers (IDEs, Roslyn)
- Documentation: XML doc where useful; terse, purposeful comments only for non-obvious logic

## Architecture & Design
- **Boundaries**: Domain, Application, Infrastructure, Web/API; internal abstractions via interfaces where they express variability
- **Composition**: Dependency Injection via `Microsoft.Extensions.DependencyInjection`; favor constructor injection; avoid service locators
- **Patterns**: CQRS for application commands/queries; mediator optional; Strategy/Policy for pluggable behaviors; Options pattern for configuration; Factory methods for SDK client creation
- **SOLID**: Single-responsibility services; interfaces segregated by use; dependencies inverted toward domain contracts; keep side effects isolated
- **Error Handling**: Use `Result`/`OneOf`-style discriminated unions for flow; exceptions only for truly exceptional cases; map to ProblemDetails at API boundary

## Copilot SDK Integration Plan
- Encapsulate SDK usage behind an interface (`ICopilotClient`) with an Infrastructure implementation
- Centralize SDK configuration (keys, endpoints) using `IOptions` and user secrets/local env vars; no secrets in repo
- Provide a thin anti-corruption layer translating domain intents to SDK requests/responses
- Add feature toggles to enable/disable Copilot-powered flows at runtime
- Include resilience (retry with jitter, timeout, cancellation) via `Polly` or built-in handlers

## Testing Strategy
- Unit tests: domain and application layers (no SDK network calls)
- Integration tests: SDK client wrapper with test doubles; consider recording/playback harness if feasible
- Contract tests: validate request/response shapes against SDK expectations
- Tooling: `dotnet test`; use `WebApplicationFactory` for API; prefer deterministic, hermetic tests

## Observability
- Logging: structured logging via `ILogger<T>`; enrich with correlation IDs
- Metrics/Tracing: OpenTelemetry exporters optional; minimal default is OK, keep hooks ready

## Security & Compliance
- Secrets: store outside repo (user secrets, env vars, managed identity when available)
- Inputs: validate/guard; prefer minimal surface area for Copilot prompts; audit logging for SDK-invoked actions
- Data: avoid persisting sensitive prompts/responses unless explicitly required

## Developer Workflow
- Prereqs: .NET 10 SDK
- Commands: `dotnet restore`, `dotnet build`, `dotnet test`
- Quality: run `dotnet format` and analyzers before PRs

## Next Steps
- Scaffold solution and projects under `src/` and `tests/`
- Add Copilot SDK package reference and wrap it behind `ICopilotClient`
- Draft a sample feature (e.g., code suggestion request) exercising the SDK via API and application layers
- Wire baseline tests and CI workflow using `dotnet test`
