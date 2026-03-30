using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace OptiBat.Services;

/// <summary>
/// Enforces single-instance execution across elevation levels.
/// Uses cross-integrity mutex (same as optiRAM pattern).
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private Mutex? _mutex;
    private EventWaitHandle? _activationSignal;
    private readonly string _mutexName;
    private readonly string _signalName;
    private RegisteredWaitHandle? _waitHandle;

    public SingleInstanceService(string mutexName, string signalName)
    {
        _mutexName = mutexName;
        _signalName = signalName;
    }

    /// <summary>
    /// Try to acquire the single-instance lock.
    /// Returns true if this is the first instance.
    /// </summary>
    public bool TryAcquire()
    {
        _mutex = CreateCrossIntegrityMutex(_mutexName, out var createdNew);

        if (!createdNew)
        {
            // Signal existing instance to activate
            try
            {
                var signal = EventWaitHandle.OpenExisting(_signalName);
                signal.Set();
                signal.Dispose();
            }
            catch { }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Start listening for activation signals from new instances.
    /// </summary>
    public void StartListening(Action callback)
    {
        try
        {
            _activationSignal = new EventWaitHandle(false, EventResetMode.AutoReset, _signalName);
            _waitHandle = ThreadPool.RegisterWaitForSingleObject(
                _activationSignal,
                (_, _) => callback(),
                null, -1, false);
        }
        catch { }
    }

    private static Mutex CreateCrossIntegrityMutex(string name, out bool createdNew)
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            MutexRights.Synchronize | MutexRights.Modify,
            AccessControlType.Allow));
        security.AddAccessRule(new MutexAccessRule(
            WindowsIdentity.GetCurrent().User!,
            MutexRights.FullControl,
            AccessControlType.Allow));

        return MutexAcl.Create(true, $@"Global\{name}", out createdNew, security);
    }

    public void Dispose()
    {
        _waitHandle?.Unregister(null);
        _activationSignal?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
    }
}
