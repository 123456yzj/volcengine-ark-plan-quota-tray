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

        // UX022 v0.14: with the fixed header / footer removed, the details'
        // four actions live in a details-local context menu on the cards. It
        // reuses the EXACT old handlers — refresh (single-flight via
        // RefreshRequested), copy (snapshot-only formatter via
        // OnCopySummaryClicked), settings (single shared modal path via
        // SettingsRequested) and close (HidePanel) — so there is no new query
        // source and no second clipboard write path. Opening the menu is zero
        // query and suppresses focus-loss hiding; closing re-enables it. The
        // copy action is enabled only while a snapshot is rendered. The shared
        // circle / tray menu (8 items) is untouched.
        private void BuildDetailsMenu()
        {
            _detailsMenu = new ContextMenuStrip();
            _detailsMenu.ShowItemToolTips = true;

            _miRefresh = new ToolStripMenuItem("刷新");
            _miRefresh.AccessibleName = "刷新额度";
            _miRefresh.ToolTipText = "重新查询当前额度（Ctrl+R）";
            _miRefresh.Click += delegate { OnRefreshRequested(); };

            _miCopy = new ToolStripMenuItem("复制摘要");
            _miCopy.AccessibleName = "复制摘要";
            _miCopy.ToolTipText = "复制当前额度摘要到剪贴板（Ctrl+C）";
            _miCopy.Enabled = false;   // enabled once a snapshot is rendered
            _miCopy.Click += delegate { OnCopySummaryClicked(); };

            _miSettings = new ToolStripMenuItem("设置");
            _miSettings.AccessibleName = "设置悬浮窗显示";
            _miSettings.Click += delegate { OnSettingsRequested(); };

            _miClose = new ToolStripMenuItem("关闭");
            _miClose.AccessibleName = "关闭面板（不退出）";
            _miClose.Click += delegate { HidePanel(); };

            _detailsMenu.Items.AddRange(new ToolStripItem[]
                { _miRefresh, _miCopy, _miSettings, _miClose });
            UiStyle.StyleMenu(_detailsMenu);

            _detailsMenu.Opening += delegate
            {
                _miCopy.Enabled = _view != null && _view.Data != null;
                _menuOpen = true;
                _hide.Cancel();
                _hideTimer.Stop();
            };
            _detailsMenu.Closed += delegate { _menuOpen = false; };
            _content.ContextMenuStrip = _detailsMenu;
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

        // UX021 v0.13: details-local accelerators. Only the two exact combos
        // Ctrl+R and Ctrl+C are recognized, and only while the details are
        // visible, enabled and contain keyboard focus with no settings modal
        // or shared menu open. Both reuse the exact button click paths —
        // single-flight refresh (a pending query is never queued) and the
        // snapshot-only copy with its fixed safe strings — so there is no new
        // query source and no second clipboard write path. With no snapshot,
        // Ctrl+C is still consumed but writes nothing and never queries.
        // Everything else (Ctrl+Shift / Ctrl+Alt variants, Esc, hidden or
        // unfocused states, modal / menu open) falls through to base
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
                if (keyData == (Keys.Control | Keys.C))
                {
                    OnCopySummaryClicked(); // no snapshot: guarded, writes nothing
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

        private void OnCancelRequested()
        {
            if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
        }

        private void OnSettingsRequested()
        {
            if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty);
        }

        // UX019: the ONLY clipboard write path, invoked strictly from the
        // explicit copy action (since UX022 the details-local menu item; the
        // old footer button was removed with the chrome) and — since UX021
        // v0.13 — the details-local
        // Ctrl+C accelerator (both explicit user actions; auto refresh / open
        // / render never copy). A clipboard failure shows a short fixed inline
        // "复制失败" as a visible short tooltip (never a blocking dialog) and
        // the user can simply retry.
        private void OnCopySummaryClicked()
        {
            if (_view == null || _view.Data == null) return;   // disabled guard
            if (_copyFeedback != null && _copyFeedback.Enabled) _copyFeedback.Stop();
            string summary = null;
            try { summary = QuotaSummary.Build(_view); }
            catch (Exception) { ShowCopyFeedback("复制失败"); return; }
            if (string.IsNullOrEmpty(summary)) return;
            try { _clipboardSet(summary); ShowCopyFeedback("已复制"); }
            catch (Exception) { ShowCopyFeedback("复制失败"); }
        }

        // UX022 v0.14: copy feedback is a short VISIBLE non-blocking tooltip
        // over the cards ("已复制" / "复制失败", ~2s) — there is no footer
        // button text anymore. It never overwrites an error card, never
        // contains raw upstream text and never queries. The one-shot timer
        // clears the recorded state; stopped and disposed deterministically.
        private void ShowCopyFeedback(string text)
        {
            _copyFeedbackText = text;
            try
            {
                Control anchor = _content.Controls.Count > 0
                    ? _content.Controls[0] : (Control)_content;
                if (_feedbackTip == null) _feedbackTip = new ToolTip();
                _feedbackTip.Show(text, anchor,
                    Math.Max(4, anchor.Width - S(90)), S(8), 2000);
            }
            catch (Exception) { }
            if (_copyFeedback == null)
            {
                _copyFeedback = new System.Windows.Forms.Timer();
                _copyFeedback.Interval = 2000;
                _copyFeedback.Tick += delegate
                {
                    _copyFeedback.Stop();
                    _copyFeedbackText = null;
                };
            }
            _copyFeedback.Start();
        }

        // ---- clipboard / guide (local actions, no network) ----

        private void CopyLoginCommand()
        {
            try
            {
                Clipboard.SetText("arkcli auth login volc-sso");
                // UX022: no footer note line anymore — the action result is a
                // short visible non-blocking tooltip (never a dialog).
                ShowCopyFeedback("已复制登录命令，请在终端粘贴执行。");
            }
            catch (Exception)
            {
                ShowCopyFeedback("剪贴板被占用，复制失败，请手动执行：arkcli auth login volc-sso");
            }
        }

        private void OpenGuide()
        {
            try
            {
                string path = TrayApp.GuidePath();
                if (!System.IO.File.Exists(path))
                {
                    ShowCopyFeedback("未找到设置指南：" + path);
                    return;
                }
                System.Diagnostics.ProcessStartInfo psi =
                    new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "notepad.exe";
                psi.Arguments = "\"" + path + "\"";
                psi.UseShellExecute = false;
                System.Diagnostics.Process.Start(psi);
                ShowCopyFeedback("已用记事本打开：" + path);
            }
            catch (Exception)
            {
                ShowCopyFeedback("无法打开设置指南：" + TrayApp.GuidePath());
            }
        }
    }
}
