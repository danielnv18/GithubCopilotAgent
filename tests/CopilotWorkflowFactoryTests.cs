using GithubCopilotAgent.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GithubCopilotAgent.Tests;

public class CopilotWorkflowFactoryTests
{
  [Fact]
  public async Task CreateAgentsIncludesFileTools()
  {
    await using var factory = new CopilotWorkflowFactory(NullLogger<CopilotWorkflowFactory>.Instance, autoStart: false);

    var agents = await factory.CreateAgentsAsync();

    Assert.Equal(5, agents.Count);

    var exec = agents.Single(a => a.Id == "exec-agent");
    Assert.Contains("fs_read", exec.Tools);
    Assert.Contains("fs_write", exec.Tools);
    Assert.Contains("fs_delete", exec.Tools);
    Assert.Contains("fs_list", exec.Tools);
  }
}
