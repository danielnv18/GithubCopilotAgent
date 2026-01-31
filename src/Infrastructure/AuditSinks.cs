using System.Text.Json;
using GithubCopilotAgent.Application;
using GithubCopilotAgent.Domain;
using Microsoft.Extensions.Logging;

namespace GithubCopilotAgent.Infrastructure;

public sealed class NullAuditSink : IAuditSink
{
  public Task WriteAsync(string kind, CopilotRequest request, CopilotResponse response, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FileAuditSink : IAuditSink
{
  private readonly string _path;
  private readonly IClock _clock;
  private readonly ILogger<FileAuditSink> _logger;

  public FileAuditSink(string path, IClock clock, ILogger<FileAuditSink> logger)
  {
    _path = path;
    _clock = clock;
    _logger = logger;
  }

  public async Task WriteAsync(string kind, CopilotRequest request, CopilotResponse response, CancellationToken cancellationToken)
  {
    try
    {
      var directory = Path.GetDirectoryName(_path);
      if (!string.IsNullOrWhiteSpace(directory))
      {
        Directory.CreateDirectory(directory);
      }

      var payload = new
      {
        kind,
        ts = _clock.UtcNow,
        request,
        response
      };

      var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
      await File.AppendAllTextAsync(_path, json + Environment.NewLine, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to write audit log to {Path}", _path);
    }
  }
}
