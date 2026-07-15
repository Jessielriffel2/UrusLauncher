namespace LegendLauncher.App.Services;

internal sealed class GameAudioService : IDisposable
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(750);

    private readonly object _gate = new();
    private readonly HashSet<int> _processIds = [];
    private readonly Action<IReadOnlySet<int>, bool> _applyMute;
    private readonly TimeSpan _refreshInterval;
    private readonly Timer _timer;
    private bool _isMuted = true;
    private int _isApplying;
    private bool _disposed;

    public GameAudioService()
        : this(CoreAudioSessionController.TrySetMute, DefaultRefreshInterval)
    {
    }

    internal GameAudioService(
        Action<IReadOnlySet<int>, bool> applyMute,
        TimeSpan refreshInterval)
    {
        _applyMute = applyMute ?? throw new ArgumentNullException(nameof(applyMute));
        if (refreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }

        _refreshInterval = refreshInterval;
        _timer = new Timer(static state => ((GameAudioService)state!).RefreshNow(), this,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsMuted
    {
        get
        {
            lock (_gate)
            {
                return _isMuted;
            }
        }
    }

    public void SetMuted(bool isMuted)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _isMuted = isMuted;
        }

        RefreshNow();
    }

    public void RegisterProcess(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        lock (_gate)
        {
            ThrowIfDisposed();
            _processIds.Add(processId);
            _timer.Change(TimeSpan.Zero, _refreshInterval);
        }
    }

    public void UnregisterProcess(int processId)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _processIds.Remove(processId);
            if (_processIds.Count == 0)
            {
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }
    }

    internal void RefreshNow()
    {
        if (Interlocked.Exchange(ref _isApplying, 1) != 0)
        {
            return;
        }

        try
        {
            HashSet<int> snapshot;
            bool muted;
            lock (_gate)
            {
                if (_disposed || _processIds.Count == 0)
                {
                    return;
                }

                snapshot = [.. _processIds];
                muted = _isMuted;
            }

            try
            {
                _applyMute(snapshot, muted);
            }
            catch (Exception exception) when (IsRecoverableAudioFailure(exception))
            {
                // Audio-session discovery is best effort and must never terminate the launcher.
            }
        }
        finally
        {
            Volatile.Write(ref _isApplying, 0);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _processIds.Clear();
        }

        using var callbacksCompleted = new ManualResetEvent(initialState: false);
        if (_timer.Dispose(callbacksCompleted))
        {
            callbacksCompleted.WaitOne();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static bool IsRecoverableAudioFailure(Exception exception) =>
        exception is System.Runtime.InteropServices.COMException or
            InvalidOperationException or UnauthorizedAccessException or ArgumentException;
}
