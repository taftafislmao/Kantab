using System;

namespace KanTab.ViewModels;

/// <summary>
/// Pure, testable helper that defers a single-click Edit until the double-click window expires.
/// No Avalonia dependency; the view owns the DispatcherTimer that calls <see cref="FlushIfDue"/>.
/// </summary>
public sealed class DeferredClickHandler<T> where T : class
{
    public const double DefaultDoubleClickMs = 350;
    public const double DefaultSingleClickDelayMs = 280;
    public const double DefaultDetailsDedupMs = 500;

    private readonly Func<T, string> _idSelector;
    private readonly double _doubleClickMs;
    private readonly double _delayMs;
    private readonly double _dedupMs;

    private T? _pendingEdit;
    private DateTime _pendingSinceUtc;
    private DateTime _lastPressUtc = DateTime.MinValue;
    private T? _lastPressItem;
    private DateTime _lastDetailsUtc = DateTime.MinValue;

    public DeferredClickHandler(Func<T, string> idSelector, double doubleClickMs = DefaultDoubleClickMs, double delayMs = DefaultSingleClickDelayMs, double dedupMs = DefaultDetailsDedupMs)
    {
        _idSelector = idSelector;
        _doubleClickMs = doubleClickMs;
        _delayMs = delayMs;
        _dedupMs = dedupMs;
    }

    public bool HasPending => _pendingEdit != null;
    public T? Pending => _pendingEdit;

    /// <summary>
    /// Call on PointerPressed. Returns true if this press should open Task Details (double-click).
    /// Simplified: only used for tracking; the view's authoritative double-click now goes via
    /// DoubleTapped. This method still cancels any pending single-click edit when a true double
    /// is detected, but callers should NOT open Details based solely on this return value.
    /// </summary>
    public bool OnPointerPressed(T item, int clickCount, DateTime nowUtc)
    {
        bool isDouble = clickCount == 2 || IsDoubleClickByTime(_lastPressUtc, nowUtc, _lastPressItem, item);
        var wasDouble = isDouble;
        _lastPressUtc = nowUtc;
        _lastPressItem = item;

        if (wasDouble)
        {
            _pendingEdit = null;
            // Legacy dedup retained but secondary — window tracking is authoritative.
            if ((nowUtc - _lastDetailsUtc).TotalMilliseconds <= _dedupMs)
                return false;
            _lastDetailsUtc = nowUtc;
            return true;
        }
        return false;
    }

    /// <summary>Call on DoubleTapped routed event. Authoritative double-click — always cancels pending Edit.</summary>
    public bool OnDoubleTapped(T item, DateTime nowUtc)
    {
        _pendingEdit = null;
        _lastPressUtc = nowUtc;
        _lastPressItem = item;
        // Dedup is intentionally NOT enforced here; KanBanViewModel's window registry is authoritative.
        // The 500ms window dedup previously caused races; now duplicates are prevented by window identity.
        return true;
    }

    public void ScheduleSingleClick(T item, DateTime nowUtc)
    {
        _pendingEdit = item;
        _pendingSinceUtc = nowUtc;
    }

    public void CancelPending() => _pendingEdit = null;

    /// <summary>Call when drag starts; cancels pending edit.</summary>
    public void OnDragStarted() => _pendingEdit = null;

    public bool IsDoubleClickByTime(DateTime lastDownUtc, DateTime nowUtc, T? lastItem, T currentItem)
    {
        if (lastItem == null) return false;
        if (!ReferenceEquals(lastItem, currentItem))
        {
            try
            {
                if (_idSelector(lastItem) != _idSelector(currentItem)) return false;
            }
            catch { return false; }
        }
        var ms = (nowUtc - lastDownUtc).TotalMilliseconds;
        return ms >= 0 && ms <= _doubleClickMs;
    }

    /// <summary>Called by the view's timer tick. Returns the pending item if delay elapsed.</summary>
    public T? FlushIfDue(DateTime nowUtc)
    {
        if (_pendingEdit == null) return null;
        if ((nowUtc - _pendingSinceUtc).TotalMilliseconds >= _delayMs)
        {
            var item = _pendingEdit;
            _pendingEdit = null;
            return item;
        }
        return null;
    }

    /// <summary>Immediate flush for tests / teardown.</summary>
    public T? FlushImmediately()
    {
        var item = _pendingEdit;
        _pendingEdit = null;
        return item;
    }

    // Legacy dedup support retained for tests but no longer used as primary guard.
    public DateTime LastDetailsUtc => _lastDetailsUtc;

    // Static helper used by existing tests
    public static bool IsDoubleClickByTimeStatic(DateTime lastDownUtc, DateTime nowUtc, T? lastItem, T? currentItem, Func<T, string> idSel, double windowMs = DefaultDoubleClickMs)
    {
        if (lastItem == null || currentItem == null) return false;
        if (!ReferenceEquals(lastItem, currentItem))
        {
            try { if (idSel(lastItem) != idSel(currentItem)) return false; } catch { return false; }
        }
        var ms = (nowUtc - lastDownUtc).TotalMilliseconds;
        return ms >= 0 && ms <= windowMs;
    }
}
