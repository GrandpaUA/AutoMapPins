namespace AutoMapPins.Icons;

// Batches FileSystemWatcher bursts without blocking Unity's main thread.
internal sealed class IconReloadThrottle
{
    private bool _pending;
    private float _deadline;
    private int _retriesRemaining;

    internal void ScheduleChange(float now)
    {
        _retriesRemaining = 3;
        Schedule(now);
    }

    internal bool RetryAfterFailure(float now)
    {
        if (_retriesRemaining <= 0) return false;
        _retriesRemaining--;
        Schedule(now + 0.65f); // next attempt after 1 second, not every Update frame
        return true;
    }

    internal void Schedule(float now)
    {
        _pending = true;
        _deadline = now + 0.35f;
    }

    internal bool TryConsume(float now)
    {
        if (!_pending || now < _deadline) return false;
        _pending = false;
        return true;
    }

    internal void Clear()
    {
        _pending = false;
        _retriesRemaining = 0;
    }
}
