using GithubCopilotAgent.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GithubCopilotAgent.Tests;

public class FileSystemToolsTests
{
  [Fact]
  public void NormalizeBlocksTraversal()
  {
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"))) + Path.DirectorySeparatorChar;
    Directory.CreateDirectory(root);

    Assert.Throws<InvalidOperationException>(() => FileSystemTools.Normalize(root, "../../outside.txt"));
  }

  [Fact]
  public async Task ReadWriteListDeleteWithinRoot()
  {
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(root);

    var (read, write, delete, list) = FileSystemTools.Create(root, NullLogger.Instance);

    // Write a file
    await FileSystemTools.WriteTextAsync(Path.Combine(root, "a.txt"), "hello");

    // Read the file
    var text = await FileSystemTools.ReadTextAsync(Path.Combine(root, "a.txt"), 1024);
    Assert.Equal("hello", text);

    // List files
    var entries = FileSystemTools.ListEntries(root + Path.DirectorySeparatorChar, ".", recursive: false);
    Assert.Contains("a.txt", entries);

    // Delete file
    var result = FileSystemTools.DeletePath(Path.Combine(root, "a.txt"));
    Assert.Equal("deleted", result);
  }
}
