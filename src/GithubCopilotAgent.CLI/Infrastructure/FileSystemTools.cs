using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace GithubCopilotAgent.CLI.Infrastructure;

public interface IFileSystem
{
    string ReadFile(string path);
    string WriteFile(string path, string content);
    string DeleteFile(string path);
    string[] ListFiles(string searchPattern = "**/*");
}

public sealed class DiskFileSystem(string root, bool dryRun = false) : IFileSystem
{
    private readonly string _root = Path.GetFullPath(root);
    private readonly bool _dryRun = dryRun;

    public string ReadFile(string path)
    {
        var fullPath = ResolvePath(path);
        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath)
            : throw new FileNotFoundException($"File not found: {path}");
    }

    public string WriteFile(string path, string content)
    {
        var fullPath = ResolvePath(path);
        if (_dryRun)
        {
            return $"[dry-run] Would write {path} ({content.Length} chars)";
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return $"Wrote {path} ({content.Length} chars)";
    }

    public string DeleteFile(string path)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
        {
            return $"No file deleted; not found: {path}";
        }

        if (_dryRun)
        {
            return $"[dry-run] Would delete {path}";
        }

        File.Delete(fullPath);
        return $"Deleted {path}";
    }

    public string[] ListFiles(string searchPattern = "**/*")
    {
        return Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Select(TrimRoot)
            .Where(p => Matches(searchPattern, p))
            .ToArray();
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            throw new InvalidOperationException("Use paths relative to the workspace root.");
        }

        var combined = Path.GetFullPath(Path.Combine(_root, path));
        if (!combined.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Path escapes the workspace root.");
        }

        return combined;
    }

    private string TrimRoot(string fullPath) => fullPath.Replace(_root, string.Empty).TrimStart(Path.DirectorySeparatorChar);

    private static bool Matches(string pattern, string path)
    {
        // Simple glob: **/* or *.ext supported; keep lightweight for CLI use.
        if (pattern is "**/*" or "*")
        {
            return true;
        }

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var ext = pattern[1..];
            return path.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        return path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

public static class FileSystemTools
{
    public static (AIFunction ReadFile, AIFunction WriteFile, AIFunction DeleteFile, AIFunction ListFiles) Create(IFileSystem fileSystem)
    {
        var read = AIFunctionFactory.Create(
            ([Description("Path relative to workspace root")] string path) => fileSystem.ReadFile(path),
            name: "read_file",
            description: "Read a file from the workspace.");

        var write = AIFunctionFactory.Create(
            ([Description("Path relative to workspace root")] string path,
             [Description("Full file content to write")] string content) => fileSystem.WriteFile(path, content),
            name: "write_file",
            description: "Create or overwrite a file with provided content.");

        var delete = AIFunctionFactory.Create(
            ([Description("Path relative to workspace root")] string path) => fileSystem.DeleteFile(path),
            name: "delete_file",
            description: "Delete a file.");

        var list = AIFunctionFactory.Create(
            ([Description("Glob-like pattern; use **/* for all")] string pattern) => string.Join('\n', fileSystem.ListFiles(pattern)),
            name: "list_files",
            description: "List files in the workspace.");

        return (read, write, delete, list);
    }

    public static AIFunction CreateTestRunner(string workingDirectory)
    {
        return AIFunctionFactory.Create(
            ([Description("Command to run; defaults to 'dotnet test'")] string command) => RunCommand(command, workingDirectory),
            name: "run_tests",
            description: "Run tests (default: dotnet test) and return stdout/stderr.");
    }

    private static string RunCommand(string command, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-lc \"{command}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(180_000);

        output.AppendLine($"[exit code] {process.ExitCode}");
        return output.ToString();
    }
}
