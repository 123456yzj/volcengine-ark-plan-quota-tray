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
        // ---- v0.12 UX020: explicit floating window re-home (悬浮窗归位).
        // Production and tests share the same re-home core; work area /
        // scale are injected here so offline tests never depend on real
        // Screen geometry. ----

        private static void RepositionHomeUX020Cases()
        {
            Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            Rectangle home1 = FloatingCircleControl.InitialBounds(area, 1.0);

            // Scales 1 / 1.5 / 2: home == InitialBounds of the injected work
            // area (bottom-right, default safe margin, DPI-scaled 112dp
            // diameter), circle visible afterwards. The forced re-home path
            // must apply the given bounds even though the circle was already
            // positioned once.
            string[] scaleTags = new string[] { "1", "15", "2" };
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            for (int i = 0; i < scales.Length; i++)
            {
                using (FloatingQuotaForm f = new FloatingQuotaForm(
                    delegate { return null; },
                    delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    delegate(FloatingPreferences q) { return true; }))
                {
                    f.ShowCircleAtForTest(area, 1.0);
                    f.RepositionCircleHomeForTest(area, scales[i]);
                    Check("home.scale" + scaleTags[i] + ".bounds", f.CircleBounds,
                        FloatingCircleControl.InitialBounds(area, scales[i]));
                    Check("home.scale" + scaleTags[i] + ".visible", f.CircleVisible, true);
                }
            }

            // (0,0) origin and negative-coordinate (screen left of primary)
            // work areas land inside the given area; a work area smaller than
            // the circle clamps to the work area's top-left.
            Rectangle atOrigin = new Rectangle(0, 0, 800, 600);
            Rectangle negative = new Rectangle(-1920, -500, 1920, 1040);
            Rectangle tiny = new Rectangle(500, 400, 100, 80);
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                f.ShowCircleAtForTest(area, 1.0);
                f.RepositionCircleHomeForTest(atOrigin, 1.0);
                Check("home.originBounds", f.CircleBounds,
                    FloatingCircleControl.InitialBounds(atOrigin, 1.0));
                f.RepositionCircleHomeForTest(negative, 1.0);
                Check("home.negativeBounds", f.CircleBounds,
                    FloatingCircleControl.InitialBounds(negative, 1.0));
                f.RepositionCircleHomeForTest(tiny, 1.0);
                Check("home.tinyClamped", f.CircleBounds,
                    FloatingCircleControl.InitialBounds(tiny, 1.0));
                Check("home.tinyInside", tiny.IntersectsWith(f.CircleBounds), true);
            }

            // Locked circle: explicit re-home still moves it, zero prefs
            // write (the lock toggle save is the only one).
            int saves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { saves++; return true; }))
            {
                f.ShowCircleAtForTest(area, 1.0);
                f.SetPositionLocked(true);
                Point away = new Point(area.Left + 10, area.Top + 10);
                f.CircleForTest.Bounds = new Rectangle(away, f.CircleForTest.Size);
                Check("home.lockedAway", f.CircleBounds.Location, away);
                f.RepositionCircleHomeForTest(area, 1.0);
                Check("home.lockedHome", f.CircleBounds, home1);
                Check("home.lockedStillOn", f.PositionLocked, true);
                Check("home.lockedSingleSave", saves, 1);
            }

            // In-progress drag: re-home resets the gesture and releases
            // capture, so the stale move cannot drag it back and the release
            // cannot mis-open details.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                f.ShowCircleAtForTest(area, 1.0);
                FloatingCircleControl circle = f.CircleForTest;
                int opened = 0;
                circle.DetailsRequested += delegate { opened++; };
                Point p0 = new Point(circle.Bounds.Left + 20, circle.Bounds.Top + 20);
                circle.SimulateMouseDownForTest(p0);
                // Drag up-left: the circle sits at the bottom-right corner.
                circle.SimulateMouseMoveForTest(new Point(p0.X - 60, p0.Y - 40));
                Check("home.dragSeen", circle.DraggingForTest, true);
                Check("home.dragMoved", circle.Bounds.Location != home1.Location, true);
                f.RepositionCircleHomeForTest(area, 1.0);
                Check("home.dragReset", circle.DraggingForTest, false);
                Check("home.dragHome", f.CircleBounds, home1);
                circle.SimulateMouseMoveForTest(new Point(p0.X - 100, p0.Y - 80));
                Check("home.staleNoMove", f.CircleBounds, home1);
                circle.SimulateMouseUpForTest(new Point(p0.X - 100, p0.Y - 80));
                Check("home.staleNoDetails", opened, 0);
            }

            // Hidden circle: re-home shows it again at the home corner.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                f.ShowCircleAtForTest(area, 1.0);
                f.HideCircleForTest();
                Check("home.hiddenPre", f.CircleVisible, false);
                f.RepositionCircleHomeForTest(area, 1.0);
                Check("home.hiddenShows", f.CircleVisible, true);
                Check("home.hiddenHome", f.CircleBounds, home1);
            }

            // Auto-hide-for-details: a re-home clears the stale flag so a
            // later details close can never resurrect the old position; the
            // restore call afterwards must be a no-op.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                Rectangle tinyWa = new Rectangle(0, 0, 400, 300);
                f.ShowCircleAtForTest(tinyWa, 1.0);
                DetailsPlacement dp = f.PrepareDetails(new Size(900, 500), tinyWa);
                Check("home.autoHidePlacement", dp, DetailsPlacement.HideCircle);
                Check("home.autoHidden", f.AutoHiddenForDetailsForTest, true);
                f.RepositionCircleHomeForTest(area, 1.0);
                Check("home.autoCleared", f.AutoHiddenForDetailsForTest, false);
                Check("home.autoVisible", f.CircleVisible, true);
                Check("home.autoHome", f.CircleBounds, home1);
                f.RestoreAfterDetails();
                Check("home.restoreNoopVisible", f.CircleVisible, true);
                Check("home.restoreNoopHome", f.CircleBounds, home1);
            }

            // v0.12 UX020 fix: a never-positioned circle must re-home onto
            // the PRIMARY screen (a fresh control's default bounds are
            // non-zero at (0,0), which could sit on a secondary monitor).
            // A fresh form is a genuine not-positioned fixture because
            // ShowCircleAtForTest does not set _positioned; deterministic on
            // a single screen — no second monitor required or assumed.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                Check("home.freshNotPositioned", f.PositionedForTest, false);
                f.RepositionCircleHome(); // production path: real screen choice
                System.Windows.Forms.Screen ps = System.Windows.Forms.Screen.PrimaryScreen;
                Check("home.freshPrimaryBounds", f.CircleBounds,
                    FloatingCircleControl.InitialBounds(ps.WorkingArea, DpiUtil.GetScale(ps)));
                Check("home.freshVisible", f.CircleVisible, true);
                Check("home.freshPositionedAfter", f.PositionedForTest, true);
            }

            // Production handler via the shared menu (index 4): details close,
            // the dragged circle returns home, zero query, poll interval
            // follows the existing UX013 visible rule, toggle text intact.
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("home.appDetailsOpen", app.DetailsVisibleForTest, true);
                FloatingCircleControl circle = app.CircleForTest;
                Point p0 = new Point(circle.Bounds.Left + 20, circle.Bounds.Top + 20);
                Rectangle beforeDrag = circle.Bounds;
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new Point(p0.X - 80, p0.Y - 60));
                circle.SimulateMouseUpForTest(new Point(p0.X - 80, p0.Y - 60));
                Check("home.appDraggedAway", circle.Bounds.Location != beforeDrag.Location, true);
                app.PerformMenuHomeForTest(); // 悬浮窗归位
                System.Windows.Forms.Application.DoEvents();
                Check("home.appDetailsClosed", app.DetailsVisibleForTest, false);
                Check("home.appCircleVisible", app.FloatingVisibleForTest, true);
                System.Windows.Forms.Screen scr =
                    System.Windows.Forms.Screen.FromRectangle(circle.Bounds);
                Check("home.appAtHome", circle.Bounds,
                    FloatingCircleControl.InitialBounds(scr.WorkingArea, DpiUtil.GetScale(scr)));
                Check("home.appZeroQuery", queries, 0);
                Check("home.appToggleText", app.MenuTextForTest(1), "隐藏悬浮窗");
                Check("home.appPollVisible", app.PollIntervalForTest, 10000);
            }

            // Hidden circle + failing prefs: re-home shows the circle and
            // never attempts a prefs save (no failure notify intent).
            int queries2 = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries2++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); },
                delegate(FloatingPreferences p) { return false; }))
            {
                app.HideDetailsForTest();
                Check("home.hiddenAppPre", app.FloatingVisibleForTest, false);
                app.PerformMenuHomeForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("home.hiddenAppShows", app.FloatingVisibleForTest, true);
                // Not positioned (silent start) -> the primary branch decides
                // screen AND scale; do not assume the machine runs at 100%.
                System.Windows.Forms.Screen psh = System.Windows.Forms.Screen.PrimaryScreen;
                Check("home.hiddenAppHome", app.CircleForTest.Bounds,
                    FloatingCircleControl.InitialBounds(psh.WorkingArea, DpiUtil.GetScale(psh)));
                Check("home.hiddenAppNoSave", app.LockFailNotifyCountForTest, 0);
                Check("home.hiddenAppZeroQuery", queries2, 0);
                Check("home.hiddenAppPollVisible", app.PollIntervalForTest, 10000);
                Check("home.hiddenAppToggleText", app.MenuTextForTest(1), "隐藏悬浮窗");
            }

            // Settings modal up: a programmatic 归位 click must do NOTHING
            // (no owner move, no modal close, no query). The circle is dragged
            // away from home first so a missing guard would be observable.
            int queries3 = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries3++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                FloatingCircleControl circle = app.CircleForTest;
                Point p0 = new Point(circle.Bounds.Left + 20, circle.Bounds.Top + 20);
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new Point(p0.X - 80, p0.Y - 60));
                circle.SimulateMouseUpForTest(new Point(p0.X - 80, p0.Y - 60));
                Rectangle dragged = circle.Bounds;
                Check("home.modalDragged", dragged.Location != home1.Location, true);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                bool clicked = false;
                Rectangle during = Rectangle.Empty;
                t.Interval = 200;
                t.Tick += delegate
                {
                    t.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return; // modal never opened: skip marker
                    app.PerformMenuHomeForTest(); // 归位 while the modal owns the loop
                    clicked = true;
                    during = circle.Bounds;
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t.Start();
                app.OpenSettingsForTest(); // settings modal (blocks in ShowDialog)
                t.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("home.modalClicked", clicked, true);
                Check("home.modalNoMove", during, dragged);
                Check("home.modalClosed", app.SettingsOpenForTest, false);
                Check("home.modalStillDragged", circle.Bounds, dragged);
                Check("home.modalZeroQuery", queries3, 0);
            }
        }

    }
}
