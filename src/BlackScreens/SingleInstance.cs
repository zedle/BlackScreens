namespace BlackScreens;

/// <summary>
/// Holds the single instance mutex and a named event a second launch uses to ask the running copy
/// to show its settings window.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\BlackScreens.SingleInstance";
    private const string ActivateName = @"Local\BlackScreens.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirst = createdNew;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateName);
    }

    public bool IsFirst { get; }

    /// <summary>Called by a second launch to wake the instance that is already running.</summary>
    public void SignalRunningInstance()
    {
        try
        {
            _activate.Set();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Activation signal failed: {ex}");
        }
    }

    /// <summary>Runs <paramref name="handler"/> on a pool thread whenever another launch signals us.</summary>
    public void OnActivationRequested(Action handler)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    handler();
                }
            },
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _registration?.Unregister(null);
        }
        catch
        {
        }

        try
        {
            if (IsFirst)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch
        {
        }

        _mutex.Dispose();
        _activate.Dispose();
    }
}
