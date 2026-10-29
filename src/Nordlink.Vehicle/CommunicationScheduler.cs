namespace Nordlink.Vehicle;

// Every communication request shares one owner. Pending exclusive jobs suppress polling.
public sealed class CommunicationScheduler
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int exclusivePending;
    public bool PollingSuspended => Volatile.Read(ref exclusivePending) > 0;
    public async Task<T> Exclusive<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        Interlocked.Increment(ref exclusivePending);
        try
        {
            await gate.WaitAsync(ct);
            try { return await action(ct); } finally { gate.Release(); }
        }
        finally { Interlocked.Decrement(ref exclusivePending); }
    }
    public async Task<bool> Poll(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (PollingSuspended || !await gate.WaitAsync(0, ct)) return false;
        try { if (PollingSuspended) return false; await action(ct); return true; }
        finally { gate.Release(); }
    }
}
