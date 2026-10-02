using System;
using System.Threading;

namespace Baddel.Services;

/// <summary>Keeps one copy of Baddel running; a second launch asks the first one to show its window.</summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly RegisteredWaitHandle? _registration;

    public SingleInstance(string id)
    {
        _mutex = new Mutex(true, $@"Local\{id}.Instance", out bool createdNew);
        IsFirstInstance = createdNew;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{id}.Activate");
        if (createdNew)
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _activation, (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty), null, Timeout.Infinite, executeOnlyOnce: false);
        }
    }

    public event EventHandler? ActivationRequested;

    public bool IsFirstInstance { get; }

    public void SignalFirstInstance() => _activation.Set();

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activation.Dispose();
        if (IsFirstInstance)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
