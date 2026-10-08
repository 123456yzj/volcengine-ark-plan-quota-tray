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
        private static void ActualOpenEntryCases()
        {
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                foreach (string entry in new string[] { "firstRun", "--show", "IPC", "menu", "tray" })
                {
                    app.OpenEntryForTest(entry);
                    System.Windows.Forms.Application.DoEvents();
                    Check("actualEntry." + entry + ".circle", app.FloatingVisibleForTest, true);
                    if (entry == "tray" || entry == "firstRun" || entry == "--show" || entry == "IPC")
                    {
                        // v0.4: the default entry opens ONLY the circle; details
                        // open on the explicit "查看全部额度" / circle click.
                        Check("actualEntry." + entry + ".detailsClosed", app.DetailsVisibleForTest, false);
                    }
                    else
                    {
                        Check("actualEntry." + entry + ".details", app.DetailsVisibleForTest, true);
                    }
                    Check("actualEntry." + entry + ".zeroQuery", queries, 0);
                }
            }
        }

        private static void ActualRefreshButtonCases()
        {
            using (PopupForm form = new PopupForm())
            {
                int queries = 0;
                System.Threading.Tasks.TaskCompletionSource<QueryOutcome> pending =
                    new System.Threading.Tasks.TaskCompletionSource<QueryOutcome>();
                QuotaSnapshot snap = SyntheticSample.BuildLarge();
                string fingerprint = QueryScope.FromAuth(PopupForm.SampleIdentity()).Fingerprint;
                using (SnapshotController controller = new SnapshotController(form.Model,
                    delegate(IProgress<QueryProgress> progress, System.Threading.CancellationToken token)
                    { queries++; return pending.Task; }, form.ApplyModelView, form.ShowPanel,
                    delegate { return PersistentStateStore.ToCache(snap, fingerprint); },
                    delegate { return true; }, delegate { }))
                {
                    form.RefreshRequested += async delegate { await controller.Refresh(); };
                    form.ShowPanel();
                    // UX022: the card (content's first child) is the focus
                    // target; Ctrl+R refreshes the focused details.
                    form.ContentForTest.Controls[0].Focus();
                    form.ContentForTest.AutoScrollPosition = new Point(0, 90);
                    Point scroll = form.ContentForTest.AutoScrollPosition;
                    System.Windows.Forms.Control card = form.ContentControls[0];
                    System.Windows.Forms.Control child = card.Controls[0];
                    string time = form.UpdateTimeTextForTest;
                    Application.DoEvents();
                    DispatchCmdKey(card, Keys.Control | Keys.R);
                    DispatchCmdKey(card, Keys.Control | Keys.R);
                    Check("actualRefresh.singleFlight", queries, 1);
                    Check("actualRefresh.waitSameChild", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                    Check("actualRefresh.waitTime", form.UpdateTimeTextForTest, time);
                    Check("actualRefresh.waitScroll", form.ContentForTest.AutoScrollPosition, scroll);
                    Check("actualRefresh.waitFocus", form.ContentForTest.Controls[0].Focused, true);
                    pending.SetResult(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.Failed } });
                    System.Windows.Forms.Application.DoEvents();
                    Check("actualRefresh.failureSameChild", ReferenceEquals(child, form.ContentControls[0].Controls[0]), true);
                    Check("actualRefresh.failureTime", form.UpdateTimeTextForTest, time);
                    Check("actualRefresh.failureScroll", form.ContentForTest.AutoScrollPosition, scroll);
                    Check("actualRefresh.failureFocus", form.ContentForTest.Controls[0].Focused, true);
                    Check("actualRefresh.finished", controller.Querying, false);
                }
            }
        }

        private static void FloatingTrayAppCases()
        {
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                // Left-click the circle (production mouse handlers): details open
                // with ZERO query, circle stays.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("trayApp.circleClickDetails", app.DetailsVisibleForTest, true);
                Check("trayApp.circleClickZeroQuery", queries, 0);
                Check("trayApp.circleStaysVisible", app.FloatingVisibleForTest, true);
                // Details is positioned left of the circle, not overlapping it.
                System.Drawing.Rectangle cb = app.CircleForTest.Bounds;
                System.Drawing.Rectangle details = app.DetailsBoundsForTest;
                Check("trayApp.detailsLeftOfCircle", details.Right <= cb.Left, true);

                Point click = app.CircleForTest.PointToScreen(new Point(20, 20));
                app.CircleForTest.SimulateMouseDownForTest(click);
                app.CircleForTest.SimulateMouseUpForTest(click);
                Application.DoEvents();
                Check("trayApp.circleRepeatKeepsDetails", app.DetailsVisibleForTest, true);
                Check("trayApp.circleSecondClickKeepsCircle", app.FloatingVisibleForTest, true);
                app.CircleForTest.SimulateMouseDownForTest(click);
                app.CircleForTest.SimulateMouseUpForTest(click);
                Application.DoEvents();
                Check("trayApp.circleThirdClickOpens", app.DetailsVisibleForTest, true);

                Check("trayApp.circleToggleZeroQuery", queries, 0);

                // Account actions live inside the shared settings submenu.
                Check("trayApp.menuPresent", app.MenuItemCountForTest, 5);
                Check("trayApp.menuTopSettings", app.MenuTextForTest(0), "设置");
                Check("trayApp.menuTopExit", app.MenuTextForTest(4), "退出 ark_left");
                Check("trayApp.submenuCount", app.MenuSettingsCountForTest, 3);
                Check("trayApp.menuHasContent", app.MenuSettingsTextForTest(0), "悬浮内容");
                Check("trayApp.menuHasToggle", app.MenuTextForTest(2), "隐藏悬浮窗");
                app.ShowDetailsForTest(); // left-click details path, zero query
                System.Windows.Forms.Application.DoEvents();
                Check("trayApp.menuViewZeroQuery", queries, 0);
                // Repeat the details action while already visible: no
                // re-layout / flicker (same bounds, still visible), zero query.
                System.Drawing.Rectangle before = app.DetailsBoundsForTest;
                app.ShowDetailsForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("trayApp.repeatNoFlicker", app.DetailsBoundsForTest, before);
                Check("trayApp.repeatStillVisible", app.DetailsVisibleForTest, true);
                Check("trayApp.repeatZeroQuery", queries, 0);

                // Tray toggle: shown -> hidden -> shown, always zero query.
                app.OpenEntryForTest("tray");
                System.Windows.Forms.Application.DoEvents();
                Check("trayApp.trayShowsCircle", app.FloatingVisibleForTest, true);
                app.ToggleFloatingForTest();
                Check("trayApp.toggleHides", app.FloatingVisibleForTest, false);
                app.ToggleFloatingForTest();
                Check("trayApp.toggleShows", app.FloatingVisibleForTest, true);
                Check("trayApp.zeroQuery", queries, 0);
            }
        }

        // v0.5 UX013: the production tray timer accelerates to 10s while the
        // circle / details are visible and falls back to 5min when both hide.
        // Uses the REAL System.Windows.Forms.Timer and the SAME production
        // controller; no new timer, no CLI, synthetic fixtures only.
        private static void PollIntervalUX013Cases()
        {
            Check("ux013.baseConst", SnapshotController.PollIntervalMs, 300000);
            Check("ux013.visibleConst", SnapshotController.VisiblePollIntervalMs, 10000);

            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                // Fresh construction leaves both surfaces hidden (production
                // Run starts the timer; the ctor and this test do not show).
                Check("ux013.hiddenBase", app.PollIntervalForTest, 300000);
                Check("ux013.noQueryAtInit", queries, 0);

                // Show the circle (zero query) must accelerate WITHOUT a query.
                app.OpenEntryForTest("tray");
                Check("ux013.circleVisible10s", app.PollIntervalForTest, 10000);
                Check("ux013.showNoQuery", queries, 0);

                // Details fallback restore: opening details may auto-hide the
                // circle; interval stays 10s via _form.Visible.
                app.ShowDetailsForTest(); // left-click details path, zero query
                System.Windows.Forms.Application.DoEvents();
                Check("ux013.detailsVisible", app.DetailsVisibleForTest, true);
                Check("ux013.detailsStill10s", app.PollIntervalForTest, 10000);

                // Settings modal alone is intentionally out of scope here: the
                // existing suite already covers the settings path, and a modal
                // must not accelerate (circle/details hidden => base).

                // Hide the details and the circle: both hidden -> 300000.
                app.HideDetailsForTest();
                app.ToggleFloatingForTest(); // shown -> hidden
                Check("ux013.allHidden300s", app.PollIntervalForTest, 300000);

                // Re-showing must accelerate again with zero query; repeated
                // identical visibility keeps the same interval value.
                app.ToggleFloatingForTest(); // hidden -> shown => 10000
                Check("ux013.showAgain10s", app.PollIntervalForTest, 10000);
                app.OpenEntryForTest("tray"); // net visible again
                Check("ux013.sameValueStable", app.PollIntervalForTest, 10000);
            }

            // Real WinForms Timer: with the circle visible (10000ms) the first
            // tick executes exactly one production query. Pumps up to 14s and
            // never calls the CLI (injected counter).
            int ticks = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { ticks++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                app.OpenEntryForTest("tray"); // circle visible, zero query
                Check("ux013.timerStartZero", ticks, 0);
                Check("ux013.timerInterval10s", app.PollIntervalForTest, 10000);
                app.StartPollingForTest(); // starts the existing _poll only
                DateTime deadline = DateTime.UtcNow.AddSeconds(14);
                while (ticks == 0 && DateTime.UtcNow < deadline)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(50);
                }
                Check("ux013.realTickOneQuery", ticks, 1);
            }

            // In-flight single-flight (Tick while a query is pending must not
            // stack) is reused from the existing `controller.singleflight`
            // assertion in SnapshotControllerCases; not duplicated here.
        }

    }
}
