using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private static void PopupDisplayCases()
        {
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                // v0.14 UX022 card-only popup: no chrome at all — exactly one
                // direct child (the card content), a details-local context
                // menu with EXACTLY the four reused actions, copy disabled
                // before any snapshot, and no outer padding.
                System.Windows.Forms.ContextMenuStrip menu = form.DetailsMenuForTest;
                Check("ux022.contentOnly", form.Controls.Count == 1
                    && ReferenceEquals(form.Controls[0], form.ContentForTest), true);
                Check("ux022.contentPadding", form.ContentForTest.Padding.All, 0);
                Check("ux022.menuPresent", menu != null && menu.Items.Count == 4, true);
                Check("ux022.menuActions", form.RefreshMenuItemForTest != null
                    && form.CopyMenuItemForTest != null
                    && form.SettingsMenuItemForTest != null
                    && form.CloseMenuItemForTest != null, true);
                // Only refresh / copy advertise the local shortcuts; settings /
                // close keep their accessible names.
                Check("ux022.menuTooltips", form.RefreshMenuItemForTest.ToolTipText
                    == "重新查询当前额度（Ctrl+R）"
                    && form.CopyMenuItemForTest.ToolTipText
                    == "复制当前额度摘要到剪贴板（Ctrl+C）", true);
                Check("ux022.menuAccessible", form.SettingsMenuItemForTest.AccessibleName
                    == "设置悬浮窗显示"
                    && form.CloseMenuItemForTest.AccessibleName == "关闭面板（不退出）", true);
                Check("ux022.copyDisabledNoSnapshot", form.CopyMenuItemForTest.Enabled, false);
                form.ApplyModelView(form.Model.CurrentView);
                Check("ui.empty", form.ContentControls[0].Controls[0].Text, "暂无数据");
                System.Windows.Forms.Control empty = form.ContentControls[0].Controls[0];
                form.BeginAuth();
                Check("ui.emptyWaitSame", ReferenceEquals(empty, form.ContentControls[0].Controls[0]), true);
                QuotaSnapshot snap = SyntheticSample.BuildLarge();
                form.ForceRender(snap);
                string key = PopupForm.DisplayKey(form.Model.CurrentView);
                System.Windows.Forms.Control card = form.ContentControls[0];
                System.Windows.Forms.Control child = card.Controls[0];
                QuotaSnapshot same = SyntheticSample.BuildLarge(); same.FetchedAt = snap.FetchedAt.AddHours(1);
                form.ForceRender(same);
                Check("ui.fetchedIgnored", PopupForm.DisplayKey(form.Model.CurrentView), key);
                Check("ui.sameCard", ReferenceEquals(card, form.ContentControls[0]), true);
                Check("ui.sameChild", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                // UX022: cards are keyboard-focusable; the update time lives
                // on the cards' tooltip (no footer).
                Check("ux022.cardSelectable", card.TabStop, true);
                Check("ux022.updateTooltipPresent", form.UpdateTimeTextForTest != null, true);
                string time = form.UpdateTimeTextForTest;
                form.ForceError(QuotaStatus.Failed, "失败");
                Check("ui.failureSameChild", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                Check("ui.failureSameTime", form.UpdateTimeTextForTest, time);
                same.Products[0].Periods[0].RemainingPercent = 12;
                form.ForceRender(same);
                Check("ui.rawPercentIgnored", ReferenceEquals(card, form.ContentControls[0]), true);
                same.Products[0].Periods[2].RemainingAmount = 50;
                form.ForceRender(same);
                Check("ui.changedReplaced", ReferenceEquals(card, form.ContentControls[0]), false);
                Check("ui.changedDisposed", card.IsDisposed, true);
                same.Products[0].SubscribedKnown = false;
                form.ForceRender(same);
                Check("ui.subscriptionKnownCompared", PopupForm.DisplayKey(form.Model.CurrentView) == key, false);
                Check("ui.unknownSubscription", ContainsText(form.ContentControls[0], "订阅状态未知"), true);
                same.Products[0].Periods[0].PercentKnown = false;
                same.Products[0].Periods[0].AmountKnown = false;
                form.ForceRender(same);
                Check("ui.unknownPercent", ContainsText(form.ContentControls[0], "剩余未知"), true);
                same.Products[0].Periods[0].Error = "该周期获取失败";
                form.ForceRender(same);
                Check("ui.periodError", ContainsText(form.ContentControls[0], "获取失败"), true);
            }
        }

        // UX019 (v0.11): footer copy action — real WinForms event path on a
        // SHOWN form, click-only clipboard write with an injected recorder,
        // inline feedback, disabled without a snapshot. A HIDDEN form cannot
        // select the button (Button.PerformClick no-ops when CanSelect is
        // false), so the earlier hidden-form test never fired Click and then
        // indexed an empty clip list. The form is now shown and driven through
        // Application.DoEvents; the recorder is still injected so the real user
        // clipboard is never touched.
        private static void CopySummaryUX019Cases()
        {
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                // UX022: the copy action is a details-menu item; feedback is a
                // short visible tooltip text recorded for tests.
                System.Windows.Forms.ToolStripMenuItem copy = form.CopyMenuItemForTest;

                Check("copy.accessible", copy.AccessibleName, "复制摘要");
                Check("copy.tooltip", copy.ToolTipText, "复制当前额度摘要到剪贴板（Ctrl+C）");
                Check("copy.noSnapshotDisabled", copy.Enabled, false);

                int queries = 0;
                form.RefreshRequested += delegate { queries++; };
                List<string> clips = new List<string>();
                form.ClipboardSetForTest = delegate(string t) { clips.Add(t); };

                QuotaSnapshot snap = SyntheticSample.BuildLarge();
                form.ForceRender(snap);
                Check("copy.enabledWithData", copy.Enabled, true);

                // Real control event path on a visible form.
                form.ShowPanel();
                System.Windows.Forms.Application.DoEvents();
                Check("copy.visible", form.Visible, true);
                Check("copy.enabledWhileShown", copy.Enabled, true);

                System.Windows.Forms.Control card = form.ContentControls[0];
                System.Windows.Forms.Control child = card.Controls[0];
                string time = form.UpdateTimeTextForTest;

                // Copy is a read-only action: it must leave the user's scroll
                // offset and focused control exactly where they were, and never
                // rebuild the card. Asserted on the SHOWN form right before and
                // after the real click (verified together with the injected
                // clipboard recorder, not by swapping the handler).
                form.ContentForTest.AutoScrollPosition = new Point(0, 12);
                form.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Application.DoEvents();
                Point scrollBefore = form.ContentForTest.AutoScrollPosition;
                System.Windows.Forms.Control focusBefore = form.ActiveControlForTest;

                CheckCardLayout(form, "copy.geo100");
                copy.PerformClick();
                System.Windows.Forms.Application.DoEvents();
                int recorded = clips.Count;
                // Opening / rendering / showing never copy; only the click does.
                Check("copy.recorded", recorded, 1);
                Check("copy.matchesFormatter",
                    recorded == 1 && clips[0] == QuotaSummary.Build(form.Model.CurrentView), true);
                Check("copy.zeroQuery", queries, 0);
                Check("copy.feedback", form.CopyFeedbackTextForTest, "已复制");
                Check("copy.timeUntouched", form.UpdateTimeTextForTest, time);
                Check("copy.sameCard", ReferenceEquals(card, form.ContentControls[0]), true);
                Check("copy.sameChild", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                Check("copy.scrollKept", form.ContentForTest.AutoScrollPosition, scrollBefore);
                Check("copy.focusKept", form.ActiveControlForTest, focusBefore);

                // Same-semantics refresh during feedback: cards are not rebuilt
                // and the feedback text is not cleared by ApplyModelView.
                form.ForceRender(snap);
                Check("copy.sameCardAfter", ReferenceEquals(card, form.ContentControls[0]), true);
                Check("copy.sameChildAfter", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                Check("copy.feedbackStable", form.CopyFeedbackTextForTest, "已复制");

                // Clipboard failure: short fixed inline text, no dialog, and a
                // plain retry succeeds.
                int beforeFail = clips.Count;
                form.ClipboardSetForTest = delegate(string t)
                    { throw new System.Runtime.InteropServices.ExternalException("clipboard busy"); };
                copy.PerformClick();
                System.Windows.Forms.Application.DoEvents();
                Check("copy.failFeedback", form.CopyFeedbackTextForTest, "复制失败");
                Check("copy.failNoClip", clips.Count, beforeFail);
                Check("copy.timeUntouchedOnFail", form.UpdateTimeTextForTest, time);
                form.ClipboardSetForTest = delegate(string t) { clips.Add(t); };
                copy.PerformClick();
                System.Windows.Forms.Application.DoEvents();
                Check("copy.retryClip", clips.Count, beforeFail + 1);
                Check("copy.retryFeedback", form.CopyFeedbackTextForTest, "已复制");

                // One-shot feedback timer restores the label and stops.
                form.CopyFeedbackTimerForTest.Stop();
                form.CopyFeedbackTimerForTest.Interval = 30;
                form.CopyFeedbackTimerForTest.Start();
                WaitForCopyRestore(form, form.CopyFeedbackTimerForTest);
                Check("copy.timerStopped", form.CopyFeedbackTimerForTest.Enabled, false);
                Check("copy.feedbackCleared", form.CopyFeedbackTextForTest == null, true);

                // Without a snapshot the action is disabled again and a bare
                // state change never writes the clipboard.
                int afterRetry = clips.Count;
                form.ForceIdentityChanged();
                Check("copy.disabledOnIdentityChanged", copy.Enabled, false);
                copy.PerformClick();
                System.Windows.Forms.Application.DoEvents();
                Check("copy.noClipOnIdentityChanged", clips.Count, afterRetry);

                // 100% / 150% / 200%: cards re-derive padding / fonts / radius
                // and the fitted width / height still hug the window exactly.
                form.SetScaleForTest(1.0);
                CheckCardLayout(form, "copy.geo100b");
                form.SetScaleForTest(1.5);
                CheckCardLayout(form, "copy.geo150");
                form.SetScaleForTest(2.0);
                CheckCardLayout(form, "copy.geo200");
                form.SetScaleForTest(1.0);
                form.SetScaleForTest(2.0);
                CheckCardLayout(form, "copy.geo200b");
                form.HidePanel();
            }
        }

        private static void DetailsShortcutUX021Cases()
        {
            // Tooltips advertise the shortcuts; menu labels / accessible
            // names are untouched.
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                Check("ux021.tooltipRefresh",
                    form.RefreshMenuItemForTest.ToolTipText,
                    "重新查询当前额度（Ctrl+R）");
                Check("ux021.tooltipCopy",
                    form.CopyMenuItemForTest.ToolTipText,
                    "复制当前额度摘要到剪贴板（Ctrl+C）");
                Check("ux021.refreshLabel", form.RefreshMenuItemForTest.Text, "刷新");
                Check("ux021.refreshAccessible",
                    form.RefreshMenuItemForTest.AccessibleName, "刷新额度");
                Check("ux021.copyLabel", form.CopyMenuItemForTest.Text, "复制摘要");
                Check("ux021.copyAccessible",
                    form.CopyMenuItemForTest.AccessibleName, "复制摘要");
            }

            // Ctrl+R on a visible focused form: exact single-flight reuse of
            // the refresh path (a pending query is never queued), focus /
            // scroll / children unchanged, exact-combo-only recognition.
            int queries = 0;
            System.Threading.Tasks.TaskCompletionSource<QueryOutcome> pending =
                new System.Threading.Tasks.TaskCompletionSource<QueryOutcome>();
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                QuotaSnapshot snap = SyntheticSample.BuildLarge();
                string fingerprint = QueryScope.FromAuth(PopupForm.SampleIdentity()).Fingerprint;
                using (SnapshotController controller = new SnapshotController(form.Model,
                    delegate(IProgress<QueryProgress> progress, System.Threading.CancellationToken token)
                    { queries++; return pending.Task; }, form.ApplyModelView, form.ShowPanel,
                    delegate { return PersistentStateStore.ToCache(snap, fingerprint); },
                    delegate { return true; }, delegate { }))
                {
                    form.RefreshRequested += async delegate { await controller.Refresh(); };
                    form.ForceRender(snap);
                    form.ShowPanel();
                    System.Windows.Forms.Application.DoEvents();
                    // UX022: the focused target is the first card.
                    form.ContentForTest.Controls[0].Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.refreshFocused",
                        form.ContentForTest.Controls[0].Focused, true);
                    form.ContentForTest.AutoScrollPosition = new Point(0, 90);
                    Point scroll = form.ContentForTest.AutoScrollPosition;
                    System.Windows.Forms.Control card = form.ContentControls[0];
                    System.Windows.Forms.Control child = card.Controls[0];

                    // Child route: the key enters pre-processing at the real
                    // focused control and must reach the form override.
                    Check("ux021.ctrlR.childRouteHandled",
                        DispatchCmdKey(form.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                        true);
                    Check("ux021.ctrlR.singleFlight", queries, 1);
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R);
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.ctrlR.noQueue", queries, 1);
                    Check("ux021.ctrlR.sameCard",
                        ReferenceEquals(card, form.ContentControls[0]), true);
                    Check("ux021.ctrlR.sameChild",
                        ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                    Check("ux021.ctrlR.scrollKept",
                        form.ContentForTest.AutoScrollPosition, scroll);
                    Check("ux021.ctrlR.focusKept",
                        form.ContentForTest.Controls[0].Focused, true);

                    // Exact combos only: modified variants fall through to
                    // base (handled=false) and never touch the query counter.
                    Check("ux021.ctrlShiftR.fallsThrough",
                        DispatchCmdKey(form.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift |
                            System.Windows.Forms.Keys.R),
                        false);
                    Check("ux021.ctrlShiftR.noQuery", queries, 1);
                    Check("ux021.ctrlAltC.fallsThrough",
                        DispatchCmdKey(form.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt |
                            System.Windows.Forms.Keys.C),
                        false);
                    Check("ux021.ctrlAltC.noQuery", queries, 1);

                    pending.SetResult(new QueryOutcome
                    { Snapshot = new QuotaSnapshot { Status = QuotaStatus.Failed } });
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.ctrlR.finished", controller.Querying, false);
                    form.HidePanel();
                }
            }

            // Ctrl+C on a visible focused form: exact copy-button reuse —
            // formatter text of the CURRENT snapshot, zero query, inline
            // feedback, retry after failure — then the cleared-snapshot state
            // still consumes the recognized key without writing anything.
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                form.ForceRender(SyntheticSample.BuildLarge());
                form.ShowPanel();
                System.Windows.Forms.Application.DoEvents();
                List<string> clips = new List<string>();
                form.ClipboardSetForTest = delegate(string t) { clips.Add(t); };
                int queries2 = 0;
                form.RefreshRequested += delegate { queries2++; };
                form.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.copyFocused", form.ContentForTest.Controls[0].Focused, true);

                Check("ux021.ctrlC.handled",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                    true);
                Check("ux021.ctrlC.recorded", clips.Count, 1);
                Check("ux021.ctrlC.matchesFormatter",
                    clips.Count == 1 && clips[0] == QuotaSummary.Build(form.Model.CurrentView),
                    true);
                Check("ux021.ctrlC.zeroQuery", queries2, 0);
                Check("ux021.ctrlC.feedback", form.CopyFeedbackTextForTest, "已复制");
                Check("ux021.ctrlC.focusKept",
                    form.ContentForTest.Controls[0].Focused, true);
                form.CopyFeedbackTimerForTest.Stop();

                // Failure inline feedback + retry through the same key.
                int beforeFail = clips.Count;
                form.ClipboardSetForTest = delegate(string t)
                    { throw new System.Runtime.InteropServices.ExternalException("clipboard busy"); };
                DispatchCmdKey(form.ContentForTest.Controls[0],
                    System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C);
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.ctrlC.failFeedback", form.CopyFeedbackTextForTest, "复制失败");
                Check("ux021.ctrlC.failNoClip", clips.Count, beforeFail);
                form.ClipboardSetForTest = delegate(string t) { clips.Add(t); };
                DispatchCmdKey(form.ContentForTest.Controls[0],
                    System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C);
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.ctrlC.retryClip", clips.Count, beforeFail + 1);
                Check("ux021.ctrlC.retryFeedback", form.CopyFeedbackTextForTest, "已复制");
                form.CopyFeedbackTimerForTest.Stop();

                // Identity change clears the snapshot: the key is still
                // consumed (recognized) but nothing is written / queried.
                form.ForceIdentityChanged();
                form.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.ctrlC.noSnapshotDisabled",
                    form.CopyMenuItemForTest.Enabled, false);
                int afterClear = clips.Count;
                Check("ux021.ctrlC.consumedWithoutSnapshot",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                    true);
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.ctrlC.noClipWithoutSnapshot", clips.Count, afterClear);
                Check("ux021.ctrlC.zeroQueryWithoutSnapshot", queries2, 0);
                form.HidePanel();
            }

            // Guard states: hidden form, shared-menu flag, dialog flag, lifted
            // guard, then a visible-but-unfocused form. All rejected states
            // fall through to base (handled=false) with zero query, zero
            // clipboard writes (recorder injected — a guard regression must
            // never reach the real clipboard) and no window is opened.
            int queries3 = 0;
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                form.RefreshRequested += delegate { queries3++; };
                form.ForceRender(SyntheticSample.BuildLarge());
                List<string> clips3 = new List<string>();
                form.ClipboardSetForTest = delegate(string t) { clips3.Add(t); };

                Check("ux021.hidden.rejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                    false);
                Check("ux021.hidden.noQuery", queries3, 0);
                Check("ux021.hidden.stillHidden", form.Visible, false);
                Check("ux021.hidden.ctrlCRejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                    false);
                Check("ux021.hidden.noClip", clips3.Count, 0);

                form.ShowPanel();
                System.Windows.Forms.Application.DoEvents();
                form.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Application.DoEvents();

                form.SetMenuOpen(true);
                Check("ux021.menuOpen.rejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                    false);
                Check("ux021.menuOpen.noQuery", queries3, 0);
                Check("ux021.menuOpen.ctrlCRejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                    false);
                Check("ux021.menuOpen.noClip", clips3.Count, 0);
                form.SetMenuOpen(false);

                form.SetDialogOpen(true);
                Check("ux021.dialogFlag.rejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                    false);
                Check("ux021.dialogFlag.noQuery", queries3, 0);
                Check("ux021.dialogFlag.noClip", clips3.Count, 0);
                form.SetDialogOpen(false);

                Check("ux021.guardLifted.acts",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                    true);
                Check("ux021.guardLifted.query", queries3, 1);

                // Visible but not containing focus: focus moves to a separate
                // helper window via plain Control.Focus() (no synthetic input).
                using (System.Windows.Forms.Form other = new System.Windows.Forms.Form())
                {
                    other.Show();
                    other.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.unfocused.stillVisible", form.Visible, true);
                    Check("ux021.unfocused.precondition", form.ContainsFocus, false);
                    Check("ux021.unfocused.rejected",
                        DispatchCmdKey(form.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                        false);
                    Check("ux021.unfocused.noQuery", queries3, 1);
                    Check("ux021.unfocused.ctrlCRejected",
                        DispatchCmdKey(form.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                        false);
                    Check("ux021.unfocused.noClip", clips3.Count, 0);
                    other.Close();
                }
                form.HidePanel();
            }

            // Real settings ShowDialog with an active ComboBox: the owner
            // details must not act on Ctrl+R / Ctrl+C (dialog flag, no focus),
            // while the modal's own combo keeps its local key handling.
            // The owner clipboard write is injectable here too: a guard
            // regression must never touch the real user clipboard.
            int queries4 = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries4++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                app.OpenEntryForTest("tray");
                app.ShowDetailsForTest(); // left-click details path, zero query
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.modal.detailsShown", app.DetailsVisibleForTest, true);
                PopupForm details = app.DetailsFormForTest;
                List<string> clips4 = new List<string>();
                details.ClipboardSetForTest = delegate(string t) { clips4.Add(t); };
                details.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Timer t5 = new System.Windows.Forms.Timer();
                t5.Interval = 200;
                bool reached = false;
                t5.Tick += delegate
                {
                    t5.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    reached = true;
                    dlg.ActiveControl = dlg.ComboForTest;
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.modal.dialogFlag", details.DialogOpenForTest, true);
                    Check("ux021.modal.ownerNoFocus", details.ContainsFocus, false);
                    Check("ux021.modal.ownerCtrlR",
                        DispatchCmdKey(details.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                        false);
                    Check("ux021.modal.ownerCtrlC",
                        DispatchCmdKey(details.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C),
                        false);
                    Check("ux021.modal.zeroQuery", queries4, 0);
                    Check("ux021.modal.noClip", clips4.Count, 0);
                    Check("ux021.modal.comboStillLocal",
                        dlg.FocusedForTest == (System.Windows.Forms.Control)dlg.ComboForTest,
                        true);
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t5.Start();
                app.OpenSettingsForTest(); // settings modal while details visible
                t5.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.modal.reached", reached, true);
                Check("ux021.modal.detailsSurvive", app.DetailsVisibleForTest, true);
                Check("ux021.modal.dialogOffAfter", details.DialogOpenForTest, false);
                Check("ux021.modal.zeroQueryAfter", queries4, 0);
                Check("ux021.modal.noClipAfter", clips4.Count, 0);
            }
        }

        private static void CardOnlyUX022Cases()
        {
            Rectangle wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            QuotaSnapshot big = SyntheticSample.BuildLarge();
            CompactUiCases();

            using (FloatingCircleControl circle = new FloatingCircleControl())
            {
                circle.ShowAt(wa, 1.0);
                circle.SetReduceMotion(true);
                foreach (double percent in new double[] { 0, 50, 97, 100 })
                {
                    circle.SetDisplay(new FloatingDisplay { HasData = true, PercentKnown = true,
                        Percent = percent, PercentText = percent + "%" });
                    using (Bitmap bitmap = new Bitmap(circle.Width, circle.Height))
                    {
                        circle.DrawToBitmap(bitmap, circle.ClientRectangle);
                        Color top = bitmap.GetPixel(circle.Width / 2, 12);
                        Check("circle.skyOrWhite", top.ToArgb(), (percent >= 97
                            ? Color.FromArgb(90, 154, 248) : Color.White).ToArgb());
                        // The antialiased outer edge is transparent; sample the
                        // inset stroke rather than the old clipped boundary.
                        Color ring = bitmap.GetPixel(3, circle.Height / 2);
                        Check("circle.bluegrayRing", ring.G - ring.R >= 8
                            && ring.B - ring.G >= 3 && ring.B >= 200, true);
                    }
                }
            }

            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);

                // Multi-card stack: gaps ONLY between cards, last card hugs
                // the client bottom (or the window scrolls — then the last
                // card must still be reachable at the bottom).
                form.ForceRender(big);
                form.ShowPanel();
                System.Windows.Forms.Application.DoEvents();
                Check("ux022.multi.cards", form.ContentCardCount > 1, true);
                CheckCardLayout(form, "ux022.multi.fit");
                System.Windows.Forms.FlowLayoutPanel content = form.ContentForTest;
                if (content.AutoScroll && content.VerticalScroll.Visible)
                {
                    Control last = content.Controls[content.Controls.Count - 1];
                    content.ScrollControlIntoView(last);
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux022.multi.scrollUsed",
                        content.AutoScrollPosition.Y < 0, true);
                    // Control.Bottom is already in the container's SCROLLED
                    // client coordinates, so adding AutoScrollPosition.Y again
                    // double-counts the offset; the display bottom itself must
                    // reach the viewport bottom after ScrollControlIntoView.
                    Check("ux022.multi.lastReachable",
                        last.Bottom >= content.ClientSize.Height - 2,
                        true);
                }

                // Deterministic deep overflow: many periods must scroll and
                // shrink the card width by the real scrollbar width.
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"items\":[{\"product\":\"agent-plan\",\"edition\":\"personal\",");
                sb.Append("\"subscribed\":true,\"periods\":[");
                for (int i = 0; i < 24; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("{\"label\":\"p" + i + "\",\"used\":" + i + ",\"total\":1000,");
                    sb.Append("\"percent\":" + (i * 4) + ",\"reset_at\":\"2026-10-28T00:00:00+08:00\"}");
                }
                sb.Append("]}]}");
                form.ForceRender(QuotaParser.Parse(sb.ToString()));
                System.Windows.Forms.Application.DoEvents();
                Check("ux022.overflow.scrollVisible",
                    content.AutoScroll && content.VerticalScroll.Visible, true);
                // The card must shrink by the REAL scrollbar width relative to
                // the FULL window width: once AutoScroll is on, the content's
                // own ClientSize already excludes the scrollbar, so compare
                // against the window client width instead.
                Check("ux022.overflow.widthShrunk",
                    content.Controls[0].Width < form.ClientSize.Width
                        && content.Controls[0].Width >= form.ClientSize.Width
                            - SystemInformation.VerticalScrollBarWidth - 2,
                    true);
                Check("ux022.overflow.noRounding", form.Region == null, true);
                Control last2 = content.Controls[content.Controls.Count - 1];
                content.ScrollControlIntoView(last2);
                System.Windows.Forms.Application.DoEvents();
                // Same as above: Bottom is already scroll-adjusted.
                Check("ux022.overflow.lastReachable",
                    last2.Bottom >= content.ClientSize.Height - 2,
                    true);

                // Re-anchor consumption: a visible size change pends exactly
                // once; no change consumes nothing.
                form.ConsumePendingReposition();
                int widthBefore = form.Width;
                int heightBefore = form.Height;
                form.ForceRender(big);
                if (form.Width != widthBefore || form.Height != heightBefore)
                    Check("ux022.reanchor.pended", form.ConsumePendingReposition(), true);
                Check("ux022.reanchor.consumedOnce", form.ConsumePendingReposition(), false);

                // In-place status row: a failure over existing data keeps the
                // SAME card, appends the compact status line INSIDE the first
                // card, and a fresh render removes it in place again.
                Control card = form.ContentControls[0];
                Control child = card.Controls[0];
                form.ForceError(QuotaStatus.Timeout, "失败");
                Check("ux022.status.sameCard", ReferenceEquals(card, form.ContentControls[0]), true);
                Check("ux022.status.sameChild", ReferenceEquals(child, card.Controls[0]), true);
                Control status = FindStatusLabelForTest(card);
                Check("ux022.status.present", status != null
                    && status.Text == "未能更新，显示上次数据。", true);
                Check("ux022.status.strong", status != null
                    && status.ForeColor == PopupForm.WarningColor, true);
                Check("ux022.status.cardGrew", card.Height > status.Top + status.Height
                    || card.Bottom == content.ClientSize.Height, true);

                // Focus lands on the first card when the panel opens.
                form.HidePanel();
                form.ShowPanel();
                System.Windows.Forms.Application.DoEvents();
                Check("ux022.focusFirstCard",
                    form.ActiveControlForTest == form.ContentForTest.Controls[0], true);

                // Fresh same-data render clears the status row IN PLACE.
                Control card2 = form.ContentControls[0];
                form.ForceRender(big);
                Check("ux022.status.clearedInPlace",
                    ReferenceEquals(card2, form.ContentControls[0])
                        && FindStatusLabelForTest(card2) == null, true);
                form.HidePanel();
            }

            // Single message card: fits inside the one-pixel outer frame.
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                form.ForceLoading();
                Check("ux022.single.count", form.ContentCardCount, 1);
                Check("ux022.single.region", form.Region == null, true);
                Control card = form.ContentControls[0];
                Check("ux022.single.hug",
                    card.Width == form.ClientSize.Width - 2 && card.Height == form.ClientSize.Height - 2,
                    true);
                CheckCardLayout(form, "ux022.single.fit");
            }
        }

    }
}
