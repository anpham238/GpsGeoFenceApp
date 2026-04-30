namespace MauiApp1.Services.Narration;

public enum NarrationState
{
    Idle,
    Queued,
    Playing,
    Paused,
    Interrupted,
    Completed,
    Skipped
}

public sealed class NarrationQueueManager
{
    private readonly List<NarrationQueueItem> _queue = new();
    private NarrationQueueItem? _currentPlaying;
    private readonly object _sync = new();

    public NarrationState CurrentState { get; private set; } = NarrationState.Idle;
    public IReadOnlyList<NarrationQueueItem> Queue { get { lock (_sync) return _queue.ToList().AsReadOnly(); } }
    public NarrationQueueItem? CurrentPlaying { get { lock (_sync) return _currentPlaying; } }

    public event Action<NarrationQueueItem>? PlayRequested;
    public event Action? QueueEmpty;

    public void EnqueueRange(IEnumerable<NarrationQueueItem> items)
    {
        lock (_sync)
        {
            foreach (var item in items)
            {
                if (_currentPlaying?.PoiId == item.PoiId) continue;
                if (_queue.Any(q => q.PoiId == item.PoiId)) continue;
                _queue.Add(item);
            }

            ReorderQueue();

            if (_currentPlaying == null && _queue.Count > 0)
            {
                PlayNextInternal();
            }
            else
            {
                CurrentState = _queue.Count > 0 ? NarrationState.Queued : NarrationState.Idle;
            }
        }
    }

    public void RemoveExpired(DateTime nowUtc)
    {
        lock (_sync)
        {
            _queue.RemoveAll(q => q.ExpiresAt.HasValue && q.ExpiresAt.Value < nowUtc);
            if (_queue.Count == 0 && _currentPlaying == null)
                CurrentState = NarrationState.Idle;
        }
    }

    public bool TryInterrupt(NarrationQueueItem incoming)
    {
        lock (_sync)
        {
            if (_currentPlaying == null) return false;
            if (!incoming.AllowInterrupt) return false;
            if (incoming.FinalPriorityScore <= _currentPlaying.FinalPriorityScore) return false;

            _queue.Add(_currentPlaying);
            _currentPlaying = incoming;
            _queue.RemoveAll(q => q.PoiId == incoming.PoiId);
            CurrentState = NarrationState.Interrupted;
            ReorderQueue();
        }

        PlayRequested?.Invoke(incoming);
        return true;
    }

    public NarrationQueueItem? CompleteCurrentAndPlayNext()
    {
        lock (_sync)
        {
            if (_currentPlaying != null)
                CurrentState = NarrationState.Completed;
            _currentPlaying = null;
            return PlayNextInternal();
        }
    }

    public void SkipCurrent()
    {
        lock (_sync)
        {
            if (_currentPlaying != null)
            {
                CurrentState = NarrationState.Skipped;
                _currentPlaying = null;
            }
            PlayNextInternal();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _queue.Clear();
            _currentPlaying = null;
            CurrentState = NarrationState.Idle;
        }
    }

    private void ReorderQueue()
    {
        _queue.Sort((a, b) =>
        {
            var sc = b.FinalPriorityScore.CompareTo(a.FinalPriorityScore);
            if (sc != 0) return sc;
            var dc = a.DistanceMeters.CompareTo(b.DistanceMeters);
            if (dc != 0) return dc;
            return a.TriggeredAt.CompareTo(b.TriggeredAt);
        });
    }

    private NarrationQueueItem? PlayNextInternal()
    {
        _queue.RemoveAll(q => q.ExpiresAt.HasValue && q.ExpiresAt.Value < DateTime.UtcNow);

        if (_queue.Count == 0)
        {
            _currentPlaying = null;
            CurrentState = NarrationState.Idle;
            QueueEmpty?.Invoke();
            return null;
        }

        _currentPlaying = _queue[0];
        _queue.RemoveAt(0);
        CurrentState = NarrationState.Playing;

        var item = _currentPlaying;
        Task.Run(() => PlayRequested?.Invoke(item));
        return _currentPlaying;
    }
}
