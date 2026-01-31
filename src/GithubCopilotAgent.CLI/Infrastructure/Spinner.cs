using System.Threading;

namespace GithubCopilotAgent.CLI.Infrastructure;

public sealed class Spinner : IDisposable
{
    private readonly string _label;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public Spinner(string label)
    {
        _label = label;
        _loop = Task.Run(async () =>
        {
            var frames = new[] {"-", "\\", "|", "/"};
            var i = 0;
            while (!_cts.IsCancellationRequested)
            {
                Console.Write($"\r{_label} {frames[i % frames.Length]} ");
                i++;
                await Task.Delay(120, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);
    }

    public void Ping()
    {
        // Hook for future heartbeat; currently no-op.
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop.Wait(500);
        }
        catch
        {
            // ignored
        }

        Console.Write("\r               \r");
        _cts.Dispose();
    }
}
