using System;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
        private void ShowAndRefresh()
        {
            _controller.Open();
        }

        // Default open entry (start / --show / IPC / tray toggle): show ONLY the
        // floating circle, no query. The details panel opens only on the explicit
        // "查看全部额度" action or a circle click.
        private void ShowFloating()
        {
            _floating.ShowCircle();
            if (_form.Visible) _form.HidePanel();
        }

        // Circle left-click / Enter / Space and the "查看全部额度" menu item:
        // show the details WITHOUT any query. Layout never covers the circle.
        private void ShowDetails()
        {
            if (_menu != null) _menu.Close();
            if (_form.Visible) return;
            // Ensure the circle is initialised/positioned first (a silent marker
            // start has never opened it), so its screen is real. Then let the
            // details complete its layout on the CIRCLE's screen before sizing,
            // and finally place it relative to the circle with the REAL size.
            _floating.ShowCircle();
            _form.ShowPanel(_floating.CircleScreen());
            _floating.PrepareDetails(_form.Size);
            _form.Bounds = _floating.PendingDetailsBounds;
        }

        // UX022 v0.14: consumed after view changes — when the fitted size
        // actually changed while visible, re-run the circle-relative placement
        // with the FINAL size. Zero query; focus / scroll are untouched.
        private void ConsumeDetailsReposition()
        {
            if (!_form.ConsumePendingReposition()) return;
            if (!_form.Visible || _form.IsDisposed || _form.Disposing) return;
            try
            {
                _floating.PrepareDetails(_form.Size);
                _form.Bounds = _floating.PendingDetailsBounds;
            }
            catch (Exception) { }
        }

        // Called when the details panel hides; restores ONLY an auto-hidden
        // circle (a user-hidden circle is never resurrected).
        private void OnDetailsHidden()
        {
            try
            {
                _floating.RestoreAfterDetails();
            }
            catch (Exception) { }
        }

        private void ShowFromMenu()
        {
            ShowDetails();
        }

        // Single shared settings entry for the circle right-click menu, the tray
        // menu and the details-header button. All three use the same
        // single-instance modal inside FloatingQuotaForm (a repeat call
        // activates the existing dialog, never stacks a second one). While the
        // modal is up the details panel's focus-loss hide is suppressed, so
        // returning from the modal never accidentally closes the details.
        // UX015: the visible details panel OWNS the modal (its screen decides
        // DPI / work area, and focus returns to it); otherwise the REAL circle
        // surface owns it (screen / DPI follow the circle — the wrapper form
        // itself is a hidden 1x1 window). With neither visible nothing is
        // forced open. The
        // release is guarded: a repeat request that arrives while the modal is
        // already up returns immediately after activating it — its finally must
        // NOT end the suppression window while the first request is still
        // blocked inside ShowDialog.
        private void OpenSettings()
        {
            IWin32Window owner;
            if (_form.Visible) owner = _form;
            else if (_floating.CircleVisible) owner = _floating.CircleSurface;
            else owner = _floating;
            if (_form.Visible) _form.SetDialogOpen(true);
            try
            {
                _floating.OpenSettings(owner);
            }
            finally
            {
                if (!_floating.SettingsModalOpen) _form.SetDialogOpen(false);
            }
        }

        private void ToggleFloating()
        {
            if (_floating.CircleVisible)
            {
                // Hide circle AND its details together (user intent).
                _floating.HideCircle();
                if (_form.Visible) _form.HidePanel();
            }
            else
            {
                _floating.ShowCircle();
            }
            UpdateToggleText();
        }

        // v0.12 UX020: explicit "悬浮窗归位". Zero query, zero persistence;
        // the position lock never blocks it. While the settings modal is up
        // the handler returns without moving anything (a real user cannot
        // open the menu over the modal either — this keeps a programmatic
        // click equally safe and never moves the modal's owner). If the
        // details are visible they are hidden first via the existing
        // HidePanel path, the stale auto-hide flag is cleared through
        // RestoreAfterDetails, and only then does the circle move home — so
        // position and visibility end up consistent and a later details
        // close can never pull the circle back to its old position. Visible
        // changes stay inside the existing UX013 interval rules (the circle
        // VisibleChanged event re-evaluates the poll interval; no immediate
        // query, no timer restructure).
        private void RepositionFloatingHome()
        {
            if (_floating == null || _floating.IsDisposed) return;
            if (_floating.SettingsModalOpen) return;
            if (_form != null && _form.Visible) _form.HidePanel();
            try { _floating.RestoreAfterDetails(); } catch (Exception) { }
            _floating.RepositionCircleHome();
            UpdateToggleText();
        }

        private void UpdateToggleText()
        {
            if (_menuToggle == null || _floating == null) return;
            _menuToggle.Text = _floating.CircleVisible ? "隐藏悬浮窗" : "显示悬浮窗";
        }

        // v0.8 UX016 / v0.9 UX017: the tray checks always mirror the real
        // states (kept on a failed save, updated on a successful one).
        private void SyncLockChecked()
        {
            if (_menuLock == null || _floating == null) return;
            _menuLock.Checked = _floating.PositionLocked;
            if (_menuMotion != null) _menuMotion.Checked = _floating.ReduceMotion;
        }

        // A failed lock save uses a short tray balloon regardless of circle
        // visibility (fixed text,
        // never a raw exception). Never
        // opens the circle / details, never touches polling or queries; a
        // successful toggle never notifies. Offline tests have no real
        // NotifyIcon - they observe the intent through the counters below.
        private void OnLockSaveFailed()
        {
            if (_disposed) return;
            _lockFailNotifyCount++;
            _lastLockFailText = "位置锁定状态未保存";
            if (_notify == null) return;
            try
            {
                _notify.ShowBalloonTip(2500, "ark_left", _lastLockFailText,
                    ToolTipIcon.Warning);
            }
            catch (Exception) { }
        }

        // v0.9 UX017: a failed reduce-motion save mirrors the lock path but
        // with DISTINCT fixed wording (never claims the lock failed). Circle
        // visibility does not change the tray balloon intent. Never opens the
        // circle / details, never touches polling or
        // queries; a successful toggle never notifies.
        private void OnMotionSaveFailed()
        {
            if (_disposed) return;
            _motionFailNotifyCount++;
            _lastMotionFailText = "减少动画设置未保存";
            if (_notify == null) return;
            try
            {
                _notify.ShowBalloonTip(2500, "ark_left", _lastMotionFailText,
                    ToolTipIcon.Warning);
            }
            catch (Exception) { }
        }

        // v0.15 UX023: a failed 悬浮内容 menu save mirrors the lock / motion
        // path with DISTINCT fixed wording, always through a tray balloon.
        // Never opens the circle /
        // details, never opens a modal, never queries.
        private void OnContentSaveFailed()
        {
            if (_disposed) return;
            _contentFailNotifyCount++;
            _lastContentFailText = "悬浮内容未保存";
            if (_notify == null) return;
            try
            {
                _notify.ShowBalloonTip(2500, "ark_left", _lastContentFailText,
                    ToolTipIcon.Warning);
            }
            catch (Exception) { }
        }

        // v0.5 UX013: while the circle or the details panel is visible, poll at
        // VisiblePollIntervalMs; otherwise fall back to the base interval. Only
        // the Timer interval changes - no immediate query, no timer restart, and
        // a same-value change is skipped so the running countdown is never reset.
        private void UpdatePollInterval()
        {
            if (_disposed) return;
            int interval = (_floating.CircleVisible || _form.Visible)
                ? SnapshotController.VisiblePollIntervalMs
                : SnapshotController.PollIntervalMs;
            if (_poll.Interval != interval) _poll.Interval = interval;
        }

        // Mouse-down captures visibility and opens the activate-suppression window
        // BEFORE any focus-loss from the click can be processed.
        private void OnTrayMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _floating.TrayDown();
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            bool wantsHide = _floating.WantsHideOnTrayClick();
            if (wantsHide) _floating.HideCircle();
            else
            {
                // Tray left-click toggles the circle only (details stays closed);
                // no query, no dependency on details focus state.
                _floating.ShowCircle();
            }
            if (_form.Visible) _form.HidePanel();
            UpdateToggleText();
        }
    }
}
