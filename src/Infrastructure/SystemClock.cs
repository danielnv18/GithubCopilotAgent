using GithubCopilotAgent.Application;

namespace GithubCopilotAgent.Infrastructure;

public sealed class SystemClock : IClock
{
  public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
