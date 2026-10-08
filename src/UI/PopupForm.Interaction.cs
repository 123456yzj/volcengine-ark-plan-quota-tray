using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm
    {
        public void SetReduceMotion(bool reduce)
        {
            _reduceMotion = reduce;
            foreach (Control card in _content.Controls)
                SetReduceMotion(card, reduce);
        }

        // v0.24 UX029: re-derive the details surface after a theme change. The
        // cards bake palette colors at build time, so the view is rebuilt with
        // the CURRENT snapshot; the outer frame / content background are
        // restyled too. Zero query; the same snapshot is kept (a null view
        // stays on the empty/message shape).
        public void ApplyTheme()
        {
            Control focus = _dialogOpen ? _dialogFocus : ActiveControl;
            System.Collections.Generic.List<int> focusPath = new System.Collections.Generic.List<int>();
            for (Control c = focus; c != null && c != this; c = c.Parent)
            {
                if (c.Parent == null) { focusPath.Clear(); break; }
                focusPath.Insert(0, c.Parent.Controls.IndexOf(c));
            }
            BackColor = ContentBg;
            if (_content != null) _content.BackColor = ContentBg;
            _displayKey = null; // force a real rebuild with the new palette
            if (_view != null) ApplyModelView(_view);
            else Invalidate(true);
            Control replacement = this;
            foreach (int index in focusPath)
            {
                if (index < 0 || index >= replacement.Controls.Count) { replacement = null; break; }
                replacement = replacement.Controls[index];
            }
            if (replacement != null && replacement != this)
            {
                if (_dialogOpen) _dialogFocus = replacement;
                else if (replacement.CanFocus) replacement.Focus();
            }
        }

        private void SetMotionAllowed(bool allowed)
        {
            foreach (Control card in _content.Controls)
                SetMotionAllowed(card, allowed);
        }

        private static void SetMotionAllowed(Control parent, bool allowed)
        {
            foreach (Control child in parent.Controls)
            {
                QuotaBar bar = child as QuotaBar;
                if (bar != null) bar.SetMotionAllowed(allowed);
                if (child.HasChildren) SetMotionAllowed(child, allowed);
            }
        }

        private static void SetReduceMotion(Control parent, bool reduce)
        {
            foreach (Control child in parent.Controls)
            {
                QuotaBar bar = child as QuotaBar;
                if (bar != null) bar.ReduceMotion = reduce;
                if (child.HasChildren) SetReduceMotion(child, reduce);
            }
        }

        // ---- visibility ----

        public void ShowPanel()
        {
            ShowPanel(Screen.FromPoint(Cursor.Position));
        }

        // Opens the details on an explicit screen (normally the floating
        // circle's screen) so the fixed layout matches that screen's DPI. The
        // parameterless overload keeps the original mouse-screen behaviour.
        public void ShowPanel(Screen screen)
        {
            Screen scr = screen ?? Screen.FromPoint(Cursor.Position);
            _openScreen = scr;
            _openScale = DpiUtil.GetScale(scr);
            if (_content.Controls.Count == 0)
            {
                // First open shows the model's current (loading / waiting)
                // state; opening never queries.
                ApplyModelView(Model.CurrentView);
            }
            if (Math.Abs(_openScale - _scale) > 0.001)
            {
                // UX022: a DPI change must re-derive card padding / fonts /
                // corner radius and the fitted height; the per-card layout
                // cache is invalidated so a same-width card still reflows.
                _scale = _openScale;
                InvalidateCardLayouts();
                RelayoutContent();
            }

            _suppressHide = true;
            try
            {
                // UX022: opening never changes the data, so keep the CURRENT
                // width decision (SizeToFit(false)); a full pass here would
                // re-run the two-pass width flip and rebuild every card on a
                // repeat open. The first build / DPI change already fitted.
                SizeToFit(false);
                if (!Visible) Show();
                _hide.State(true, Environment.TickCount); // clear any pending hide
                _hideTimer.Stop();
                if (!_clock.Enabled) _clock.Start();
                SetMotionAllowed(true);
                SetReduceMotion(_reduceMotion);
                Activate();
                BringToFront();
                // UX022: focus the card (or the first focusable content) on
                // activation; a repeat open that already holds CARD focus is
                // not reset. ContainsFocus alone is true when only the content
                // container holds focus, which must still hand focus to a card.
                if (!CardHasFocus()) FocusFirstContent();
            }
            finally
            {
                _suppressHide = false;
            }
        }

        public void HidePanel()
        {
            _suppressHide = true;
            try
            {
                _hide.State(false, Environment.TickCount);
                _hideTimer.Stop();
                if (Visible) Hide();
                if (_clock.Enabled) _clock.Stop();
                SetMotionAllowed(false);
            }
            finally
            {
                _suppressHide = false;
            }
        }

        // Called by the tray mouse-down BEFORE the click toggles. Captures the
        // ACTUAL visibility at press time, opens the activate-suppression
        // window, cancels any queued focus-loss hide, and stops the hide timer so
        // a long hold cannot hide the panel before mouse-up.
        public void NotifyTrayMouseDown()
        {
            _hide.Toggle(Environment.TickCount, Visible);
            _hideTimer.Stop();
        }

        // The one-shot toggle decision after a tray click. Consumes the capture
        // from the last mouse-down; if no mouse-down preceded this click, the
        // live visibility is used instead of a stale prior capture.
        public bool WantsHideOnTrayClick()
        {
            return _hide.ConsumeToggle(Visible);
        }

        // Suppresses focus-loss hiding while the tray context menu is open.
        public void SetMenuOpen(bool open)
        {
            _menuOpen = open;
            if (open) { _hide.Cancel(); _hideTimer.Stop(); }
        }

        // Suppresses focus-loss hiding while a modal dialog (the shared settings
        // form) is up. Without this, opening the settings from the details
        // header deactivates the details and the hide timer would close it out
        // from under the returned modal. Mirrors SetMenuOpen.
        // UX015: engaging captures the details' active control and scroll
        // offset; releasing proactively restores both so returning from the
        // settings lands back on the original detail state. A repeated engage
        // keeps the FIRST capture (a repeat request must not reset it).
        public void SetDialogOpen(bool open)
        {
            if (open)
            {
                if (_dialogOpen) return;
                _dialogOpen = true;
                try { _dialogFocus = ActiveControl; } catch (Exception) { _dialogFocus = null; }
                try { _dialogScroll = _content.AutoScrollPosition; }
                catch (Exception) { _dialogScroll = Point.Empty; }
                _hide.Cancel(); _hideTimer.Stop();
                return;
            }
            if (!_dialogOpen) return;
            _dialogOpen = false;
            RestoreDialogState();
        }

        // UX015/T042: proactive return to the original details. Order matters:
        // reactivate the panel and restore the effective focus FIRST (focusing
        // can scroll the content on its own), then restore the captured scroll
        // offset LAST — including an explicit restore of zero. A disposing /
        // disposed form is left alone (never reactivated).
        private void RestoreDialogState()
        {
            Control f = _dialogFocus;
            _dialogFocus = null;
            if (IsDisposed || Disposing) return;
            if (Visible) { try { Activate(); } catch (Exception) { } }
            if (f != null && !f.IsDisposed)
            {
                try { ActiveControl = f; } catch (Exception) { }
                if (f.CanFocus) { try { f.Focus(); } catch (Exception) { } }
            }
            try
            {
                Point p = _dialogScroll;
                _content.AutoScrollPosition = new Point(-p.X, -p.Y);
            }
            catch (Exception) { }
        }

        // Shows the slow-response hint by updating only the note/footer fields.
        // Never rebuilds cards, so keyboard focus is preserved.
        public void ShowSlowHint()
        {
            // UX011: progress callbacks must not alter the displayed state.
        }

        // Text-only stage hint; never rebuilds cards (keeps focus).
        public void ShowStageHint(string text)
        {
            // UX011: no process text.
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0006 /* WM_ACTIVATE */)
            {
                int wa = m.WParam.ToInt32() & 0xFFFF;
                if (wa == 0 /* WA_INACTIVE */) OnFocusLost();
                else { _hide.Cancel(); _hideTimer.Stop(); }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            OnFocusLost();
        }

        // Single hide mechanism: queue a pending hide and let the one delay timer
        // decide. Coalesced, cancellable, and never permanently stuck.
        private void OnFocusLost()
        {
            if (_suppressHide || _menuOpen || _dialogOpen) return;
            _hide.Evt(Environment.TickCount, Visible);
            if (_hide.Pending && !_hideTimer.Enabled) _hideTimer.Start();
        }

        private void OnHideTimerTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (_suppressHide || _menuOpen || _dialogOpen) { _hide.Cancel(); return; }
            if (Visible && ContainsFocus) { _hide.Cancel(); return; }
            bool hide = _hide.Tick(Environment.TickCount, Visible);
            if (hide && Visible) HidePanel();
            else if (_hide.Pending) _hideTimer.Start(); // still suppressed: re-check later
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HidePanel();
        }

        // v0.24 UX029: details-local accelerator. Only the exact Ctrl+R combo is
        // recognized, and only while the details are visible, enabled and
        // contain keyboard focus with no settings modal or shared menu open. It
        // reuses the exact refresh path (single-flight; a pending query is never
        // queued). Everything else (Ctrl+Shift / Ctrl+Alt variants, Esc, hidden
        // or unfocused states, modal / menu open) falls through to base
        // processing; no global hotkey and no input injection is involved.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Visible && Enabled && ContainsFocus && !IsDisposed && !Disposing
                && !_dialogOpen && !_menuOpen)
            {
                if (keyData == (Keys.Control | Keys.R))
                {
                    OnRefreshRequested();   // single-flight; pending never queues
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                HidePanel();
                return;
            }
            base.OnFormClosing(e);
        }

        private void OnRefreshRequested()
        {
            if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty);
        }

        // ---- guide (local action, no network) ----

        private void OpenGuide()
        {
            try
            {
                string path = TrayApp.GuidePath();
                if (!System.IO.File.Exists(path)) return;
                System.Diagnostics.ProcessStartInfo psi =
                    new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "notepad.exe";
                psi.Arguments = "\"" + path + "\"";
                psi.UseShellExecute = false;
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception) { }
        }
    }
}
