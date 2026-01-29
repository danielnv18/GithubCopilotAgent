# GitHub Copilot SDK Sandbox (.NET 10)

Experimental solution to explore GitHub Copilot SDK capabilities in a modern .NET 10 stack, emphasizing SOLID design, clean architecture boundaries, and production-ready patterns.

## What This Is
- Demonstrates Copilot SDK integration behind an `ICopilotClient` abstraction
- Uses current C# features (top-level statements when appropriate, primary constructors, required members, interceptors, AOT readiness)
- Applies CQRS for application flow and dependency injection for composition

## Project Layout (planned)
- `src/` runtime projects (Domain, Application, Infrastructure, Web/API)
- `tests/` unit and integration coverage; `dotnet test`
- `tools/` ancillary scripts/utilities
- `Directory.Packages.props` for pinned package versions

## Getting Started
1) Install .NET 10 SDK
2) Run `dotnet restore`
3) Run `dotnet build` and `dotnet test`
4) Configure Copilot SDK credentials via user secrets or environment variables (do not commit secrets)

## Quality
- `dotnet format`
- Roslyn analyzers preferred; treat warnings as guidance

## MIT License

```
MIT License

Copyright (c) 2026

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
