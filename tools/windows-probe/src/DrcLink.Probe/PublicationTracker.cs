namespace DrcLink.Probe;

// A reported Started state is not evidence of band, open authentication, or pairing.
internal sealed class PublicationTracker
{
    private readonly object _gate = new();
    private bool _everStarted;
    private bool _cleaningUp;
    private readonly TaskCompletionSource<string> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string> _terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<string> FirstState => _first.Task;
    public Task<string> TerminalState => _terminal.Task;
    public bool EverStarted { get { lock (_gate) return _everStarted; } }

    public void Observe(string status)
    {
        lock (_gate)
        {
            if (status == "Started")
            {
                _everStarted = true;
                _first.TrySetResult(status);
            }
            else if (status is "Aborted" or "Stopped")
            {
                _first.TrySetResult(status);
                if (!_cleaningUp) _terminal.TrySetResult(status);
            }
        }
    }

    public void BeginCleanup() { lock (_gate) _cleaningUp = true; }

    public static bool ShouldRequestStop(bool startWasAttempted, string currentStatus)
        => currentStatus == "Started" || (startWasAttempted && currentStatus == "Created");
}
