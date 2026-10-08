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
                // UX022: the details panel composites its child card windows so a
                // content-changing refresh (card rebuild + window re-fit) cannot
                // flash. WS_EX_COMPOSITED must be set on the real window.
                IntPtr compositedHandle = form.Handle;
                GC.KeepAlive(compositedHandle);
                Check("ux022.composited",
                    (GetWindowLong(compositedHandle, -20) & 0x02000000) != 0, true);
                // v0.24 UX029 card-only popup: no chrome at all — exactly one
                // direct child (the card content), NO details-local context menu
                // and no card tooltips, and no outer padding.
                Check("ux022.contentOnly", form.Controls.Count == 1
                    && ReferenceEquals(form.Controls[0], form.ContentForTest), true);
                Check("ux022.contentPadding", form.ContentForTest.Padding.All, 0);
                Check("ux022.noDetailsMenu",
                    form.ContentForTest.ContextMenuStrip == null, true);
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
                // on the cards' "arkUpdateTime" label (no footer, no tooltip).
                Check("ux022.cardSelectable", card.TabStop, true);
                Check("ux022.updateTimeLabelPresent", form.UpdateTimeTextForTest != null, true);
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
        // UX019 copy is removed in v0.24 UX029 (the details card context menu
        // was deleted): the copy formatter / clipboard path no longer has a UI
        // entry, so its case is gone with it.

        private static void DetailsShortcutUX021Cases()
        {
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

            // Guard states: hidden form, shared-menu flag, dialog flag, lifted
            // guard, then a visible-but-unfocused form. All rejected states
            // fall through to base (handled=false) with zero query and no
            // window opened.
            int queries3 = 0;
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(System.Windows.Forms.Screen.PrimaryScreen);
                form.RefreshRequested += delegate { queries3++; };
                form.ForceRender(SyntheticSample.BuildLarge());

                Check("ux021.hidden.rejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                    false);
                Check("ux021.hidden.noQuery", queries3, 0);
                Check("ux021.hidden.stillHidden", form.Visible, false);
                Check("ux021.hidden.ctrlShiftFallsThrough",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift |
                        System.Windows.Forms.Keys.R),
                    false);
                Check("ux021.hidden.ctrlShiftNoQuery", queries3, 0);

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
                form.SetMenuOpen(false);

                form.SetDialogOpen(true);
                Check("ux021.dialogFlag.rejected",
                    DispatchCmdKey(form.ContentForTest.Controls[0],
                        System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                    false);
                Check("ux021.dialogFlag.noQuery", queries3, 0);
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
                    other.Close();
                }
                form.HidePanel();
            }

            // Real theme dialog with an active ComboBox: the owner details must
            // not act on Ctrl+R (dialog flag, no focus), while the modal's own
            // combo keeps its local key handling.
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
                details.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Timer t5 = new System.Windows.Forms.Timer();
                t5.Interval = 200;
                bool reached = false;
                t5.Tick += delegate
                {
                    t5.Stop();
                    ThemeDialogForm dlg = FindOpenThemeDialog();
                    if (dlg == null) return;
                    reached = true;
                    dlg.ActiveControl = dlg.AccentComboForTest;
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux021.modal.dialogFlag", details.DialogOpenForTest, true);
                    Check("ux021.modal.ownerNoFocus", details.ContainsFocus, false);
                    Check("ux021.modal.ownerCtrlR",
                        DispatchCmdKey(details.ContentForTest.Controls[0],
                            System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R),
                        false);
                    Check("ux021.modal.zeroQuery", queries4, 0);
                    Check("ux021.modal.comboStillLocal",
                        dlg.ActiveControl == (System.Windows.Forms.Control)dlg.AccentComboForTest,
                        true);
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t5.Start();
                app.OpenThemeForTest(); // theme modal while details visible
                t5.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux021.modal.reached", reached, true);
                Check("ux021.modal.detailsSurvive", app.DetailsVisibleForTest, true);
                Check("ux021.modal.dialogOffAfter", details.DialogOpenForTest, false);
                Check("ux021.modal.zeroQueryAfter", queries4, 0);
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
