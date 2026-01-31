using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Infrastructure;

public static class FileSystemTools
{
  public static (AITool readFile, AITool writeFile, AITool deleteFile, AITool listFiles) Create(string root, ILogger logger)
  {
    var normalizedRoot = EnsureTrailingSlash(Path.GetFullPath(root));

    var readFile = AIFunctionFactory.Create(
      async ([Description("Relative path under workspace")] string path, [Description("Max bytes to read (default 131072)")] int? maxBytes) =>
      {
        var fullPath = Normalize(normalizedRoot, path);
        var limit = maxBytes is > 0 and <= 1_000_000 ? maxBytes.Value : 131_072;
        return await ReadTextAsync(fullPath, limit);
      },
      name: "fs_read",
      description: "Read text from a workspace-relative file (UTF-8).");

    var writeFile = AIFunctionFactory.Create(
      async ([Description("Relative path under workspace")] string path, [Description("Content to write (UTF-8)")] string content) =>
      {
        var fullPath = Normalize(normalizedRoot, path);
        await WriteTextAsync(fullPath, content);
        return "ok";
      },
      name: "fs_write",
      description: "Write text to a workspace-relative file (UTF-8, overwrite).");

    var deleteFile = AIFunctionFactory.Create(
      ([Description("Relative path under workspace")] string path) =>
      {
        var fullPath = Normalize(normalizedRoot, path);
        return DeletePath(fullPath);
      },
      name: "fs_delete",
      description: "Delete a file or directory under the workspace.");

    var listFiles = AIFunctionFactory.Create(
      ([Description("Relative path under workspace")] string path, [Description("Include subdirectories")] bool recursive) =>
      {
        return ListEntries(normalizedRoot, path, recursive);
      },
      name: "fs_list",
      description: "List files/directories under the workspace (max 200 entries).");

    return (readFile, writeFile, deleteFile, listFiles);

  }

  internal static async Task<string> ReadTextAsync(string fullPath, int maxBytes)
  {
    await using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
    var buffer = new char[maxBytes];
    var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length));
    return new string(buffer, 0, read);
  }

  internal static Task WriteTextAsync(string fullPath, string content)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    return File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
  }

  internal static string DeletePath(string fullPath)
  {
    if (File.Exists(fullPath))
    {
      File.Delete(fullPath);
      return "deleted";
    }

    if (Directory.Exists(fullPath))
    {
      Directory.Delete(fullPath, recursive: true);
      return "deleted-directory";
    }

    return "not-found";
  }

  internal static string[] ListEntries(string normalizedRoot, string path, bool recursive, int max = 200)
  {
    var fullPath = Normalize(normalizedRoot, path);
    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var entries = Directory.Exists(fullPath)
      ? Directory.EnumerateFileSystemEntries(fullPath, "*", option)
          .Take(max)
          .Select(p => Path.GetRelativePath(normalizedRoot, p))
          .ToArray()
      : Array.Empty<string>();
    return entries;
  }

  internal static string Normalize(string root, string relative)
  {
    var rooted = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
    var combined = Path.GetFullPath(Path.Combine(rooted, relative));
    var prefix = rooted + Path.DirectorySeparatorChar;

    if (!(combined.Equals(rooted, StringComparison.Ordinal) || combined.StartsWith(prefix, StringComparison.Ordinal)))
    {
      throw new InvalidOperationException("Path must stay within workspace root.");
    }

    return combined;
  }

  private static string EnsureTrailingSlash(string path)
  {
    return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
  }
}
