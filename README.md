# GitHub Copilot Agent CLI

Self-contained Spectre.Console CLI that orchestrates Copilot CLI for code review, suggestions, summaries, and planning workflows.

## Requirements

- .NET 10 SDK
- Copilot CLI installed and authenticated (`copilot --version`, `copilot auth status`)
- git available for diff/status operations

## Project Layout

- `src/Cli` — Spectre.Console entrypoint and commands
- `src/Application` — agents and abstractions (ICopilotClient, IGitClient, etc.)
- `src/Infrastructure` — Copilot CLI adapter, git adapter, process runner, audit sink
- `tests/` — placeholder for unit/integration/CLI smoke (add as needed)
- `Directory.Packages.props` — pinned package versions

## Configuration (env-first, no config file)

- `COPILOT_ENABLED` (default: true)
- `COPILOT_TIMEOUT_SECONDS` (default: 30)
- `COPILOT_MAX_PROMPT_CHARS` (default: 8000)
- `COPILOT_ALLOW_PLANNER` (default: true)
- `COPILOT_PLANNER_MAX_FILES` (default: 50)
- `COPILOT_AUDIT_PATH` (optional JSONL audit log path)
- `GITHUBCOPILOTAGENT_LOGLEVEL` (default: Information)

## Commands

- `init` — preflight (Copilot install/auth, git status signal)
- `review [--staged] [-p|--path <PATH>...]` — review staged+unstaged diffs by default; `--staged` limits scope
- `suggest --file <PATH> [--range <RANGE>] | --stdin` — code suggestions
- `summarize --file <PATH> | --diff` — summarize file or current diff
- `plan` — planner agent (uses Copilot when allowed)
- `workflow show` — list bundled Copilot SDK agents used for orchestration
- `workflow run [--name <NAME>]` — run the sequential Copilot workflow (default name: plan-exec-review)
- `doctor` — prerequisite and config checks
- `config show` — display effective settings
- Global: `--output json|text`, `--verbose`, `--no-color`

## Build & Run

- Restore/build: `dotnet build src/Cli/GithubCopilotAgent.Cli.csproj`
- Run locally: `dotnet run --project src/Cli/GithubCopilotAgent.Cli.csproj -- review`
- Publish single-file (examples):
  - macOS arm64: `dotnet publish src/Cli/GithubCopilotAgent.Cli.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true`
  - Windows x64: `dotnet publish src/Cli/GithubCopilotAgent.Cli.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`

## Notes

- No global config file is created; defaults come from code + env overrides.
- Copilot CLI is required; the app fails fast if not installed/authenticated.
- Auditing is opt-in via `COPILOT_AUDIT_PATH`.
- Copilot SDK agents are composed via `Microsoft.Agents.AI.GitHub.Copilot` and `Microsoft.Agents.AI.Workflows` with filesystem tools (`fs_read`, `fs_write`, `fs_delete`, `fs_list`) injected by `CopilotWorkflowFactory`.
