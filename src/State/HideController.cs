namespace ArkLeft
{
    // Small, testable helper for the tray toggle / focus-loss race.
    // A tray mouse-down captures the ACTUAL current visibility; the following
    // click consumes that capture exactly once. This prevents the click's own
    // WM_ACTIVATE (or a focus-loss during a long press) from hiding the window
    // before the click decides, which used to make the click re-open it.
    // All timing is external (the form's single WinForms.Timer calls Tick), so
    // the sequence of events is fully unit testable. One mechanism only.
    //
    // Trace model:
    //   Toggle(now, actualVisible) : tray mouse-down; captures visibility and
    //                                cancels any queued focus-loss hide.
    //   ConsumeToggle(actualVisible): tray click; one-shot read + reset. Uses
    //                                 the capture when present, else the
    //                                 caller's current visibility.
    //   Evt()        : a focus-loss (WM_ACTIVATE WA_INACTIVE / OnDeactivate).
    //   Tick()       : the delay timer fired.
    //   Cancel()     : window became active/shown again; clear pending.
    internal class HideController
    {
        public const int HideDelayMs = 220;

        public bool Pending { get { return _pending; } }
        public bool HasToggle { get { return _hasToggle; } }
        public int? Deadline { get { return _hasDeadline ? (int?)_deadlineVal : null; } }

        private bool _pending;
        private bool _hasDeadline;
        private int _deadlineVal;
        private bool _hasToggle;
        private bool _toggleVisible;

        // Starts the activate-suppression window and captures the actual
        // visibility at press time. A focus-loss queued just before the press
        // belongs to the same user action, so it is cleared: the click owns the
        // outcome until ConsumeToggle.
        public void Toggle(int now, bool actualVisible)
        {
            _actUntil = now + HideDelayMs;
            _hasActUntil = true;
            _hasToggle = true;
            _toggleVisible = actualVisible;
            _curVisible = actualVisible;
            _pending = false;
            _hasDeadline = false;
        }

        // One-shot consume of the mouse-down capture. Returns the press-time
        // visibility when a Toggle is outstanding, otherwise the caller-provided
        // current visibility. Always resets the one-shot state so a later click
        // without a mouse-down uses the live visibility, not a stale capture.
        public bool ConsumeToggle(bool actualVisible)
        {
            bool v = _hasToggle ? _toggleVisible : actualVisible;
            _hasToggle = false;
            return v;
        }

        // Called on each real show/hide with the resulting visibility.
        public void State(bool visible, int now)
        {
            _curVisible = visible;
            _hasActUntil = false; // show/hide itself is the "active" signal
            _hasToggle = false;    // a real state change invalidates any capture
            _pending = false;
            _hasDeadline = false;
        }

        // A focus-loss event. Queues a pending hide (deadline now + delay) or
        // extends an existing one; never returns a hide directly. While a tray
        // press is outstanding the click owns the outcome, so no hide is queued
        // (a long hold must not hide before mouse-up).
        public void Evt(int now, bool visible)
        {
            _curVisible = visible;
            if (_hasToggle) return;
            if (!visible) { _pending = false; _hasDeadline = false; return; }
            _pending = true;
            _deadlineVal = now + HideDelayMs;
            _hasDeadline = true;
        }

        // Cancel a pending hide (active regained).
        public void Cancel()
        {
            _pending = false;
            _hasDeadline = false;
        }

        // Evaluate at time now. Returns true only when a hide should occur.
        //  - not pending => false
        //  - within/after an activate-suppression window => keep pending, false
        //  - before the deadline => false
        //  - at/after the deadline with no suppression => true (and clear)
        public bool Tick(int now, bool visible)
        {
            _curVisible = visible;
            if (!_pending) return false;
            if (_hasActUntil && unchecked(now - _actUntil) < 0) return false; // still suppressed
            if (!visible) { _pending = false; _hasDeadline = false; return false; }
            if (_hasDeadline && unchecked(now - _deadlineVal) < 0) return false; // not due yet
            _pending = false;
            _hasDeadline = false;
            return true;
        }

        private bool _curVisible;
        private int _actUntil;
        private bool _hasActUntil;
    }
}
