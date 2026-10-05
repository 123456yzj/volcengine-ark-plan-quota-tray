using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    // Minimal, dependency-free test runner for the pure parsing/CLI logic.
    // Compiled with the same csc (C# 5) and shares src sources.
    internal static class QuotaTests
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failures = new List<string>();

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--menu-interaction")
                {
                    MenuInteractionCases();
                    FloatingMenuUX023Cases();
                    Console.WriteLine("menu-interaction: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--compact-ui")
                {
                    CompactUiCases();
                    Console.WriteLine("compact-ui: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--effective-quota")
                {
                    EffectiveQuotaCases();
                    Console.WriteLine("effective-quota: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--floating-amount")
                {
                    EffectiveQuotaCases();
                    FloatingSelectionCases();
                    FloatingDisplayCases();
                    FloatingWindowCases();
                    FloatingTrayAppCases();
                    Console.WriteLine("floating-amount: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                return RunTests();
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-crash.txt"),
                    ex.ToString()); } catch (Exception) { }
                Console.Error.WriteLine("unexpected test error: " + ex.Message);
                return 2;
            }
        }

        private static int RunTests()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("ark_left tests (synthetic fixtures only)");
            Console.WriteLine("========================================");

            PercentWins();
            ZeroIsValid();
            MissingIsUnknown();
            UsedTotalFallback();
            TotalZero();
            OutOfBoundsClamp();
            MalformedAndShape();
            PartialErrors();
            UnsubscribedAndEmpty();
            ErrorBucketNotUnsubscribed();
            MissingSubscribedIsMalformed();
            NullItemAndNullPeriod();
            PeriodErrorPartial();
            ItemUpdatedAt();
            ErrorTextNotLeaked();
            CliAuthGate();
            CliUsageExitCode();
            CliTimeoutCancelDispose();
            DisplayNameMapping();
            LayoutMathBounds();
            PercentFormatCases();
            EffectiveQuotaCases();
            RelativeFormatCases();
            RiskSummaryCases();
            ScopeFingerprintCases();
            ScopeValidationCases();
            PanelModelIdentityCases();
            PanelModelStaleCases();
            PanelModelUnknownVerdictCases();
            PanelModelNullIdentityCases();
            PanelModelReuseTiedToCurrentQuery();
            PanelModelAuthHintClearing();
            HideControllerCases();
            PanelPositionerCases();
            IdentityParseCases();
            IdentityDisplayCases();
            PanelStateAfterMismatch();
            ViewEmptyProductsCases();
            IpcSignalCases();
            MarkerIsolationCases();
            ProgressStageCases();
            PersistentSnapshotCases();
            SnapshotControllerCases();
            SnapshotProgressCases();
            PopupDisplayCases();
            ActualOpenEntryCases();
            ActualRefreshButtonCases();
            FloatingSelectionCases();
            FloatingSettingsStoreCases();
            FloatingDisplayCases();
            FloatingWindowCases();
            FloatingTrayAppCases();
            FloatingSettingsDialogCases();
            FloatingLayoutCases();
            FloatingSettingsModalCases();
            FloatingPrepareDetailsCases();
            PollIntervalUX013Cases();
            FloatingSettingsUX015Cases();
            FloatingPreferencesUX016Cases();
            FloatingPreferencesUX017Cases();
            QuotaSummaryCases();
            CopySummaryUX019Cases();
            RepositionHomeUX020Cases();
            DetailsShortcutUX021Cases();
            CardOnlyUX022Cases();
            FloatingMenuUX023Cases();

            Console.WriteLine();
            Console.WriteLine("passed: " + _passed + ", failed: " + _failed);
            if (_failed > 0)
            {
                Console.WriteLine("failures:");
                for (int i = 0; i < _failures.Count; i++)
                    Console.WriteLine("  - " + _failures[i]);
                return 1;
            }
            return 0;
        }

        // ---- cases ----

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static void PumpToggleMessages(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            do
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            } while (DateTime.UtcNow < until);
        }

        private static void LogMenuFailureState(int previousFailures, ContextMenuStrip menu,
            ToolStripMenuItem settings, ToolStripMenuItem content, Control circle)
        {
            if (_failed == previousFailures) return;
            Console.Error.WriteLine("menu state: cursor=" + Cursor.Position
                + " active=" + (Form.ActiveForm == null ? "null" : Form.ActiveForm.Text)
                + " circle=" + circle.Bounds + " root=" + menu.Bounds
                + " rootVisible=" + menu.Visible + " settings=" + settings.DropDown.Bounds
                + " settingsVisible=" + settings.DropDown.Visible + " content=" + content.DropDown.Bounds
                + " contentVisible=" + content.DropDown.Visible);
        }

        private static void CheckMenuGeometry(string tag, string level, ToolStripDropDown drop,
            Rectangle work, Control circle)
        {
            int before = _failed;
            Check(tag + "." + level + ".visible", drop.Visible, true);
            if (drop.Visible)
            {
                Check(tag + "." + level + ".onScreen", work.Contains(drop.Bounds), true);
                Check(tag + "." + level + ".avoidsCircle",
                    drop.Bounds.IntersectsWith(circle.Bounds), false);
            }
            if (_failed != before)
                Console.Error.WriteLine(tag + "." + level + " state: circle=" + circle.Bounds
                    + " bounds=" + drop.Bounds + " visible=" + drop.Visible
                    + " autoClose=" + drop.AutoClose + " cursor=" + Cursor.Position
                    + " active=" + (Form.ActiveForm == null ? "null" : Form.ActiveForm.Text));
        }

        private static void MenuInteractionCases()
        {
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.OpenEntryForTest("circle");
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                FloatingCircleControl circle = app.CircleForTest;
                Check("menu.circleHoverTooltipEmpty", circle.HoverTooltipForTest, "");
                ContextMenuStrip menu = app.MenuForTest;
                ToolStripMenuItem settings = app.MenuSettingsForTest;
                ToolStripMenuItem content = (ToolStripMenuItem)settings.DropDownItems[0];
                int initialFailures = _failed;
                app.ShowDetailsForTest();
                Application.DoEvents();
                Check("menu.detailsInitiallyOpen", app.DetailsVisibleForTest, true);
                menu.Show(circle, new Point(20, 20));
                Application.DoEvents();
                Check("menu.closesLeftDetails", app.DetailsVisibleForTest, false);
                Check("menu.rootAvoidsCircle", menu.Bounds.IntersectsWith(circle.Bounds), false);
                LogMenuFailureState(initialFailures, menu, settings, content, circle);
                foreach (ToolStripMenuItem item in new ToolStripMenuItem[] { settings, content })
                {
                    int beforeItem = _failed;
                    item.PerformClick();
                    Application.DoEvents();
                    Check("menu.clickOpens." + item.Text, item.DropDown.Visible, true);
                    item.PerformClick();
                    Application.DoEvents();
                    Check("menu.clickCloses." + item.Text, item.DropDown.Visible, false);
                    item.PerformClick();
                    Application.DoEvents();
                    Check("menu.clickReopens." + item.Text, item.DropDown.Visible, true);
                    Point center = new Point(item.Bounds.Left + item.Bounds.Width / 2,
                        item.Bounds.Top + item.Bounds.Height / 2);
                    IntPtr point = new IntPtr(center.X | (center.Y << 16));
                    SendMessage(item.Owner.Handle, 0x0201, new IntPtr(1), point);
                    SendMessage(item.Owner.Handle, 0x0202, IntPtr.Zero, point);
                    PumpToggleMessages(80);
                    Check("menu.mouseCloses." + item.Text, item.DropDown.Visible, false);
                    SendMessage(item.Owner.Handle, 0x0201, new IntPtr(1), point);
                    SendMessage(item.Owner.Handle, 0x0202, IntPtr.Zero, point);
                    PumpToggleMessages(80);
                    Check("menu.mouseReopens." + item.Text, item.DropDown.Visible, true);
                    LogMenuFailureState(beforeItem, menu, settings, content, circle);
                }
                int beforePlacement = _failed;
                Check("menu.settingsAvoidsCircle", settings.DropDown.Bounds.IntersectsWith(circle.Bounds), false);
                Check("menu.contentAvoidsCircle", content.DropDown.Bounds.IntersectsWith(circle.Bounds), false);
                LogMenuFailureState(beforePlacement, menu, settings, content, circle);
                int beforeDetails = _failed;
                app.ShowDetailsForTest();
                Application.DoEvents();
                Check("menu.leftDetailsOpen", app.DetailsVisibleForTest, true);
                Check("menu.leftClosesRoot", menu.Visible, false);
                Check("menu.leftClosesSettings", settings.DropDown.Visible, false);
                Check("menu.leftClosesContent", content.DropDown.Visible, false);
                LogMenuFailureState(beforeDetails, menu, settings, content, circle);
                app.HideDetailsForTest();
                Point click = circle.PointToScreen(new Point(circle.Width / 2, circle.Height / 2));
                int dragStarts = 0;
                circle.DragStarted += delegate { dragStarts++; };
                app.ShowDetailsForTest();
                Application.DoEvents();
                circle.SimulateMouseDownForTest(click);
                Check("menu.downKeepsDetails", app.DetailsVisibleForTest, true);
                circle.SimulateMouseMoveForTest(new Point(click.X + 2, click.Y));
                Check("menu.belowThresholdKeepsDetails", app.DetailsVisibleForTest, true);
                circle.SimulateMouseMoveForTest(new Point(click.X + 20, click.Y));
                Application.DoEvents();
                Check("menu.dragClosesDetails", app.DetailsVisibleForTest, false);
                Check("menu.firstDragStart", dragStarts, 1);
                circle.SimulateMouseMoveForTest(new Point(click.X + 24, click.Y));
                Check("menu.dragStartOnlyOnce", dragStarts, 1);
                circle.SimulateMouseUpForTest(new Point(click.X + 20, click.Y));
                Application.DoEvents();
                Check("menu.dragReleaseKeepsDetailsClosed", app.DetailsVisibleForTest, false);
                menu.Show(circle, new Point(20, 20));
                settings.ShowDropDown();
                content.ShowDropDown();
                Application.DoEvents();
                click = circle.PointToScreen(new Point(circle.Width / 2, circle.Height / 2));
                circle.SimulateMouseDownForTest(click);
                Check("menu.downKeepsRoot", menu.Visible, true);
                Check("menu.downKeepsSettings", settings.DropDown.Visible, true);
                Check("menu.downKeepsContent", content.DropDown.Visible, true);
                circle.SimulateMouseMoveForTest(new Point(click.X + 2, click.Y));
                Check("menu.belowThresholdKeepsRoot", menu.Visible, true);
                circle.SimulateMouseMoveForTest(new Point(click.X + 20, click.Y));
                Application.DoEvents();
                Check("menu.dragClosesRoot", menu.Visible, false);
                Check("menu.dragClosesSettings", settings.DropDown.Visible, false);
                Check("menu.dragClosesContent", content.DropDown.Visible, false);
                Check("menu.secondDragStart", dragStarts, 2);
                circle.SimulateMouseUpForTest(new Point(click.X + 20, click.Y));
                Application.DoEvents();
                Check("menu.dragReleaseKeepsRootClosed", menu.Visible, false);
                Check("menu.dragReleaseKeepsDetailsClosedAgain", app.DetailsVisibleForTest, false);
                circle.PositionLocked = true;
                Point lockedLocation = circle.Location;
                click = circle.PointToScreen(new Point(circle.Width / 2, circle.Height / 2));
                circle.SimulateMouseDownForTest(click);
                circle.SimulateMouseMoveForTest(new Point(click.X + 20, click.Y));
                circle.SimulateMouseUpForTest(new Point(click.X + 20, click.Y));
                Check("menu.lockedDragDoesNotMove", circle.Location, lockedLocation);
                Check("menu.lockedDragDoesNotStart", dragStarts, 2);
                Check("menu.lockedDragDoesNotOpenDetails", app.DetailsVisibleForTest, false);
                circle.PositionLocked = false;
                click = circle.PointToScreen(new Point(circle.Width / 2, circle.Height / 2));
                circle.SimulateMouseDownForTest(click);
                circle.SimulateMouseUpForTest(click);
                Application.DoEvents();
                Check("menu.clickStillOpensDetails", app.DetailsVisibleForTest, true);
                app.HideDetailsForTest();
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                bool rootAutoClose = menu.AutoClose;
                bool settingsAutoClose = settings.DropDown.AutoClose;
                bool contentAutoClose = content.DropDown.AutoClose;
                try
                {
                menu.AutoClose = false;
                settings.DropDown.AutoClose = false;
                content.DropDown.AutoClose = false;
                circle.Location = new Point(work.Left + (work.Width - circle.Width) / 2,
                    work.Top + (work.Height - circle.Height) / 2);
                menu.Show(circle, new Point(20, 20));
                Application.DoEvents();
                int beforeCenter = _failed;
                Check("menu.center.root.visible", menu.Visible, true);
                if (circle.Left - work.Left >= menu.Width + FloatingLayout.Gap
                    && work.Right - circle.Right >= menu.Width + FloatingLayout.Gap && menu.Visible)
                {
                    Check("menu.centerRightOfCircle", menu.Left >= circle.Right + FloatingLayout.Gap,
                        true);
                    Check("menu.centerRootOnScreen", work.Contains(menu.Bounds), true);
                }
                LogMenuFailureState(beforeCenter, menu, settings, content, circle);
                menu.Close();
                int chainWidth = menu.Width + settings.DropDown.GetPreferredSize(Size.Empty).Width
                    + content.DropDown.GetPreferredSize(Size.Empty).Width + FloatingLayout.Gap;
                int rightSpace = menu.Width + FloatingLayout.Gap + 12;
                if (rightSpace < chainWidth && work.Width - circle.Width - rightSpace
                    >= menu.Width + FloatingLayout.Gap)
                {
                    circle.Location = new Point(work.Right - circle.Width - rightSpace,
                        work.Top + (work.Height - circle.Height) / 2);
                    menu.Show(circle, new Point(20, 20));
                    settings.ShowDropDown();
                    content.ShowDropDown();
                    Application.DoEvents();
                    int beforeShortRight = _failed;
                    CheckMenuGeometry("menu.shortRight", "root", menu, work, circle);
                    if (menu.Visible)
                        Check("menu.shortRightRootRightOfCircle",
                            menu.Left >= circle.Right + FloatingLayout.Gap, true);
                    CheckMenuGeometry("menu.shortRight", "settings", settings.DropDown, work, circle);
                    CheckMenuGeometry("menu.shortRight", "content", content.DropDown, work, circle);
                    LogMenuFailureState(beforeShortRight, menu, settings, content, circle);
                    menu.Close();
                }
                foreach (Point corner in new Point[] { work.Location,
                    new Point(work.Right - circle.Width, work.Top),
                    new Point(work.Left, work.Bottom - circle.Height),
                    new Point(work.Right - circle.Width, work.Bottom - circle.Height) })
                {
                    circle.Location = corner;
                    menu.Show(circle, new Point(20, 20));
                    settings.ShowDropDown();
                    content.ShowDropDown();
                    Application.DoEvents();
                    CheckMenuGeometry("menu.corner", "root", menu, work, circle);
                    CheckMenuGeometry("menu.corner", "settings", settings.DropDown, work, circle);
                    CheckMenuGeometry("menu.corner", "content", content.DropDown, work, circle);
                    menu.Close();
                }
                }
                finally
                {
                    content.HideDropDown();
                    settings.HideDropDown();
                    menu.Close();
                    content.DropDown.AutoClose = contentAutoClose;
                    settings.DropDown.AutoClose = settingsAutoClose;
                    menu.AutoClose = rootAutoClose;
                }
                Point nativePoint = new Point(work.Left + work.Width / 2,
                    work.Top + work.Height / 2);
                int beforeNative = _failed;
                menu.Show(nativePoint);
                Application.DoEvents();
                Check("menu.nonCircleSource", menu.SourceControl == null, true);
                Check("menu.nonCircleLocation", menu.Location, nativePoint);
                ToolStripDropDownDirection settingsBefore = settings.DropDownDirection;
                ToolStripDropDownDirection contentBefore = content.DropDownDirection;
                settings.ShowDropDown();
                content.ShowDropDown();
                Application.DoEvents();
                ToolStripDropDownDirection settingsAfter = settings.DropDownDirection;
                ToolStripDropDownDirection contentAfter = content.DropDownDirection;
                LogMenuFailureState(beforeNative, menu, settings, content, circle);
                menu.Close();
                using (ContextMenuStrip native = new ContextMenuStrip())
                {
                    ToolStripMenuItem nativeSettings = new ToolStripMenuItem("设置");
                    ToolStripMenuItem nativeContent = new ToolStripMenuItem("悬浮内容");
                    nativeContent.DropDownItems.Add("对照项");
                    nativeSettings.DropDownItems.Add(nativeContent);
                    native.Items.Add(nativeSettings);
                    native.Items.Add("隐藏悬浮窗");
                    native.Items.Add("退出 ark_left");
                    native.Show(nativePoint);
                    Application.DoEvents();
                    Check("menu.nonCircleSettingsDirection", settingsBefore, nativeSettings.DropDownDirection);
                    Check("menu.nonCircleContentDirection", contentBefore, nativeContent.DropDownDirection);
                    nativeSettings.ShowDropDown();
                    nativeContent.ShowDropDown();
                    Application.DoEvents();
                    Check("menu.nonCircleSettingsDirectionAfterOpen", settingsAfter,
                        nativeSettings.DropDownDirection);
                    Check("menu.nonCircleContentDirectionAfterOpen", contentAfter,
                        nativeContent.DropDownDirection);
                }
                Check("menu.zeroQuery", queries, 0);
            }
        }

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

        private static bool ContainsText(System.Windows.Forms.Control root, string text)
        {
            if (root.Text.Contains(text)) return true;
            foreach (System.Windows.Forms.Control c in root.Controls) if (ContainsText(c, text)) return true;
            return false;
        }

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
                    // target; the refresh action is a details-menu item.
                    form.ContentForTest.Controls[0].Focus();
                    form.ContentForTest.AutoScrollPosition = new Point(0, 90);
                    Point scroll = form.ContentForTest.AutoScrollPosition;
                    System.Windows.Forms.Control card = form.ContentControls[0];
                    System.Windows.Forms.Control child = card.Controls[0];
                    string time = form.UpdateTimeTextForTest;
                    form.RefreshMenuItemForTest.PerformClick();
                    form.RefreshMenuItemForTest.PerformClick();
                    Check("actualRefresh.singleFlight", queries, 1);
                    Check("actualRefresh.menuItemEnabled", form.RefreshMenuItemForTest.Enabled, true);
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

                // The shared menu exposes settings, visibility and exit;
                // settings contains only the content chooser.
                Check("trayApp.menuPresent", app.MenuItemCountForTest, 3);
                Check("trayApp.menuTopSettings", app.MenuTextForTest(0), "设置");
                Check("trayApp.menuTopExit", app.MenuTextForTest(2), "退出 ark_left");
                Check("trayApp.submenuCount", app.MenuSettingsCountForTest, 1);
                Check("trayApp.menuHasContent", app.MenuSettingsTextForTest(0), "悬浮内容");
                Check("trayApp.menuHasToggle", app.MenuTextForTest(1), "隐藏悬浮窗");
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

        // ---- v0.4 floating circle / settings cases ----

        private static void FloatingSelectionCases()
        {
            // Agent Plan amounts 750/300/100 share a 1000 total, so all display 10%.
            QuotaSnapshot snap = SyntheticSample.BuildLarge();
            List<FloatingEntry> entries = FloatingSelection.Build(snap);
            Check("selection.count", entries.Count, 8);
            Check("selection.orderFirst", entries[0].Label, "5h");
            FloatingEntry def = FloatingSelection.Default(entries);
            Check("selection.defaultFirst", def.Label, "5h");
            Check("selection.defaultKnown", def.HasTrustedValue, true);
            CheckClose("selection.defaultPercent", def.Percent, 10.0);

            // Unknown before a known one: default skips to the first trusted value,
            // never the global minimum.
            QuotaSnapshot mixed = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true,
                    Period("weekly", null, null, null, null) + "," + Period("5h", "80", null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", "99", null, null, null))));
            List<FloatingEntry> mixedEntries = FloatingSelection.Build(mixed);
            FloatingEntry mixedDef = FloatingSelection.Default(mixedEntries);
            Check("selection.mixedSkipsUnknown", mixedDef.Label, "5h");
            CheckClose("selection.mixedNotMinimum", mixedDef.Percent, 20.0);

            // All unknown -> first selectable, shown unknown (no fake 0).
            QuotaSnapshot allUnknown = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("weekly", null, null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", null, null, null, null))));
            FloatingEntry unknownDef = FloatingSelection.Default(FloatingSelection.Build(allUnknown));
            Check("selection.allUnknownFirst", unknownDef.Label, "weekly");
            Check("selection.allUnknownNoValue", unknownDef.HasTrustedValue, false);
            Check("selection.allUnknownSelectable", unknownDef.Selectable, true);

            // No products -> no default.
            Check("selection.emptyDefault", FloatingSelection.Default(new List<FloatingEntry>()), null);

            // Unsubscribed / unknown subscription are never candidates.
            QuotaSnapshot unsub = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", false, Period("5h", "10", null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", "40", null, null, null))));
            List<FloatingEntry> unsubEntries = FloatingSelection.Build(unsub);
            List<FloatingEntry> cand = FloatingSelection.SelectableCandidates(unsubEntries);
            Check("selection.nonSubscribedExcluded", cand.Count, 1);
            Check("selection.nonSubscribedLabel", cand[0].Label, "monthly");

            // Period error keeps the entry listed but not selectable.
            QuotaSnapshot errSnap = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("5h", "10", null, null, null))));
            errSnap.Products[0].Periods[0].Error = "该周期查询失败。";
            FloatingEntry errEntry = FloatingSelection.Build(errSnap)[0];
            Check("selection.periodErrorStatus", errEntry.StatusText, "获取失败");
            Check("selection.periodErrorNotSelectable", errEntry.Selectable, false);
            Check("selection.periodErrorNotTrusted", errEntry.HasTrustedValue, false);

            // Product key distinguishes edition / tier (no cross-version collision).
            QuotaSnapshot a = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "small", true, Period("5h", "10", null, null, null))));
            QuotaSnapshot b = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "large", true, Period("5h", "10", null, null, null))));
            string keyA = FloatingSettingsStore.ProductKey(a.Products[0]);
            string keyB = FloatingSettingsStore.ProductKey(b.Products[0]);
            Check("selection.productKeyDiffersByTier", keyA == keyB, false);
            Check("selection.productKeyContent", keyA, "agent-plan|personal|small");

            // Settings ComboBox shows honest per-item state text.
            Check("selection.comboKnown", entries[0].ComboText.Contains("剩余 10%"), true);
            FloatingEntry unk = FloatingSelection.Build(allUnknown)[0];
            Check("selection.comboUnknown", unk.ComboText.Contains("剩余未知"), true);
            Check("selection.comboError", errEntry.ComboText.Contains("获取失败"), true);

            // Find honours both product key and label.
            FloatingSettings sel = new FloatingSettings {
                Version = 1, ProductKey = entries[0].ProductKey, PeriodLabel = "5h" };
            Check("selection.findSelected", FloatingSelection.Find(entries, sel).Label, "5h");
            sel.PeriodLabel = "weekly";
            Check("selection.findOtherPeriod", FloatingSelection.Find(entries, sel).Label, "weekly");
            sel.PeriodLabel = "missing";
            Check("selection.findMissing", FloatingSelection.Find(entries, sel), null);
        }

        private static void FloatingSettingsStoreCases()
        {
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_float_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                FloatingSettings s = new FloatingSettings();
                s.Version = FloatingSettingsStore.FormatVersion;
                s.ProductKey = "agent-plan|personal|medium";
                s.PeriodLabel = "5h";
                Check("floatSetting.save", FloatingSettingsStore.Save(s), true);
                FloatingSettings back = FloatingSettingsStore.Load();
                Check("floatSetting.loadKey", back.ProductKey, s.ProductKey);
                Check("floatSetting.loadPeriod", back.PeriodLabel, "5h");
                Check("floatSetting.loadVersion", back.Version, 1);

                // Corrupt JSON -> treated as not configured.
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath, "{not json", Encoding.UTF8);
                Check("floatSetting.corruptNull", FloatingSettingsStore.Load(), null);

                // Wrong version -> not configured.
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":2,\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongVersionNull", FloatingSettingsStore.Load(), null);

                // Empty fields / over-length -> rejected.
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":\"\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.emptyKeyNull", FloatingSettingsStore.Load(), null);
                // Extra / unknown fields are rejected (strict field count).
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\",\"Extra\":1}",
                    Encoding.UTF8);
                Check("floatSetting.extraFieldNull", FloatingSettingsStore.Load(), null);
                // Wrong types are rejected.
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":123,\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongTypeKeyNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":\"1\",\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongTypeVersionNull", FloatingSettingsStore.Load(), null);
                FloatingSettings longKey = new FloatingSettings {
                    Version = 1, ProductKey = new string('x', 129), PeriodLabel = "5h" };
                Check("floatSetting.longKeyReject", FloatingSettingsStore.Valid(longKey), false);
                FloatingSettings longPeriod = new FloatingSettings {
                    Version = 1, ProductKey = "a|b|c", PeriodLabel = new string('y', 65) };
                Check("floatSetting.longPeriodReject", FloatingSettingsStore.Valid(longPeriod), false);
                Check("floatSetting.nullReject", FloatingSettingsStore.Valid(null), false);

                // Save failure leaves the existing valid file intact.
                FloatingSettings valid = new FloatingSettings {
                    Version = 1, ProductKey = "a|b|c", PeriodLabel = "5h" };
                Check("floatSetting.resave", FloatingSettingsStore.Save(valid), true);
                using (System.IO.FileStream locked = new System.IO.FileStream(
                    FloatingSettingsStore.FilePath, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    FloatingSettings other = new FloatingSettings {
                        Version = 1, ProductKey = "z|y|x", PeriodLabel = "weekly" };
                    Check("floatSetting.replaceFailure", FloatingSettingsStore.Save(other), false);
                }
                Check("floatSetting.replaceRetains", FloatingSettingsStore.Load().ProductKey, "a|b|c");

                FloatingSettingsStore.Clear();
                Check("floatSetting.clear", FloatingSettingsStore.Load(), null);

                // The preference file is separate from the quota cache.
                Check("floatSetting.notCacheFile",
                    FloatingSettingsStore.FileName == "quota-cache.dat", false);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
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

        private static void FloatingSettingsModalCases()
        {
            // The REAL production path: the shared tray menu's "设置" item opens
            // the modal dialog; a Timer (no synthetic menu clicks) selects a
            // non-default target and saves. The circle must switch to that
            // target and NO query may run.
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.ApplyViewForTest(SyntheticSample.BuildLarge()); // data, zero query
                Check("settingsMenu.zeroBefore", queries, 0);
                System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer();
                saveTimer.Interval = 200;
                saveTimer.Tick += delegate
                {
                    saveTimer.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return; // marker: no dialog
                    dlg.SelectForTest(1); // weekly (30%), not the default 5h
                    if (!dlg.SaveForTest()) dlg.Close();
                };
                saveTimer.Start();
                app.OpenSettingsForTest(); // settings modal path, zero query
                saveTimer.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("settingsMenu.zeroAfterSave", queries, 0);
                Check("settingsMenu.savedPeriod", app.SelectedPeriodTextForTest, "weekly");
                Check("settingsMenu.circleSwitched",
                    app.CircleForTest.PercentTextForTest, "10%");
                Check("settingsMenu.dialogClosed", app.SettingsOpenForTest, false);

                // Cancel from the same production menu keeps the choice.
                System.Windows.Forms.Timer cancelTimer = new System.Windows.Forms.Timer();
                cancelTimer.Interval = 200;
                cancelTimer.Tick += delegate
                {
                    cancelTimer.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg != null) { dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel; dlg.Close(); }
                };
                cancelTimer.Start();
                app.OpenSettingsForTest();
                cancelTimer.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("settingsMenu.cancelKeeps", app.SelectedPeriodTextForTest, "weekly");
                Check("settingsMenu.cancelZeroQuery", queries, 0);
            }

            // A query is started exactly once to populate data (base = 1); the
            // subsequent settings save must NOT add another query.
            int q2 = 0;
            QueryOutcome outcome = new QueryOutcome();
            outcome.AuthConfirmed = true;
            outcome.Identity = PopupForm.SampleIdentity();
            outcome.AuthScope = QueryScope.FromAuth(outcome.Identity);
            outcome.Verdict = ScopeVerdict.Same;
            outcome.Snapshot = SyntheticSample.BuildLarge();
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { q2++; return System.Threading.Tasks.Task.FromResult(outcome); }))
            {
                app.RunQueryForTest();
                Check("settingsMenu.baseQueryOne", q2, 1);
                System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer();
                saveTimer.Interval = 200;
                saveTimer.Tick += delegate
                {
                    saveTimer.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg != null) { dlg.SelectForTest(1); dlg.SaveForTest(); }
                };
                saveTimer.Start();
                app.OpenSettingsForTest();
                saveTimer.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("settingsMenu.noExtraQueryAfterSave", q2, 1);
                Check("settingsMenu.viewSwitched", app.CircleForTest.PercentTextForTest, "10%");
            }
        }

        // UX015 (v0.7): settings entry / modal semantics. Injected 100 / 150 /
        // 200% layouts and narrow / short work areas are CHECKED here instead
        // of claimed human vision; the production modal path is driven through
        // the real tray menu with an injected query counter (no CLI account).
        private static void FloatingSettingsUX015Cases()
        {
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));

            // ---- injected-DPI layout matrix ----
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            int[] workW = new int[] { 1920, 2560, 3840 };
            int[] workH = new int[] { 1040, 1392, 2096 };
            for (int i = 0; i < scales.Length; i++)
            {
                Rectangle work = new Rectangle(0, 0, workW[i], workH[i]);
                string tag = "ux015.dpi" + (int)(scales[i] * 100);
                using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates,
                    null, delegate(FloatingSettings s) { return true; },
                    scales[i], work))
                {
                    IntPtr h = dlg.Handle; GC.KeepAlive(h);
                    Check(tag + ".scale", dlg.ScaleForTest, scales[i]);
                    Check(tag + ".insideWork", work.Contains(dlg.Bounds), true);
                    Check(tag + ".headerScaled", dlg.HeaderForTest.Height,
                        (int)Math.Round(46 * scales[i]));
                    Check(tag + ".saveInside",
                        dlg.ClientRectangle.Contains(dlg.SaveButtonForTest.Bounds), true);
                    Check(tag + ".cancelInside",
                        dlg.ClientRectangle.Contains(dlg.CancelButtonForTest.Bounds), true);
                    Check(tag + ".saveEnabled", dlg.SaveEnabledForTest, true);
                    Check(tag + ".noComboErrorOverlap",
                        dlg.ComboForTest.Bottom <= dlg.ErrorLabelForTest.Top, true);
                    Check(tag + ".buttonsBelowContent",
                        dlg.SaveButtonForTest.Top >= dlg.HostForTest.Bottom, true);
                    Check(tag + ".dropdownClamped",
                        dlg.DropDownWidthForTest <= work.Width - 20, true);
                    Check(tag + ".dropdownFloor", dlg.DropDownWidthForTest >= 100, true);
                    Check(tag + ".headerAboveContent",
                        dlg.HeaderForTest.Bottom <= dlg.HostForTest.Top, true);
                    Check(tag + ".contentAboveButtons",
                        dlg.HostForTest.Bottom <= dlg.SaveButtonForTest.Top, true);
                    Check(tag + ".noButtonOverlap",
                        dlg.SaveButtonForTest.Right <= dlg.CancelButtonForTest.Left, true);
                    Check(tag + ".headingInsideHeader",
                        dlg.HeadingForTest.Left >= 0
                        && dlg.HeadingForTest.Right <= dlg.CloseButtonForTest.Left
                        && dlg.HeadingForTest.Bottom <= dlg.HeaderForTest.Height, true);
                }
            }

            // ---- 100 / 150 / 200% combined with a 400x200 work area: the
            // effective layout scale must reflow (fonts and boxes together) so
            // header / content / buttons never overlap, everything stays inside
            // the window and the work area, and the dropdown never exceeds it ----
            for (int i = 0; i < scales.Length; i++)
            {
                Rectangle work = new Rectangle(0, 0, 400, 200);
                string tag = "ux015.small" + (int)(scales[i] * 100);
                using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates,
                    null, delegate(FloatingSettings s) { return true; },
                    scales[i], work))
                {
                    IntPtr h = dlg.Handle; GC.KeepAlive(h);
                    Check(tag + ".insideWork", work.Contains(dlg.Bounds), true);
                    Check(tag + ".headerAboveContent",
                        dlg.HeaderForTest.Bottom <= dlg.HostForTest.Top, true);
                    Check(tag + ".contentAboveButtons",
                        dlg.HostForTest.Bottom <= dlg.SaveButtonForTest.Top, true);
                    Check(tag + ".noButtonOverlap",
                        dlg.SaveButtonForTest.Right <= dlg.CancelButtonForTest.Left, true);
                    Check(tag + ".saveInside",
                        dlg.ClientRectangle.Contains(dlg.SaveButtonForTest.Bounds), true);
                    Check(tag + ".cancelInside",
                        dlg.ClientRectangle.Contains(dlg.CancelButtonForTest.Bounds), true);
                    Check(tag + ".saveEnabled", dlg.SaveEnabledForTest, true);
                    Check(tag + ".headingInsideHeader",
                        dlg.HeadingForTest.Left >= 0
                        && dlg.HeadingForTest.Right <= dlg.CloseButtonForTest.Left
                        && dlg.HeadingForTest.Bottom <= dlg.HeaderForTest.Height, true);
                    Check(tag + ".dropdownClamped",
                        dlg.DropDownWidthForTest <= work.Width - 20, true);
                }
            }

            // ---- narrow work area: dialog clamps, buttons stay usable ----
            Rectangle narrow = new Rectangle(0, 0, 400, 400);
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates,
                null, delegate(FloatingSettings s) { return true; }, 1.0, narrow))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("ux015.narrowInsideWork", narrow.Contains(dlg.Bounds), true);
                Check("ux015.narrowShrunk", dlg.ClientSize.Width < 430, true);
                Check("ux015.narrowSaveInside",
                    dlg.ClientRectangle.Contains(dlg.SaveButtonForTest.Bounds), true);
                Check("ux015.narrowCancelInside",
                    dlg.ClientRectangle.Contains(dlg.CancelButtonForTest.Bounds), true);
                Check("ux015.narrowNoButtonOverlap",
                    dlg.SaveButtonForTest.Right <= dlg.CancelButtonForTest.Left, true);
            }

            // ---- short work area: content scrolls, save / cancel stay visible ----
            Rectangle shortArea = new Rectangle(0, 0, 500, 200);
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates,
                null, delegate(FloatingSettings s) { return true; }, 1.0, shortArea))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("ux015.shortInsideWork", shortArea.Contains(dlg.Bounds), true);
                Check("ux015.shortSaveInside",
                    dlg.ClientRectangle.Contains(dlg.SaveButtonForTest.Bounds), true);
                Check("ux015.shortCancelInside",
                    dlg.ClientRectangle.Contains(dlg.CancelButtonForTest.Bounds), true);
                Check("ux015.shortContentScrolls",
                    dlg.ErrorLabelForTest.Bottom > dlg.HostForTest.Height, true);
            }

            // ---- long candidate text: full tooltip follows the selection and
            // the measured dropdown clamps to the work area ----
            FloatingEntry longEntry = new FloatingEntry();
            longEntry.ProductKey = "long|edition|tier";
            longEntry.ProductTitle =
                "Doubao-Seed-1.6-Thinking-250k Ultra Long Product Edition Name Extended";
            longEntry.PeriodTitle = "monthly";
            longEntry.ProductSubscribedKnown = true;
            longEntry.ProductSubscribed = true;
            longEntry.PercentKnown = true;
            longEntry.Percent = 42.0;
            List<FloatingEntry> longList = new List<FloatingEntry>();
            longList.Add(candidates[0]);
            longList.Add(longEntry);
            Rectangle smallWork = new Rectangle(0, 0, 500, 400);
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(longList,
                null, delegate(FloatingSettings s) { return true; }, 1.0, smallWork))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("ux015.longCount", dlg.CandidateCountForTest, 2);
                dlg.SelectForTest(0);
                Check("ux015.tipFollowsFirst",
                    dlg.TooltipTextForTest, dlg.CandidateTextForTest(0));
                dlg.SelectForTest(1);
                Check("ux015.tipFollowsLong",
                    dlg.TooltipTextForTest, dlg.CandidateTextForTest(1));
                Check("ux015.tipFullText",
                    dlg.TooltipTextForTest.Contains("Extended"), true);
                Check("ux015.longDropdownClamped",
                    dlg.DropDownWidthForTest <= 500 - 20, true);
                Check("ux015.longDropdownMeasured",
                    dlg.DropDownWidthForTest > dlg.ComboForTest.Width, true);
            }

            // ---- empty state: honest placeholder, save disabled ----
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(
                new List<FloatingEntry>(), null,
                delegate(FloatingSettings s) { return true; }, 1.0,
                new Rectangle(0, 0, 1920, 1040)))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("ux015.emptyPlaceholder", dlg.CandidateCountForTest, 1);
                Check("ux015.emptyText", dlg.CandidateTextForTest(0), "暂无数据");
                Check("ux015.emptySaveDisabled", dlg.SaveEnabledForTest, false);
                Check("ux015.emptySaveRefused", dlg.SaveForTest(), false);
                Check("ux015.emptyErrorRead", dlg.ErrorForTest.Contains("暂无数据"), true);
            }

            // ---- real keyboard traversal on a SHOWN dialog via
            // SelectNextControl: combo -> save -> cancel -> header close. The
            // full hint stays reachable through the scrollable content area. ----
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates,
                null, delegate(FloatingSettings s) { return true; }, 1.0,
                new Rectangle(0, 0, 1920, 1040)))
            {
                dlg.Show();
                System.Windows.Forms.Application.DoEvents();
                dlg.ActiveControl = dlg.ComboForTest;
                List<System.Windows.Forms.Control> seq =
                    new List<System.Windows.Forms.Control>();
                System.Windows.Forms.Control c = dlg.ActiveControl;
                for (int k = 0; k < 3; k++)
                {
                    dlg.SelectNextControl(c, true, true, true, true);
                    c = dlg.ActiveControl;
                    seq.Add(c);
                }
                Check("ux015.tabSeq.save", seq.Count == 3
                    && seq[0] == dlg.SaveButtonForTest, true);
                Check("ux015.tabSeq.cancel", seq.Count == 3
                    && seq[1] == dlg.CancelButtonForTest, true);
                Check("ux015.tabSeq.close", seq.Count == 3
                    && seq[2] == dlg.CloseButtonForTest, true);
                Check("ux015.hintReachable",
                    dlg.HostForTest.DisplayRectangle.Contains(dlg.HintForTest.Bounds),
                    true);
                dlg.Close();
            }

            // ---- production path: circle / tray opens settings owned by the
            // REAL circle surface (screen / DPI follow the circle) WITHOUT
            // forcing the details panel, zero query on open and cancel ----
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                app.OpenEntryForTest("tray"); // circle visible, details not
                app.HideDetailsForTest();
                Check("ux015.circleOnlyDetailsHidden", app.DetailsVisibleForTest, false);
                System.Windows.Forms.Timer closeTimer = new System.Windows.Forms.Timer();
                closeTimer.Interval = 200;
                bool seenDuringModal = false;
                closeTimer.Tick += delegate
                {
                    closeTimer.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    seenDuringModal = true;
                    Check("ux015.circleOnlyStillHidden", app.DetailsVisibleForTest, false);
                    // The dialog must live on the REAL circle's screen with the
                    // circle screen's scale — not the hidden 1x1 wrapper's.
                    System.Windows.Forms.Screen dlgScreen =
                        System.Windows.Forms.Screen.FromControl(dlg);
                    System.Windows.Forms.Screen circleScreen =
                        System.Windows.Forms.Screen.FromControl(app.CircleForTest);
                    Check("ux015.circleOwnerScreen",
                        dlgScreen.DeviceName, circleScreen.DeviceName);
                    Check("ux015.circleOwnerScale", dlg.ScaleForTest,
                        DpiUtil.GetScale(circleScreen));
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                closeTimer.Start();
                app.OpenSettingsForTest(); // settings modal path, zero query
                closeTimer.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux015.circleOnlyModalSeen", seenDuringModal, true);
                Check("ux015.circleOnlyNoDetails", app.DetailsVisibleForTest, false);
                Check("ux015.circleOnlyStaysVisible", app.FloatingVisibleForTest, true);
                Check("ux015.circleOnlyClosed", app.SettingsOpenForTest, false);
                Check("ux015.circleOnlyZeroQuery", queries, 0);

                // Neither circle nor details visible: settings still opens with
                // NOTHING forced open, still zero query.
                app.ToggleFloatingForTest(); // circle shown -> hidden
                Check("ux015.nothingCircleHidden", app.FloatingVisibleForTest, false);
                System.Windows.Forms.Timer t2b = new System.Windows.Forms.Timer();
                t2b.Interval = 200;
                bool seenNothing = false;
                t2b.Tick += delegate
                {
                    t2b.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    seenNothing = true;
                    Check("ux015.nothingStillNoDetails", app.DetailsVisibleForTest, false);
                    Check("ux015.nothingStillNoCircle", app.FloatingVisibleForTest, false);
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t2b.Start();
                app.OpenSettingsForTest();
                t2b.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux015.nothingModalSeen", seenNothing, true);
                Check("ux015.nothingForced", app.DetailsVisibleForTest, false);
                Check("ux015.nothingCircleForced", app.FloatingVisibleForTest, false);
                Check("ux015.nothingClosed", app.SettingsOpenForTest, false);
                Check("ux015.nothingZeroQuery", queries, 0);
            }

            // ---- production path: details visible. The modal must not hide
            // the details, a re-entrant repeat request must not release the
            // suppression early, and closing returns focus + scroll, zero
            // query. ----
            int q2 = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { q2++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                app.OpenEntryForTest("tray");
                app.ShowDetailsForTest(); // left-click details path, zero query
                System.Windows.Forms.Application.DoEvents();
                Check("ux015.detailsShown", app.DetailsVisibleForTest, true);
                PopupForm details = app.DetailsFormForTest;
                details.ContentForTest.Controls[0].Focus();
                details.ContentForTest.AutoScrollPosition = new Point(0, 90);
                Point scrollBefore = details.ContentForTest.AutoScrollPosition;
                System.Windows.Forms.Control focusBefore = details.ActiveControlForTest;
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 200;
                bool reached = false;
                t.Tick += delegate
                {
                    t.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    reached = true;
                    // The details must survive the modal's activation change.
                    Check("ux015.duringModalDetails", app.DetailsVisibleForTest, true);
                    Check("ux015.duringModalSuppressed", details.DialogOpenForTest, true);
                    // Re-entrant repeat request: only activates the open modal
                    // and must NOT release the details suppression (its
                    // try/finally returns while the outer ShowDialog blocks).
                    app.OpenSettingsForTest();
                    Check("ux015.repeatStillOpen", app.SettingsOpenForTest, true);
                    Check("ux015.repeatKeepsSuppression", details.DialogOpenForTest, true);
                    // Cancel semantics: close without saving anything.
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t.Start();
                app.OpenSettingsForTest(); // settings modal while details visible
                t.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux015.modalReached", reached, true);
                Check("ux015.afterModalDetails", app.DetailsVisibleForTest, true);
                Check("ux015.afterModalSuppressionOff", details.DialogOpenForTest, false);
                Check("ux015.afterModalClosed", app.SettingsOpenForTest, false);
                Check("ux015.afterModalZeroQuery", q2, 0);
                Check("ux015.scrollRestored",
                    details.ContentForTest.AutoScrollPosition, scrollBefore);
                Check("ux015.focusRestored",
                    details.ActiveControlForTest, focusBefore);

                // Second cycle with a ZERO captured scroll: the restore must
                // still run explicitly (activate -> focus -> scroll incl. 0).
                details.ContentForTest.AutoScrollPosition = new Point(0, 0);
                details.ContentForTest.Controls[0].Focus();
                System.Windows.Forms.Timer t4 = new System.Windows.Forms.Timer();
                t4.Interval = 200;
                bool reached4 = false;
                t4.Tick += delegate
                {
                    t4.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    reached4 = true;
                    dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    dlg.Close();
                };
                t4.Start();
                app.OpenSettingsForTest();
                t4.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux015.zeroScrollModal", reached4, true);
                Check("ux015.zeroScrollRestored",
                    details.ContentForTest.AutoScrollPosition, new Point(0, 0));
                Check("ux015.zeroScrollFocus",
                    details.ActiveControlForTest
                        == (System.Windows.Forms.Control)details.ContentForTest.Controls[0],
                    true);
            }

            // ---- restore guard: a disposed details form is never
            // reactivated (no throw, no resurrection) ----
            using (PopupForm pf = new PopupForm())
            {
                pf.SetDialogOpen(true);
                pf.Dispose();
                pf.SetDialogOpen(false);
                Check("ux015.restoreDisposedNoThrow", true, true);
            }

            // ---- production path: real store save failure keeps the modal
            // open with an inline error. The floating form is the PRODUCTION
            // construction (no injected store, no query channel at all); the
            // preference file is locked (FileShare.Read) so the atomic
            // FloatingSettingsStore.Save fails for real. ----
            string oldDir = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "arkleft-ux015-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                using (FloatingQuotaForm floating = new FloatingQuotaForm(null, null))
                {
                    IntPtr fh = floating.Handle; GC.KeepAlive(fh);
                    PanelModel model = new PanelModel();
                    model.OnAuthResult(true, PopupForm.SampleIdentity(),
                        QuotaStatus.Ok, null);
                    floating.ApplyModelView(model.OnUsageResult(
                        SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null));
                    FloatingSettings seed = new FloatingSettings();
                    seed.Version = FloatingSettingsStore.FormatVersion;
                    seed.ProductKey = "ux015|seed|key";
                    seed.PeriodLabel = "5h";
                    Check("ux015.seedSaved", FloatingSettingsStore.Save(seed), true);
                    System.IO.FileStream fs = new System.IO.FileStream(
                        FloatingSettingsStore.FilePath, System.IO.FileMode.Open,
                        System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    try
                    {
                        System.Windows.Forms.Timer t3 = new System.Windows.Forms.Timer();
                        t3.Interval = 200;
                        bool failed = false;
                        string inlineError = null;
                        t3.Tick += delegate
                        {
                            t3.Stop();
                            FloatingSettingsForm dlg = FindOpenSettings();
                            if (dlg == null) return;
                            dlg.SelectForTest(0);
                            if (!dlg.SaveForTest())
                            {
                                failed = true;
                                inlineError = dlg.ErrorForTest;
                            }
                            // The dialog must still be open after the failure.
                            Check("ux015.saveFailStaysOpen",
                                floating.SettingsOpenForTest, true);
                            dlg.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                            dlg.Close();
                        };
                        t3.Start();
                        floating.OpenSettings(); // real production modal
                        t3.Dispose();
                        System.Windows.Forms.Application.DoEvents();
                        Check("ux015.saveFailed", failed, true);
                        Check("ux015.saveFailInline",
                            inlineError != null && inlineError.Contains("保存失败"), true);
                    }
                    finally
                    {
                        fs.Dispose();
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, oldDir);
                try { System.IO.Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        private static FloatingSettingsForm FindOpenSettings()
        {
            foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
                if (f is FloatingSettingsForm) return (FloatingSettingsForm)f;
            return null;
        }

        private static void FloatingLayoutCases()
        {
            // Normal right-bottom circle: details go to the LEFT, never covering.
            System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1080);
            System.Drawing.Rectangle circle = new System.Drawing.Rectangle(1768, 928, 136, 136);
            System.Drawing.Rectangle details = new System.Drawing.Rectangle(0, 0, 440, 560);
            FloatingLayout.Choose(circle, details, work);
            Check("layout.normalLeft", FloatingLayout.Choose(circle, details, work), DetailsPlacement.Left);
            System.Drawing.Rectangle L = FloatingLayout.Bounds(DetailsPlacement.Left, circle, details, work);
            Check("layout.normalNoOverlap", L.Right <= circle.Left, true);
            Check("layout.normalInside", CircleWithin(L, work), true);

            // Left edge circle: left does not fit -> fall back to the right.
            System.Drawing.Rectangle leftCircle = new System.Drawing.Rectangle(0, 400, 136, 136);
            Check("layout.leftFallbackRight",
                FloatingLayout.Choose(leftCircle, details, work), DetailsPlacement.Right);

            // Only room above (circle at the bottom, no left/right space).
            System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(200, 0, 460, 1080);
            System.Drawing.Rectangle bottomCircle = new System.Drawing.Rectangle(310, 930, 136, 136);
            Check("layout.aboveFallback",
                FloatingLayout.Choose(bottomCircle, details, narrow), DetailsPlacement.Above);

            // No side fits at all -> temporarily hide the circle.
            System.Drawing.Rectangle tiny = new System.Drawing.Rectangle(0, 0, 300, 300);
            System.Drawing.Rectangle midCircle = new System.Drawing.Rectangle(82, 82, 136, 136);
            Check("layout.noSpaceHide",
                FloatingLayout.Choose(midCircle, details, tiny), DetailsPlacement.HideCircle);

            // Negative-coordinate multi-monitor work area still resolves.
            System.Drawing.Rectangle negWork = new System.Drawing.Rectangle(-1920, 0, 1920, 1080);
            System.Drawing.Rectangle negCircle = new System.Drawing.Rectangle(-288, 928, 136, 136);
            Check("layout.negativeLeft",
                FloatingLayout.Choose(negCircle, details, negWork), DetailsPlacement.Left);
            System.Drawing.Rectangle nl = FloatingLayout.Bounds(DetailsPlacement.Left, negCircle, details, negWork);
            Check("layout.negativeInside", CircleWithin(nl, negWork), true);
        }

        private static bool CircleWithin(System.Drawing.Rectangle b, System.Drawing.Rectangle wa)
        {
            return b.Left >= wa.Left && b.Top >= wa.Top
                && b.Right <= wa.Right && b.Bottom <= wa.Bottom;
        }

        // Exercises the REAL PrepareDetails consumption (not just the
        // FloatingLayout enum): every branch, including HideCircle, must leave
        // PendingDetailsBounds non-zero, containing the details size, inside the
        // work area, and it must survive RestoreAfterDetails only for auto-hide.
        private static void FloatingPrepareDetailsCases()
        {
            System.Drawing.Size details = new System.Drawing.Size(440, 560);

            // Normal case on a wide work area -> Left, non-zero, size kept.
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                form.ShowCircleAtForTest(work, 1.0);
                DetailsPlacement p = form.PrepareDetails(details, work);
                System.Drawing.Rectangle b = form.PendingDetailsBounds;
                Check("prepare.positionLeft", p, DetailsPlacement.Left);
                Check("prepare.nonEmpty", b.Width > 0 && b.Height > 0, true);
                Check("prepare.sizeKept", b.Size, details);
                Check("prepare.inside", CircleWithin(b, work), true);
            }

            // Work area large enough to CONTAIN the details but positioned so
            // every side intersects the circle -> HideCircle, yet the pending
            // bounds must STILL be finite, non-zero and contain the size (this is
            // the original bug: an empty rectangle rendered the panel invisible).
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 500, 700);
                form.ShowCircleAtForTest(narrow, 1.0);
                DetailsPlacement p = form.PrepareDetails(details, narrow);
                System.Drawing.Rectangle b = form.PendingDetailsBounds;
                Check("prepare.narrowHide", p, DetailsPlacement.HideCircle);
                Check("prepare.hideNonEmpty", b.Width > 0 && b.Height > 0, true);
                Check("prepare.hideSizeKept", b.Size, details);
                Check("prepare.hideInside", CircleWithin(b, narrow), true);

                // HideCircle auto-hid the circle; closing details restores it.
                Check("prepare.circleAutoHidden", form.CircleVisible, false);
                form.RestoreAfterDetails();
                Check("prepare.autoRestored", form.CircleVisible, true);
            }

            // Explicit user hide must NOT be restored by a details close.
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 300, 300);
                form.ShowCircleAtForTest(narrow, 1.0);
                form.HideCircle();              // explicit hide first
                form.RestoreAfterDetails();     // details close
                Check("prepare.explicitHideNotRestored", form.CircleVisible, false);

                // After an explicit show, a later auto-hide is still restorable.
                form.ShowCircle();
                Check("prepare.explicitShowVisible", form.CircleVisible, true);
            }

            // ShowCircle cancels the auto-hidden flag (no stale restore).
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 300, 300);
                form.ShowCircleAtForTest(narrow, 1.0);
                form.PrepareDetails(details, narrow);
                Check("prepare.autoHiddenBeforeShow", form.CircleVisible, false);
                form.ShowCircle();              // user explicitly shows again
                form.RestoreAfterDetails();     // must not change anything
                Check("prepare.showCancelsAutoFlag", form.CircleVisible, true);
            }
        }

        private static void FloatingSettingsDialogCases()
        {
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));
            // Candidates include the never-subscribed / errored entries check.
            QuotaSnapshot snap = SyntheticSample.BuildLarge();
            snap.Products[0].Periods[1].Error = "该周期查询失败。";
            snap.Products[2].Periods[1].PercentKnown = false;
            List<FloatingEntry> withError = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(snap));
            FloatingSettings saved = null;
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(withError, null,
                delegate(FloatingSettings s) { saved = s; return true; }))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("settingsDialog.itemCount", dlg.CandidateCountForTest, 8);
                Check("settingsDialog.unknownPresent",
                    ContainsItem(dlg, "剩余未知"), true);
                Check("settingsDialog.errorPresent",
                    ContainsItem(dlg, "获取失败"), true);
                // Saving an errored entry is refused; the dialog stays open.
                int errIndex = IndexOfItem(dlg, "获取失败");
                Check("settingsDialog.selectError", dlg.SelectForTest(errIndex), true);
                Check("settingsDialog.saveRefused", dlg.SaveForTest(), false);
                Check("settingsDialog.refusedMessage",
                    dlg.ErrorForTest.Contains("不可用"), true);
                // Saving the first valid known entry succeeds and writes exactly
                // product key + period label (no identity / credentials).
                Check("settingsDialog.selectValid", dlg.SelectForTest(0), true);
                Check("settingsDialog.saveOk", dlg.SaveForTest(), true);
                Check("settingsDialog.savedProductKey", saved.ProductKey, candidates[0].ProductKey);
                Check("settingsDialog.savedPeriod", saved.PeriodLabel, candidates[0].Label);
                Check("settingsDialog.savedVersion", saved.Version, 1);
            }

            // Injected failing save surfaces a short error and does not close.
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return false; }))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("settingsDialog.selectValid2", dlg.SelectForTest(0), true);
                Check("settingsDialog.saveFailureReturnsFalse", dlg.SaveForTest(), false);
                Check("settingsDialog.saveFailureMessage",
                    dlg.ErrorForTest.Contains("保存失败"), true);
            }
        }

        private static bool ContainsItem(FloatingSettingsForm dlg, string text)
        {
            return IndexOfItem(dlg, text) >= 0;
        }

        private static int IndexOfItem(FloatingSettingsForm dlg, string text)
        {
            for (int i = 0; i < dlg.CandidateCountForTest; i++)
            {
                string s = dlg.CandidateTextForTest(i);
                if (s != null && s.Contains(text)) return i;
            }
            return -1;
        }

        private static void FloatingDisplayCases()
        {
            // 0 -> "0%", 100 -> "100%", <1 -> "<1%", 99.6 does not fake 100.
            Check("circle.zero", FloatingQuotaForm.CirclePercent(0), "0%");
            Check("circle.full", FloatingQuotaForm.CirclePercent(100), "100%");
            Check("circle.sub1", FloatingQuotaForm.CirclePercent(0.4), "<1%");
            Check("circle.half", FloatingQuotaForm.CirclePercent(50), "50%");
            Check("circle.noFakeFull", FloatingQuotaForm.CirclePercent(99.6), "99%");
            FloatingAmountCases();
        }

        private static void FloatingAmountCases()
        {
            QuotaSnapshot snap = new QuotaSnapshot();
            ProductQuota product = new ProductQuota { Product = "agent-plan",
                SubscribedKnown = true, Subscribed = true };
            PeriodQuota period = EffectivePeriod("5h", 9703.75, 10000);
            product.Periods.Add(period);
            snap.Products.Add(product);
            PanelView view = new PanelView { Data = snap, State = PanelState.ShowingCurrent };
            Rectangle area = Screen.PrimaryScreen.WorkingArea;

            using (FloatingQuotaForm floating = new FloatingQuotaFormForData(snap))
            {
                FloatingCircleControl circle = floating.CircleForTest;
                circle.SetReduceMotion(true);
                circle.ShowAt(area, 1.0);
                Check("amount.normal.percent", circle.PercentTextForTest, "97%");
                Check("amount.normal.text", circle.AmountTextForTest, "9703.75 AFP");
                Check("amount.normal.fitted", circle.FittedAmountTextForTest, "9703.75 AFP");
                Check("amount.normal.tooltipProduct", circle.TooltipForTest.Contains("Agent Plan"), true);
                Check("amount.normal.tooltipPeriod", circle.TooltipForTest.Contains("5 小时"), true);

                period.RemainingAmount = 0;
                floating.ApplyModelView(view);
                Check("amount.zero.known", floating.SelectedEntryForTest.AmountKnown, true);
                Check("amount.zero.percent", circle.PercentTextForTest, "0%");
                Check("amount.zero.text", circle.AmountTextForTest, "0 AFP");

                period.AmountKnown = false;
                period.RemainingAmount = 9703.75;
                period.RemainingPercent = 97;
                floating.ApplyModelView(view);
                Check("amount.unknown.entry", floating.SelectedEntryForTest.AmountKnown, false);
                Check("amount.unknown.percentStillKnown", circle.PercentTextForTest, "97%");
                Check("amount.unknown.text", circle.AmountTextForTest, "AFP 未知");
                period.PercentKnown = false;
                floating.ApplyModelView(view);
                Check("amount.bothUnknown.percent", circle.PercentTextForTest, "剩余未知");
                Check("amount.bothUnknown.text", circle.AmountTextForTest, "AFP 未知");

                period.AmountKnown = true;
                period.TotalKnown = false;
                floating.ApplyModelView(view);
                Check("amount.percentUnknown.percent", circle.PercentTextForTest, "剩余未知");
                Check("amount.percentUnknown.text", circle.AmountTextForTest, "9703.75 AFP");
                period.TotalKnown = true;

                product.Periods.Add(EffectivePeriod("weekly", 9000, 20000));
                product.Periods.Add(EffectivePeriod("monthly", 1234.5, 30000));
                floating.ApplyModelView(view);
                FloatingEntry entry = floating.SelectedEntryForTest;
                Check("amount.effective.selected", entry.Label, "5h");
                CheckClose("amount.effective.value", entry.RemainingAmount, 1234.5);
                CheckClose("amount.effective.percent", entry.Percent, 12.345);
                Check("amount.effective.text", circle.AmountTextForTest, "1234.5 AFP");
                Check("amount.effective.percentText", circle.PercentTextForTest, "12%");
                CheckClose("amount.effective.rawUnchanged", period.RemainingAmount, 9703.75);

                FloatingSettings weekly = new FloatingSettings { Version = 1,
                    ProductKey = FloatingSettingsStore.ProductKey(product), PeriodLabel = "weekly" };
                using (FloatingQuotaForm selected = new FloatingQuotaFormForData(snap, weekly))
                {
                    Check("amount.selected.period", selected.SelectedEntryForTest.Label, "weekly");
                    Check("amount.selected.percent", selected.CircleForTest.PercentTextForTest, "6%");
                    Check("amount.selected.text", selected.CircleForTest.AmountTextForTest, "1234.5 AFP");
                    product.Periods[1].Error = "该周期查询失败。";
                    selected.ApplyModelView(view);
                    Check("amount.error.text", selected.CircleForTest.AmountTextForTest, "AFP 未知");
                    Check("amount.error.percent", selected.CircleForTest.PercentTextForTest, "剩余未知");
                }
                product.Periods.RemoveRange(1, 2);

                foreach (double amount in new double[] { 9703.75, 123456789012345.67, double.MaxValue })
                {
                    period.RemainingAmount = amount;
                    period.Total = amount / 0.97;
                    period.RemainingPercent = 97;
                    period.PercentKnown = true;
                    floating.ApplyModelView(view);
                    foreach (double scale in new double[] { 1.0, 1.5, 2.0 })
                    {
                        circle.ShowAt(area, scale);
                        string tag = "amount.fit." + amount + "." + scale;
                        string text = circle.FittedAmountTextForTest;
                        Rectangle bounds = circle.AmountBoundsForTest;
                        Check(tag + ".width", TextRenderer.MeasureText(text, circle.AmountFontForTest).Width
                            <= bounds.Width - 2, true);
                        Check(tag + ".height", circle.AmountFontForTest.Height <= bounds.Height, true);
                        Check(tag + ".topLeftInside", circle.Region.IsVisible(bounds.Left, bounds.Top), true);
                        Check(tag + ".bottomRightInside", circle.Region.IsVisible(bounds.Right, bounds.Bottom), true);
                        Check(tag + ".scientific", text.Contains("E+"), amount > 1e12);
                        Check(tag + ".unit", text.EndsWith(" AFP", StringComparison.Ordinal), true);
                        Check(tag + ".fullTooltip", circle.TooltipForTest.Contains(
                            DisplayNames.Number(amount) + " AFP"), true);
                        using (Bitmap bitmap = new Bitmap(circle.Width, circle.Height))
                        {
                            circle.DrawToBitmap(bitmap, circle.ClientRectangle);
                            bool amountInk = false, oldCaptionInk = false;
                            for (int y = bounds.Top; y < circle.Height; y++)
                                for (int x = 0; x < circle.Width; x++)
                                    if (bitmap.GetPixel(x, y).ToArgb() == UiStyle.CircleCaption.ToArgb())
                                    {
                                        if (bounds.Contains(x, y)) amountInk = true;
                                        else oldCaptionInk = true;
                                    }
                            Check(tag + ".painted", amountInk, true);
                            Check(tag + ".noExtraCaption", oldCaptionInk, false);
                            if (scale == 1.0)
                                bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                    amount == 9703.75 ? "preview-floating-amount.png"
                                        : amount == double.MaxValue ? "preview-floating-amount-max.png"
                                        : "preview-floating-amount-long.png"));
                        }
                    }
                }
                floating.ApplyModelView(EmptyView());
                Check("amount.noData.cleared", circle.AmountTextForTest, "");
            }

            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                foreach (double amount in new double[] { 9703.75, 0, double.MaxValue })
                {
                    period.AmountKnown = true;
                    period.RemainingAmount = amount;
                    app.ApplyViewForTest(snap);
                    Check("amount.query.updated", app.CircleForTest.AmountTextForTest,
                        DisplayNames.Number(amount) + " AFP");
                }
                period.AmountKnown = false;
                app.ApplyViewForTest(snap);
                Check("amount.query.unknown", app.CircleForTest.AmountTextForTest, "AFP 未知");
                app.OpenEntryForTest("tray");
                using (Bitmap bitmap = new Bitmap(app.CircleForTest.Width, app.CircleForTest.Height))
                    app.CircleForTest.DrawToBitmap(bitmap, app.CircleForTest.ClientRectangle);
                Check("amount.query.zero", queries, 0);
            }
        }

        private static void FloatingWindowCases()
        {
            // Injection avoids any real preference IO.
            using (FloatingQuotaForm floating = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; }))
            {
                IntPtr h = floating.Handle; GC.KeepAlive(h);
                Check("floating.noEntryNoData", floating.EntryCountForTest, 0);
                FloatingDisplay empty = new FloatingDisplay();
                empty.PercentText = "暂无数据";
                empty.HasData = false;
                floating.ApplyModelView(EmptyView());
                Check("floating.emptyNoData", floating.EntryCountForTest, 0);

                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                PanelView view = model.OnUsageResult(
                    SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
                floating.ApplyModelView(view);
                Check("floating.entryCount", floating.EntryCountForTest, 8);
                FloatingEntry selected = floating.SelectedEntryForTest;
                Check("floating.selectedFirst", selected.Label, "5h");
                Check("floating.selectedPercentText", floating.CircleForTest.PercentTextForTest, "10%");
                Check("floating.selectedTrusted", floating.CircleForTest.PercentKnownForTest, true);
                Check("floating.defaultNotStored", floating.StoredSettingsForTest, null);
                Check("floating.amountCaption", floating.CircleForTest.AmountTextForTest, "100 AFP");
                Check("floating.tooltipIncludesProduct",
                    floating.CircleForTest.TooltipForTest.Contains("Agent Plan"), true);
                Check("floating.tooltipIncludesPeriod",
                    floating.CircleForTest.TooltipForTest.Contains("5 小时"), true);

                // Tooltip surfaces the PanelView's own safe freshness status and
                // never fabricates a confirmed update for a limited result.
                PanelView restricted = new PanelView();
                restricted.Data = SyntheticSample.BuildLarge();
                restricted.Note = "身份未完全确认";
                restricted.IdentityUnknown = true;
                restricted.State = PanelState.ShowingCurrent;
                restricted.FromCache = false;
                floating.ApplyModelView(restricted);
                Check("floating.tooltipNote",
                    floating.CircleForTest.TooltipForTest.Contains("身份未完全确认"), true);
                PanelView cached = new PanelView();
                cached.Data = SyntheticSample.BuildLarge();
                cached.FromCache = true;
                cached.Note = "上次更新数据";
                cached.State = PanelState.ShowingCurrent;
                floating.ApplyModelView(cached);
                Check("floating.tooltipCache",
                    floating.CircleForTest.TooltipForTest.Contains("上次更新数据"), true);
                // No raw identity / credential text can appear.
                Check("floating.tooltipNoId",
                    floating.CircleForTest.TooltipForTest.Contains("owner_trn"), false);

                // Unknown percent -> neutral, wave stopped, no fake 0.
                FloatingQuotaForm unknownForm = new FloatingQuotaFormForData(AllUnknownSnapshot());
                Check("floating.unknownText", unknownForm.CircleForTest.PercentTextForTest, "剩余未知");
                Check("floating.unknownKnownFlag", unknownForm.CircleForTest.PercentKnownForTest, false);
                Check("floating.unknownWaveStopped", unknownForm.CircleForTest.WaveRunningForTest, false);
                unknownForm.Dispose();

                // A real drag (production mouse handlers) over the threshold must
                // NOT open details; a click must. The drag clamp targets the real
                // screen work area (Screen.FromPoint), so use that as the bound.
                System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                floating.ShowCircleAtForTest(area, 1.0);
                FloatingCircleControl circle = floating.CircleForTest;
                int opened = 0;
                circle.DetailsRequested += delegate { opened++; };
                Check("floating.circleInitialInside", CircleWithin(circle.Bounds, area), true);
                System.Drawing.Point start = circle.Bounds.Location;
                System.Drawing.Point p0 = new System.Drawing.Point(start.X + 20, start.Y + 20);
                // Small move (< 4dp threshold): treated as a click.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 2, p0.Y + 2));
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 2, p0.Y + 2));
                Check("floating.clickOpens", opened, 1);
                Check("floating.clickBoundsUnchanged", circle.Bounds.Location, start);

                // Large move (> threshold): drag, no details, still inside the area.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("floating.dragMoved", circle.Bounds.Location != start, true);
                Check("floating.dragInside", CircleWithin(circle.Bounds, area), true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("floating.dragNoDetails", opened, 1);

                // Drag beyond the right/bottom edge clamps inside the work area.
                circle.SimulateMouseDownForTest(new System.Drawing.Point(
                    circle.Bounds.Left + 10, circle.Bounds.Top + 10));
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(5000, 5000));
                Check("floating.dragClamped", CircleWithin(circle.Bounds, area), true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(5000, 5000));

                // Selection missing -> "暂无数据", stored choice retained.
                FloatingQuotaForm missing = new FloatingQuotaFormForData(
                    SyntheticSample.BuildLarge(), new FloatingSettings {
                        Version = 1, ProductKey = "ghost|none|none", PeriodLabel = "5h" });
                Check("floating.missingNoData",
                    missing.CircleForTest.PercentTextForTest, "暂无数据");
                Check("floating.missingKeepsChoice",
                    missing.StoredSettingsForTest.ProductKey, "ghost|none|none");
                missing.Dispose();

                // Drifted position is preserved on re-show within the session.
                floating.ShowCircle();
                System.Drawing.Rectangle moved = new System.Drawing.Rectangle(
                    floating.CircleForTest.Bounds.Left + 30,
                    floating.CircleForTest.Bounds.Top + 30,
                    floating.CircleForTest.Width, floating.CircleForTest.Height);
                floating.CircleForTest.Bounds = moved;
                floating.HideCircleForTest();
                floating.ShowCircle();
                Check("floating.dragPositionKept",
                    floating.CircleForTest.Bounds.Location, moved.Location);

                // Identity clear (data evicted) syncs the circle to "暂无数据".
                PanelModel cleared = new PanelModel();
                cleared.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
                floating.ApplyModelView(cleared.CurrentView);
                Check("floating.identityClearNoData",
                    floating.CircleForTest.PercentTextForTest, "暂无数据");
                Check("floating.identityClearEntries", floating.EntryCountForTest, 0);

                // Partial error: a period-error entry is listed but not trusted;
                // the default skips it for the first valid known percentage.
                QuotaSnapshot partial = QuotaParser.Parse(Wrap(
                    Item("agent-plan", "personal", "medium", true,
                        Period("5h", "50", null, null, null))));
                partial.Products[0].Periods[0].Error = "该周期查询失败。";
                partial.Products.Add(QuotaParser.Parse(Wrap(
                    Item("coding-plan", "personal", null, true,
                        Period("monthly", "30", null, null, null)))).Products[0]);
                partial.Status = QuotaStatus.PartialError;
                List<FloatingEntry> pe = FloatingSelection.Build(partial);
                FloatingEntry peDef = FloatingSelection.Default(pe);
                Check("floating.partialDefaultSkipsError", peDef.Label, "monthly");
                Check("floating.partialErrorListedWithState", pe[0].StatusText, "获取失败");
                FloatingQuotaForm partialForm = new FloatingQuotaFormForData(partial);
                Check("floating.partialShowsKnown",
                    partialForm.CircleForTest.PercentTextForTest, "70%");
                partialForm.Dispose();

                // 0% and 100% are exact values but the wave animation only runs
                // for 0 < p < 100 (empty / full are static).
                FloatingQuotaForm z = new FloatingQuotaFormForData(PercentSnapshot(100));
                Check("floating.zeroExact", z.CircleForTest.PercentTextForTest, "0%");
                Check("floating.zeroWaveStopped", z.CircleForTest.WaveRunningForTest, false);
                z.HideCircleForTest();
                z.Dispose();
                FloatingQuotaForm full = new FloatingQuotaFormForData(PercentSnapshot(0));
                Check("floating.fullExact", full.CircleForTest.PercentTextForTest, "100%");
                Check("floating.fullWaveStopped", full.CircleForTest.WaveRunningForTest, false);
                full.HideCircleForTest();
                full.Dispose();

                // NaN / Infinity are never rendered as a trusted value.
                QuotaSnapshot nan = PercentSnapshot(50);
                nan.Products[0].Periods[0].RemainingPercent = double.NaN;
                Check("floating.nanUntrusted",
                    FloatingSelection.Build(nan)[0].HasTrustedValue, false);
                QuotaSnapshot inf = PercentSnapshot(50);
                inf.Products[0].Periods[0].RemainingPercent = double.PositiveInfinity;
                Check("floating.infUntrusted",
                    FloatingSelection.Build(inf)[0].HasTrustedValue, false);
            }
        }

        // Helper form that applies arbitrary data with injected no-op prefs.
        // ---- v0.8 UX016: position lock + floating-preferences.json (format 1
        // at the time; superseded by format 2 in v0.9 UX017 - see the UX017
        // cases below) ----

        private static void FloatingPreferencesUX016Cases()
        {
            // ---- store: strict decode / round-trip / atomic write ----
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_prefs_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                Check("prefs.missingNull", FloatingPreferencesStore.Load(), null);
                Check("prefs.validNull", FloatingPreferencesStore.Valid(null), false);

                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = true;
                Check("prefs.save", FloatingPreferencesStore.Save(p), true);
                FloatingPreferences back = FloatingPreferencesStore.Load();
                Check("prefs.loadLocked", back.PositionLocked, true);
                Check("prefs.loadVersion", back.Version,
                    FloatingPreferencesStore.FormatVersion);
                Check("prefs.loadMotionDefault", back.ReduceMotion, false);
                // Separate from the quota display choice file, which stays absent.
                Check("prefs.settingsUntouched",
                    System.IO.File.Exists(FloatingSettingsStore.FilePath), false);

                // Corrupt / wrong version / wrong type / unknown field -> null.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{not json", Encoding.UTF8);
                Check("prefs.corruptNull", FloatingPreferencesStore.Load(), null);
                // Version 2 is now the CURRENT version; use 9 for a true
                // wrong-version rejection.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":9,\"PositionLocked\":true}", Encoding.UTF8);
                Check("prefs.wrongVersionNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":\"yes\"}", Encoding.UTF8);
                Check("prefs.wrongTypeNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true,\"Extra\":1}", Encoding.UTF8);
                Check("prefs.unknownFieldNull", FloatingPreferencesStore.Load(), null);

                // Oversized file (> 4096 bytes) is corrupt by definition, even
                // if the JSON itself would be valid; within the limit it loads.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true}"
                    + new string(' ', 4200), Encoding.UTF8);
                Check("prefs.oversizeNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true}"
                    + new string(' ', 100), Encoding.UTF8);
                FloatingPreferences within = FloatingPreferencesStore.Load();
                Check("prefs.withinLimitLoads",
                    within != null && within.PositionLocked, true);

                // Restore a valid file first, then prove a failed save keeps
                // it intact (same order as the floating-settings store cases).
                FloatingPreferences good = new FloatingPreferences();
                good.Version = FloatingPreferencesStore.FormatVersion;
                good.PositionLocked = true;
                Check("prefs.resave", FloatingPreferencesStore.Save(good), true);
                using (System.IO.FileStream held = new System.IO.FileStream(
                    FloatingPreferencesStore.FilePath, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    FloatingPreferences other = new FloatingPreferences();
                    other.Version = FloatingPreferencesStore.FormatVersion;
                    other.PositionLocked = false;
                    Check("prefs.replaceFailure", FloatingPreferencesStore.Save(other), false);
                }
                Check("prefs.replaceRetains",
                    FloatingPreferencesStore.Load().PositionLocked, true);

                FloatingPreferencesStore.Clear();
                Check("prefs.clear", FloatingPreferencesStore.Load(), null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                try { System.IO.Directory.Delete(dir, true); } catch (Exception) { }
            }

            // ---- restart memory via injected load; no write on start ----
            FloatingPreferences lockedPrefs = new FloatingPreferences();
            lockedPrefs.Version = FloatingPreferencesStore.FormatVersion;
            lockedPrefs.PositionLocked = true;
            int saves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return lockedPrefs; },
                delegate(FloatingPreferences q) { saves++; return true; }))
            {
                Check("prefs.restartLocked", f.PositionLocked, true);
                Check("prefs.restartCircleLocked", f.CircleForTest.PositionLocked, true);
                Check("prefs.restartMenuChecked", f.DefaultMenuLockCheckedForTest, true);
                Check("prefs.noWriteOnStart", saves, 0);
            }

            // Default (no file / null load) -> unlocked.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                Check("prefs.defaultUnlocked", f.PositionLocked, false);
                Check("prefs.defaultMenuUnchecked", f.DefaultMenuLockCheckedForTest, false);
            }

            // ---- locked gestures: click works, threshold drag moves nothing
            // and never mis-opens details; unlock restores dragging ----
            System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                f.ShowCircleAtForTest(area, 1.0);
                FloatingCircleControl circle = f.CircleForTest;
                int opened = 0;
                circle.DetailsRequested += delegate { opened++; };
                System.Drawing.Point start = circle.Bounds.Location;
                System.Drawing.Point p0 = new System.Drawing.Point(start.X + 20, start.Y + 20);

                f.TogglePositionLocked(); // save succeeds (no-op) -> locked
                Check("prefs.lockToggled", f.PositionLocked, true);
                Check("prefs.lockHint", f.LockHintForTest, "已锁定位置");

                // Locked: threshold-crossing gesture does NOT move the circle
                // and does NOT open details on release.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("prefs.lockedNoMove", circle.Bounds.Location, start);
                Check("prefs.lockedThresholdSeen", circle.DraggingForTest, true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("prefs.lockedNoDetails", opened, 0);

                // Locked: a plain click still opens details.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseUpForTest(p0);
                Check("prefs.lockedClickOpens", opened, 1);

                // Unlock: dragging works again (drag still never opens details).
                f.TogglePositionLocked();
                Check("prefs.unlockToggled", f.PositionLocked, false);
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("prefs.unlockedMoves", circle.Bounds.Location != start, true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("prefs.unlockedDragNoDetails", opened, 1);

                // Mid-gesture toggle releases capture and resets the gesture:
                // the stale drag can neither move the circle nor open details.
                f.SetPositionLocked(true);
                System.Drawing.Point beforeMid = circle.Bounds.Location;
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("prefs.midToggleThresholdSeen", circle.DraggingForTest, true);
                f.SetPositionLocked(false); // flip mid-gesture
                Check("prefs.midToggleGestureReset", circle.DraggingForTest, false);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 80, p0.Y + 60));
                Check("prefs.midToggleNoMove", circle.Bounds.Location != beforeMid, false);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 80, p0.Y + 60));
                Check("prefs.midToggleNoDetails", opened, 1);
            }

            // ---- save failure keeps the old state, menu check and hint ----
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return false; }))
            {
                f.TogglePositionLocked();
                Check("prefs.failKeepsUnlocked", f.PositionLocked, false);
                Check("prefs.failMenuUnchecked", f.DefaultMenuLockCheckedForTest, false);
                Check("prefs.failHint", f.LockHintForTest, "锁定状态未保存");
                f.SetPositionLocked(true); // same value as reality: no save attempt
                Check("prefs.failNoStateFlip", f.PositionLocked, false);
            }

            // A throwing save is treated as a failure, not a crash.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { throw new InvalidOperationException("boom"); }))
            {
                f.TogglePositionLocked();
                Check("prefs.throwKeepsUnlocked", f.PositionLocked, false);
                Check("prefs.throwHint", f.LockHintForTest, "锁定状态未保存");
            }

            // ---- shared tray / circle menu: one state, one handler, zero query ----
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                Check("prefs.trayMenuDefaultUnlocked", app.MenuLockCheckedForTest, false);
                app.PerformMenuLockForTest(); // 锁定位置
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.trayMenuLocked", app.FloatingLockedForTest, true);
                Check("prefs.trayMenuChecked", app.MenuLockCheckedForTest, true);
                Check("prefs.circleMenuChecked", app.CircleMenuLockCheckedForTest, true);
                app.PerformMenuLockForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.trayMenuUnlocked", app.FloatingLockedForTest, false);
                Check("prefs.trayMenuUnchecked", app.MenuLockCheckedForTest, false);
                Check("prefs.circleMenuUnchecked", app.CircleMenuLockCheckedForTest, false);
                Check("prefs.zeroQuery", queries, 0);
                // Unlock keeps the normal circle click path working, zero query.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.unlockClickDetails", app.DetailsVisibleForTest, true);
                Check("prefs.stillZeroQuery", queries, 0);
            }

            // ---- hidden-circle failed save: tray notify intent (offline has
            // no real NotifyIcon); visible circle keeps the tooltip path ----
            int fqueries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { fqueries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); },
                delegate(FloatingPreferences p) { return false; }))
            {
                app.HideDetailsForTest();
                Check("prefs.hiddenDefault", app.FloatingVisibleForTest, false);
                app.PerformMenuLockForTest(); // 锁定位置 -> injected save fails
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.hiddenFailKeptUnlocked", app.FloatingLockedForTest, false);
                Check("prefs.hiddenFailMenuUnchecked", app.MenuLockCheckedForTest, false);
                Check("prefs.hiddenFailHint", app.LockHintForTest, "锁定状态未保存");
                Check("prefs.hiddenFailNotifyIntent", app.LockFailNotifyCountForTest, 1);
                Check("prefs.hiddenFailText", app.LastLockFailTextForTest, "位置锁定状态未保存");
                Check("prefs.hiddenFailZeroQuery", fqueries, 0);

                // Circle visible: the tooltip path already covers it -> no
                // tray notification intent, no query, state still kept old.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.visibleNow", app.FloatingVisibleForTest, true);
                app.PerformMenuLockForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.visibleFailHint", app.LockHintForTest, "锁定状态未保存");
                Check("prefs.visibleNoNotifyIntent", app.LockFailNotifyCountForTest, 1);
                Check("prefs.visibleFailKeptUnlocked", app.FloatingLockedForTest, false);
                Check("prefs.visibleZeroQuery", fqueries, 0);
            }
        }

        // ---- v0.9 UX017: reduce motion + floating-preferences.json format 2
        // (Version + PositionLocked + ReduceMotion; format 1 read-only migration) ----

        private static void FloatingPreferencesUX017Cases()
        {
            // ---- store: format 2 round-trip; format 1 migrated in memory;
            // loading never writes; non-schema rejected ----
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_prefs17_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);

                // Format 2 round-trip keeps both flags.
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = true;
                p.ReduceMotion = true;
                Check("prefs2.save", FloatingPreferencesStore.Save(p), true);
                FloatingPreferences back = FloatingPreferencesStore.Load();
                Check("prefs2.version", back.Version, 2);
                Check("prefs2.locked", back.PositionLocked, true);
                Check("prefs2.reduceMotion", back.ReduceMotion, true);
                string raw = System.IO.File.ReadAllText(FloatingPreferencesStore.FilePath,
                    System.Text.Encoding.UTF8);
                Check("prefs2.rawFormat2",
                    raw.Contains("\"Version\":2") && raw.Contains("\"PositionLocked\"")
                    && raw.Contains("\"ReduceMotion\""), true);

                // A later lock-only change still carries the other flag.
                FloatingPreferences lockOnly = new FloatingPreferences();
                lockOnly.Version = FloatingPreferencesStore.FormatVersion;
                lockOnly.PositionLocked = false;
                lockOnly.ReduceMotion = true;
                Check("prefs2.resave", FloatingPreferencesStore.Save(lockOnly), true);
                FloatingPreferences both = FloatingPreferencesStore.Load();
                Check("prefs2.lockSaved", both.PositionLocked, false);
                Check("prefs2.motionKept", both.ReduceMotion, true);

                // Legacy format 1 (strict two fields) migrates IN MEMORY to
                // format 2 with ReduceMotion = false; the file is untouched.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true}", Encoding.UTF8);
                string before = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                FloatingPreferences migrated = FloatingPreferencesStore.Load();
                Check("prefs2.migratedLocked", migrated.PositionLocked, true);
                Check("prefs2.migratedVersion", migrated.Version, 2);
                Check("prefs2.migratedMotionDefault", migrated.ReduceMotion, false);
                string afterRead = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                Check("prefs2.migrateNoWrite", afterRead, before);

                // Non-schema is never accepted: wrong version, wrong types,
                // unknown extra fields, missing fields - in either format.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":3,\"PositionLocked\":true,\"ReduceMotion\":false}", Encoding.UTF8);
                Check("prefs2.wrongVersionNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":2,\"PositionLocked\":1,\"ReduceMotion\":false}", Encoding.UTF8);
                Check("prefs2.wrongTypeNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":2,\"PositionLocked\":true,\"ReduceMotion\":false,\"Extra\":1}",
                    Encoding.UTF8);
                Check("prefs2.extraFieldNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":2,\"PositionLocked\":true}", Encoding.UTF8);
                Check("prefs2.missingFieldNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true,\"ReduceMotion\":false}", Encoding.UTF8);
                Check("prefs1.extraFieldRejected", FloatingPreferencesStore.Load(), null);

                FloatingPreferencesStore.Clear();
                Check("prefs2.clear", FloatingPreferencesStore.Load(), null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                try { System.IO.Directory.Delete(dir, true); } catch (Exception) { }
            }

            // ---- restart memory: both flags restored, no write on start ----
            FloatingPreferences stored = new FloatingPreferences();
            stored.Version = FloatingPreferencesStore.FormatVersion;
            stored.PositionLocked = true;
            stored.ReduceMotion = true;
            int saves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return stored; },
                delegate(FloatingPreferences q) { saves++; return true; }))
            {
                Check("prefs2.restartLocked", f.PositionLocked, true);
                Check("prefs2.restartMotion", f.ReduceMotion, true);
                Check("prefs2.restartCircleMotion", f.CircleForTest.ReduceMotion, true);
                Check("prefs2.restartMenuLockChecked", f.DefaultMenuLockCheckedForTest, true);
                Check("prefs2.restartMenuMotionChecked", f.DefaultMenuMotionCheckedForTest, true);
                Check("prefs2.noWriteOnStart", saves, 0);
            }

            // Default (no file / null load): both off, both checks unchecked.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { return true; }))
            {
                Check("prefs2.defaultMotionOff", f.ReduceMotion, false);
                Check("prefs2.defaultMenuMotionUnchecked",
                    f.DefaultMenuMotionCheckedForTest, false);
            }

            // ---- alternating toggles never overwrite the other flag; same-
            // value calls never write ----
            List<FloatingPreferences> written = new List<FloatingPreferences>();
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { written.Add(q); return true; }))
            {
                f.TogglePositionLocked(); // lock on, motion stays false
                Check("prefs2.lockSaveCarriesMotionFalse",
                    written[0].PositionLocked && !written[0].ReduceMotion, true);
                f.ToggleReduceMotion(); // motion on, lock preserved
                Check("prefs2.motionSaveCarriesLock",
                    written[1].PositionLocked && written[1].ReduceMotion, true);
                Check("prefs2.motionOn", f.ReduceMotion, true);
                Check("prefs2.motionOnHint", f.LockHintForTest, "已减少动画");
                int count = written.Count;
                f.SetReduceMotion(true); // same value: no save
                Check("prefs2.sameValueNoWrite", written.Count, count);
                f.TogglePositionLocked(); // lock off, motion still true
                Check("prefs2.lockOffCarriesMotion",
                    !written[written.Count - 1].PositionLocked
                    && written[written.Count - 1].ReduceMotion, true);
                Check("prefs2.lockOffKeptMotion", f.ReduceMotion, true);
                f.SetReduceMotion(false); // restore off
                Check("prefs2.motionOffHint", f.LockHintForTest, "已恢复动画");
            }

            // ---- save failure keeps ALL old preferences and BOTH menu checks;
            // the hint is motion-specific, never the lock wording ----
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return stored; },
                delegate(FloatingPreferences q) { return false; }))
            {
                f.ToggleReduceMotion(); // stored motion=true -> tries false, fails
                Check("prefs2.failKeepsMotion", f.ReduceMotion, true);
                Check("prefs2.failKeepsLock", f.PositionLocked, true);
                Check("prefs2.failMenuMotionChecked", f.DefaultMenuMotionCheckedForTest, true);
                Check("prefs2.failMenuLockChecked", f.DefaultMenuLockCheckedForTest, true);
                Check("prefs2.failHintMotion", f.LockHintForTest, "动画设置未保存");
            }

            // A throwing save is a failure, not a crash.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences q) { throw new InvalidOperationException("x"); }))
            {
                f.ToggleReduceMotion();
                Check("prefs2.throwKeepsMotion", f.ReduceMotion, false);
                Check("prefs2.throwHintMotion", f.LockHintForTest, "动画设置未保存");
            }

            // ---- real wave timer: stop on enable, restart only when visible
            // with known 0 < percent < 100; display untouched; endpoints and
            // unknown never animate; disposed never restarts ----
            System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            FloatingCircleControl waveCircle = null;
            using (FloatingQuotaForm f = new FloatingQuotaFormForData(PercentSnapshot(50)))
            {
                f.ShowCircleAtForTest(area, 1.0);
                waveCircle = f.CircleForTest;
                Check("prefs2.waveRuns", waveCircle.WaveRunningForTest, true);

                f.SetReduceMotion(true); // production handler: stop + static
                Check("prefs2.waveStopped", waveCircle.WaveRunningForTest, false);
                Check("prefs2.displayKept", waveCircle.PercentTextForTest, "50%");
                f.SetReduceMotion(false); // visible + interior -> restarts
                Check("prefs2.waveResumed", waveCircle.WaveRunningForTest, true);

                // Repeated toggles: same-value no-op, no restart errors.
                f.SetReduceMotion(true);
                f.SetReduceMotion(true);
                Check("prefs2.repeatedStopStable", waveCircle.WaveRunningForTest, false);
                f.SetReduceMotion(false);
                Check("prefs2.repeatedResumeStable", waveCircle.WaveRunningForTest, true);

                // Hidden circle: no wave; toggling while hidden never starts it.
                f.HideCircleForTest();
                Check("prefs2.hiddenNoWave", waveCircle.WaveRunningForTest, false);
                f.SetReduceMotion(true);
                Check("prefs2.hiddenToggleStillNoWave", waveCircle.WaveRunningForTest, false);
                f.SetReduceMotion(false);
                f.ShowCircleAtForTest(area, 1.0); // show recovers the animation
                Check("prefs2.recoveredAfterShow", waveCircle.WaveRunningForTest, true);
            }
            Check("prefs2.disposedWaveStaysStopped", waveCircle.WaveRunningForTest, false);

            // Endpoints (0 / 100) and unknown never animate, flag or not.
            using (FloatingQuotaForm z = new FloatingQuotaFormForData(PercentSnapshot(100)))
            {
                z.ShowCircleAtForTest(area, 1.0);
                Check("prefs2.zeroNoWave", z.CircleForTest.WaveRunningForTest, false);
                z.SetReduceMotion(false); // off + endpoint: still static
                Check("prefs2.zeroOffStillNoWave", z.CircleForTest.WaveRunningForTest, false);
                z.HideCircleForTest();
            }
            using (FloatingQuotaForm full = new FloatingQuotaFormForData(PercentSnapshot(0)))
            {
                full.ShowCircleAtForTest(area, 1.0);
                Check("prefs2.fullNoWave", full.CircleForTest.WaveRunningForTest, false);
                full.HideCircleForTest();
            }
            using (FloatingQuotaForm unknownForm = new FloatingQuotaFormForData(
                AllUnknownSnapshot()))
            {
                unknownForm.ShowCircleAtForTest(area, 1.0);
                Check("prefs2.unknownNoWave",
                    unknownForm.CircleForTest.WaveRunningForTest, false);
                unknownForm.HideCircleForTest();
            }

            // ---- shared tray menu: top level 3, the 设置 submenu holds the
            // toggles, one handler, zero query, poll interval untouched ----
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                Check("prefs2.trayMenuCount", app.MenuItemCountForTest, 3);
                Check("prefs2.trayContentOnly", app.MenuSettingsCountForTest, 1);
                Check("prefs2.trayToggleShifted", app.MenuTextForTest(1), "隐藏悬浮窗");
                Check("prefs2.trayMotionDefaultUnchecked", app.MenuMotionCheckedForTest, false);
                Check("prefs2.pollBaseUntouched", app.PollIntervalForTest, 300000);
                app.PerformMenuMotionForTest(); // 减少动画
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.trayMotionOn", app.FloatingReduceMotionForTest, true);
                Check("prefs2.trayMotionChecked", app.MenuMotionCheckedForTest, true);
                Check("prefs2.circleMenuMotionChecked", app.CircleMenuMotionCheckedForTest, true);
                Check("prefs2.lockUntouchedByMotion", app.FloatingLockedForTest, false);
                app.PerformMenuLockForTest(); // 锁定位置 still independent
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.bothOn",
                    app.FloatingLockedForTest && app.FloatingReduceMotionForTest, true);
                Check("prefs2.bothChecked",
                    app.MenuLockCheckedForTest && app.MenuMotionCheckedForTest, true);
                app.PerformMenuMotionForTest();
                app.PerformMenuLockForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.bothOff",
                    !app.FloatingLockedForTest && !app.FloatingReduceMotionForTest, true);
                Check("prefs2.repeatedTogglesZeroQuery", queries, 0);
                Check("prefs2.pollStillBase", app.PollIntervalForTest, 300000);
            }

            // Hidden circle + failing save: motion failure keeps both states
            // and both checks, notifies with motion wording (never the lock
            // wording), and the two failure channels stay separate.
            int mqueries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { mqueries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); },
                delegate(FloatingPreferences p2) { return false; }))
            {
                app.HideDetailsForTest();
                app.PerformMenuMotionForTest(); // 减少动画 -> injected save fails
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.hiddenMotionKeptOff", app.FloatingReduceMotionForTest, false);
                Check("prefs2.hiddenLockKeptOff", app.FloatingLockedForTest, false);
                Check("prefs2.hiddenMotionMenuUnchecked", app.MenuMotionCheckedForTest, false);
                Check("prefs2.hiddenMotionHint", app.LockHintForTest, "动画设置未保存");
                Check("prefs2.hiddenMotionNotifyIntent", app.MotionFailNotifyCountForTest, 1);
                Check("prefs2.hiddenMotionNotifyText", app.LastMotionFailTextForTest,
                    "减少动画设置未保存");
                Check("prefs2.hiddenMotionZeroQuery", mqueries, 0);
                Check("prefs2.hiddenLockChannelUntouched", app.LockFailNotifyCountForTest, 0);

                // A failing lock toggle keeps its own wording / counter; the
                // motion channel is never reused for a lock failure.
                app.PerformMenuLockForTest(); // 锁定位置 -> also fails
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.hiddenLockNotifyText", app.LastLockFailTextForTest,
                    "位置锁定状态未保存");
                Check("prefs2.hiddenLockNotifyIntent", app.LockFailNotifyCountForTest, 1);
                Check("prefs2.motionCounterUnchanged", app.MotionFailNotifyCountForTest, 1);
                Check("prefs2.stillZeroQuery", mqueries, 0);

                // Circle visible: the tooltip path covers the failure - no
                // extra tray intent, zero query, poll cadence intact.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.visibleNow", app.FloatingVisibleForTest, true);
                app.PerformMenuMotionForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.visibleMotionHint", app.LockHintForTest, "动画设置未保存");
                Check("prefs2.visibleNoMotionNotifyIntent",
                    app.MotionFailNotifyCountForTest, 1);
                Check("prefs2.visibleMotionZeroQuery", mqueries, 0);
                Check("prefs2.visiblePoll10s", app.PollIntervalForTest, 10000);
            }
        }

        private sealed class FloatingQuotaFormForData : FloatingQuotaForm
        {
            public FloatingQuotaFormForData(QuotaSnapshot snap) : this(snap, null) { }
            public FloatingQuotaFormForData(QuotaSnapshot snap, FloatingSettings stored)
                // v0.8 UX016: no-op preference callbacks keep these tests
                // hermetic regardless of the real machine state.
                : base(delegate { return stored; }, delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    delegate(FloatingPreferences p) { return true; })
            {
                IntPtr h = Handle; GC.KeepAlive(h);
                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                ApplyModelView(model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
            }
        }

        private static PanelView EmptyView()
        {
            PanelModel m = new PanelModel();
            return m.ShowEmptyNoData();
        }

        private static QuotaSnapshot AllUnknownSnapshot()
        {
            return QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("weekly", null, null, null, null))));
        }

        // percent is the USED percentage; remaining = clamp(100 - percent).
        private static QuotaSnapshot PercentSnapshot(double usedPercent)
        {
            return QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true,
                    Period("monthly", usedPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        null, null, null))));
        }

        private static void EffectiveQuotaCases()
        {
            ProductQuota p = new ProductQuota();
            PeriodQuota five = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota week = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota month = EffectivePeriod("monthly", 8000, 16000);
            p.Periods.Add(five); p.Periods.Add(week); p.Periods.Add(month);
            EffectivePeriodQuota f = QuotaDisplay.Effective(p, five);
            EffectivePeriodQuota w = QuotaDisplay.Effective(p, week);
            EffectivePeriodQuota m = QuotaDisplay.Effective(p, month);
            CheckClose("effective.min.5h", f.RemainingAmount, 2000);
            CheckClose("effective.min.week", w.RemainingAmount, 2000);
            CheckClose("effective.min.month", m.RemainingAmount, 8000);
            CheckClose("effective.percent.5h", f.RemainingPercent, 20);
            CheckClose("effective.percent.week", w.RemainingPercent, 50);
            CheckClose("effective.percent.month", m.RemainingPercent, 50);
            CheckClose("effective.raw.unchanged", five.RemainingAmount, 5000);

            month.RemainingAmount = 1000;
            CheckClose("effective.lower.min", QuotaDisplay.Effective(p, five).RemainingAmount, 1000);
            CheckClose("effective.lower.percent", QuotaDisplay.Effective(p, five).RemainingPercent, 10);
            CheckClose("effective.lower.doesNotAffectWeekly",
                QuotaDisplay.Effective(p, week).RemainingAmount, 1000);
            CheckClose("effective.lower.doesNotAffectMonthly",
                QuotaDisplay.Effective(p, month).RemainingAmount, 1000);

            PeriodQuota error = EffectivePeriod("error", 1, 100);
            error.Error = "failed";
            p.Periods.Add(error);
            EffectivePeriodQuota failed = QuotaDisplay.Effective(p, error);
            Check("effective.error.amountUnknown", failed.AmountKnown, false);
            Check("effective.error.percentUnknown", failed.PercentKnown, false);
            CheckClose("effective.error.ignoredMinimum",
                QuotaDisplay.Effective(p, five).RemainingAmount, 1000);

            ProductQuota shuffled = new ProductQuota();
            PeriodQuota shuffledMonth = EffectivePeriod("monthly", 8000, 16000);
            PeriodQuota shuffledFive = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota shuffledWeek = EffectivePeriod("weekly", 2000, 4000);
            shuffled.Periods.Add(shuffledMonth); shuffled.Periods.Add(shuffledFive);
            shuffled.Periods.Add(shuffledWeek);
            CheckClose("effective.shuffled.5h", QuotaDisplay.Effective(
                shuffled, shuffledFive).RemainingAmount, 2000);
            CheckClose("effective.shuffled.week", QuotaDisplay.Effective(
                shuffled, shuffledWeek).RemainingAmount, 2000);
            CheckClose("effective.shuffled.month", QuotaDisplay.Effective(
                shuffled, shuffledMonth).RemainingAmount, 8000);

            ProductQuota hierarchy = new ProductQuota();
            PeriodQuota hFive = EffectivePeriod("5h", 100, 100);
            PeriodQuota hWeek = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota hMonth = EffectivePeriod("monthly", 8000, 16000);
            hierarchy.Periods.Add(hFive); hierarchy.Periods.Add(hWeek); hierarchy.Periods.Add(hMonth);
            CheckClose("effective.ownBelowUpper.5h", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 100);
            CheckClose("effective.ownBelowUpper.week", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            hFive.RemainingAmount = 5000;
            hWeek.RemainingAmount = 8000;
            hMonth.RemainingAmount = 2000;
            CheckClose("effective.upperLimits.5h", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 2000);
            CheckClose("effective.upperLimits.week", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            CheckClose("effective.upperLimits.month", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hFive.RemainingAmount = 0;
            CheckClose("effective.zero.lowerDoesNotAffectWeekly", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            CheckClose("effective.zero.lowerDoesNotAffectMonthly", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hFive.RemainingAmount = 5000;
            hWeek.RemainingAmount = 0;
            CheckClose("effective.zero.weekLimitsFive", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 0);
            CheckClose("effective.zero.weekDoesNotAffectMonthly", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hWeek.RemainingAmount = 8000;
            hMonth.RemainingAmount = 0;
            CheckClose("effective.zero.monthLimitsFive", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 0);
            CheckClose("effective.zero.monthLimitsWeekly", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 0);

            ProductQuota unknownUpper = new ProductQuota();
            PeriodQuota uFive = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota uWeek = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota uMonth = EffectivePeriod("monthly", 8000, 16000);
            uWeek.Error = "failed";
            unknownUpper.Periods.Add(uFive); unknownUpper.Periods.Add(uWeek); unknownUpper.Periods.Add(uMonth);
            CheckClose("effective.error.upperIgnored", QuotaDisplay.Effective(
                unknownUpper, uFive).RemainingAmount, 5000);
            uWeek.Error = null; uWeek.RemainingAmount = double.NaN;
            CheckClose("effective.invalid.upperIgnored", QuotaDisplay.Effective(
                unknownUpper, uFive).RemainingAmount, 5000);
            PeriodQuota unknownLabel = EffectivePeriod("other", 0, 100);
            unknownUpper.Periods.Add(unknownLabel);
            CheckClose("effective.unknownLabel.selfOnly", QuotaDisplay.Effective(
                unknownUpper, unknownLabel).RemainingAmount, 0);
            unknownLabel.AmountKnown = false;
            Check("effective.unknownLabel.ownUnknown", QuotaDisplay.Effective(
                unknownUpper, unknownLabel).AmountKnown, false);

            QuotaSnapshot cacheSnap = new QuotaSnapshot();
            ProductQuota cacheProduct = new ProductQuota();
            cacheProduct.Product = "agent-plan"; cacheProduct.SubscribedKnown = true;
            cacheProduct.Subscribed = true;
            cacheProduct.Periods.Add(five); cacheProduct.Periods.Add(week);
            cacheProduct.Periods.Add(month);
            cacheSnap.Products.Add(cacheProduct);
            string fingerprint = QueryScope.FromAuth(PopupForm.SampleIdentity()).Fingerprint;
            CachedSnapshot cache = PersistentStateStore.Decode(PersistentStateStore.Encode(
                PersistentStateStore.ToCache(cacheSnap, fingerprint)));
            QuotaSnapshot restored = PersistentStateStore.FromCache(cache);
            CheckClose("effective.cache.rawFive", restored.Products[0].Periods[0].RemainingAmount, 5000);
            CheckClose("effective.cache.rawMonth", restored.Products[0].Periods[2].RemainingAmount, 1000);
            CheckClose("effective.cache.display", QuotaDisplay.Effective(
                restored.Products[0], restored.Products[0].Periods[0]).RemainingAmount, 1000);

            foreach (double bad in new double[] { double.NaN, double.PositiveInfinity,
                double.NegativeInfinity, -1 })
            {
                PeriodQuota invalid = EffectivePeriod("bad", bad, 100);
                p.Periods.Add(invalid);
                Check("effective.invalid.amountUnknown." + bad,
                    QuotaDisplay.Effective(p, invalid).AmountKnown, false);
                CheckClose("effective.invalid.minimum." + bad,
                    QuotaDisplay.Effective(p, five).RemainingAmount, 1000);
            }

            QuotaSnapshot snap = new QuotaSnapshot();
            p.Product = "agent-plan"; p.DisplayName = "Agent Plan";
            p.SubscribedKnown = true; p.Subscribed = true;
            snap.Products.Add(p);
            PanelView view = new PanelView(); view.Data = snap;
            Check("effective.summary.minimum", QuotaSummary.Build(view).Contains(
                "5 小时：剩余 10%，可用剩余 1000 额度"), true);
            CheckClose("effective.circle.minimum", FloatingSelection.Build(snap)[0].Percent, 10);

            PeriodQuota zero = EffectivePeriod("zero", 0, 100);
            p.Periods.Add(zero);
            CheckClose("effective.zero", QuotaDisplay.Effective(p, zero).RemainingAmount, 0);
            CheckClose("effective.zero.unknownDoesNotAffectFive", QuotaDisplay.Effective(
                p, five).RemainingAmount, 1000);
            CheckClose("effective.zero.unknownDoesNotAffectWeekly", QuotaDisplay.Effective(
                p, week).RemainingPercent, 25);

            PeriodQuota unknown = new PeriodQuota();
            unknown.Label = "unknown";
            p.Periods.Add(unknown);
            EffectivePeriodQuota u = QuotaDisplay.Effective(p, unknown);
            Check("effective.unknown.amount", u.AmountKnown, false);
            Check("effective.unknown.percent", u.PercentKnown, false);

            ProductQuota other = new ProductQuota();
            PeriodQuota otherPeriod = EffectivePeriod("monthly", 9000, 10000);
            other.Periods.Add(otherPeriod);
            CheckClose("effective.product.isolated",
                QuotaDisplay.Effective(other, otherPeriod).RemainingAmount, 9000);

            PeriodQuota invalidTotal = EffectivePeriod("raw", 50, 100);
            invalidTotal.Total = 0; invalidTotal.TotalKnown = true;
            p.Periods.Add(invalidTotal);
            CheckClose("effective.invalidTotal.rawPercent",
                QuotaDisplay.Effective(p, invalidTotal).RemainingPercent, 50);

        }

        private static PeriodQuota EffectivePeriod(string label, double amount, double total)
        {
            PeriodQuota p = new PeriodQuota();
            p.Label = label;
            p.AmountKnown = true; p.RemainingAmount = amount;
            p.TotalKnown = true; p.Total = total;
            p.PercentKnown = true; p.RemainingPercent = amount / total * 100.0;
            return p;
        }

        private static void PercentWins()
        {
            string json = Wrap(Item("agent-plan", "personal", "medium", true,
                Period("5h", "25", null, null, "2026-10-03T14:43:59+08:00")));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("percent25.known", p.PercentKnown, true);
            CheckClose("percent25.remaining=75", p.RemainingPercent, 75.0);
            Check("percent25.amountUnknown", p.AmountKnown, false);
            Check("percent25.statusOk", s.Status, QuotaStatus.Ok);
        }

        private static void ZeroIsValid()
        {
            string json = Wrap(Item("agent-plan", null, null, true,
                Period("monthly", "0", "0", "1000", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("zero.usedKnown", p.UsedKnown, true);
            CheckClose("zero.used=0", p.Used, 0.0);
            Check("zero.percentKnown", p.PercentKnown, true);
            CheckClose("zero.remaining=100", p.RemainingPercent, 100.0);
            Check("zero.amountKnown", p.AmountKnown, true);
            CheckClose("zero.amount=1000", p.RemainingAmount, 1000.0);
        }

        private static void MissingIsUnknown()
        {
            string json = Wrap(Item("coding-plan", "personal", null, true,
                Period("monthly", null, null, null, null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("missing.percentUnknown", p.PercentKnown, false);
            Check("missing.usedUnknown", p.UsedKnown, false);
            Check("missing.totalUnknown", p.TotalKnown, false);
            Check("missing.amountUnknown", p.AmountKnown, false);
        }

        private static void UsedTotalFallback()
        {
            string json = Wrap(Item("agent-plan", null, null, true,
                Period("weekly", null, "250", "1000", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("fallback.known", p.PercentKnown, true);
            CheckClose("fallback.remaining=75", p.RemainingPercent, 75.0);
            CheckClose("fallback.amount=750", p.RemainingAmount, 750.0);
        }

        private static void TotalZero()
        {
            string json = Wrap(Item("coding-plan", null, null, true,
                Period("monthly", null, "50", "0", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("total0.percentUnknown", p.PercentKnown, false);
            Check("total0.amountUnknown", p.AmountKnown, false);

            // total 0 but a valid percent: trust percent.
            string json2 = Wrap(Item("coding-plan", null, null, true,
                Period("monthly", "30", null, "0", null)));
            QuotaSnapshot s2 = Parse(json2);
            PeriodQuota p2 = s2.Products[0].Periods[0];
            Check("total0percent.known", p2.PercentKnown, true);
            CheckClose("total0percent.remaining=70", p2.RemainingPercent, 70.0);
        }

        private static void OutOfBoundsClamp()
        {
            string high = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "130", null, null, null)));
            PeriodQuota ph = Parse(high).Products[0].Periods[0];
            Check("clamp.high=0", ph.PercentKnown, true);
            CheckClose("clamp.high.remaining=0", ph.RemainingPercent, 0.0);
            Check("clamp.high.flag", ph.Clamped, true);

            string low = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "-3", null, null, null)));
            PeriodQuota pl = Parse(low).Products[0].Periods[0];
            CheckClose("clamp.low.remaining=100", pl.RemainingPercent, 100.0);
            Check("clamp.low.flag", pl.Clamped, true);
        }

        private static void MalformedAndShape()
        {
            Check("malformed.notjson", QuotaParser.Parse("not-json{").Status, QuotaStatus.FormatError);
            Check("malformed.rootArray", QuotaParser.Parse("[1,2,3]").Status, QuotaStatus.FormatError);
            Check("malformed.noItems", QuotaParser.Parse("{\"viewer\":{}}").Status, QuotaStatus.FormatError);
            Check("malformed.itemsType", QuotaParser.Parse("{\"items\":{\"a\":1}}").Status, QuotaStatus.FormatError);
            Check("malformed.empty", QuotaParser.Parse("").Status, QuotaStatus.FormatError);
        }

        private static void PartialErrors()
        {
            string errItem = "{\"product\":\"coding-plan\",\"subscribed\":true,\"error\":\"boom\"}";
            string okItem = Item("agent-plan", null, null, true, Period("5h", "10", null, null, null));
            string json = Wrap(okItem, errItem);
            QuotaSnapshot s = Parse(json);
            Check("partial.status", s.Status, QuotaStatus.PartialError);
            Check("partial.count=2", s.Products.Count, 2);
            bool foundErr = false;
            for (int i = 0; i < s.Products.Count; i++)
                if (s.Products[i].Error != null) foundErr = true;
            Check("partial.errorPresent", foundErr, true);
        }

        private static void UnsubscribedAndEmpty()
        {
            string json = Wrap(Item("agent-plan", null, null, false,
                Period("5h", "10", null, null, null)));
            QuotaSnapshot s = Parse(json);
            Check("unsub.statusNoSub", s.Status, QuotaStatus.NoSubscription);
            Check("unsub.hidden", s.Products.Count, 0);

            QuotaSnapshot e = QuotaParser.Parse("{\"items\":[]}");
            Check("empty.statusNoSub", e.Status, QuotaStatus.NoSubscription);
            Check("empty.count", e.Products.Count, 0);
        }

        private static void ErrorBucketNotUnsubscribed()
        {
            // An item-level error with subscribed=false must still be shown as an error,
            // never silently hidden as "unsubscribed".
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":false,\"error\":\"nope\"}]}";
            QuotaSnapshot s = Parse(json);
            Check("errBucket.count=1", s.Products.Count, 1);
            Check("errBucket.status", s.Status, QuotaStatus.PartialError);
            Check("errBucket.hasError", s.Products[0].Error != null, true);
        }

        private static void MissingSubscribedIsMalformed()
        {
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"periods\":[]}]}";
            QuotaSnapshot s = Parse(json);
            Check("missingSub.count=1", s.Products.Count, 1);
            Check("missingSub.malformed", s.Products[0].Malformed, true);
            Check("missingSub.notNoSub", s.Status != QuotaStatus.NoSubscription, true);
        }

        private static void NullItemAndNullPeriod()
        {
            // A null item must be an error, not silently skipped (which would
            // look like "no subscription").
            QuotaSnapshot s = QuotaParser.Parse("{\"items\":[null]}");
            Check("nullItem.count=1", s.Products.Count, 1);
            Check("nullItem.malformed", s.Products[0].Malformed, true);
            Check("nullItem.notNoSub", s.Status != QuotaStatus.NoSubscription, true);
            Check("nullItem.partial", s.Status, QuotaStatus.PartialError);

            // A null period bucket must be surfaced as a period error too.
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true,\"periods\":[null]}]}";
            QuotaSnapshot s2 = Parse(json);
            Check("nullPeriod.count=1", s2.Products[0].Periods.Count, 1);
            Check("nullPeriod.error", s2.Products[0].Periods[0].Error != null, true);
            Check("nullPeriod.flag", s2.Products[0].PeriodErrorPresent, true);
            Check("nullPeriod.partial", s2.Status, QuotaStatus.PartialError);
        }

        private static void PeriodErrorPartial()
        {
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true,\"periods\":[" +
                          "{\"label\":\"5h\",\"percent\":10}," +
                          "{\"label\":\"weekly\",\"error\":\"upstream\"}]}]}";
            QuotaSnapshot s = Parse(json);
            Check("periodErr.status", s.Status, QuotaStatus.PartialError);
            Check("periodErr.flag", s.Products[0].PeriodErrorPresent, true);
            Check("periodErr.good", s.Products[0].Periods[0].PercentKnown, true);
            Check("periodErr.bad", s.Products[0].Periods[1].Error != null, true);
        }

        private static void ItemUpdatedAt()
        {
            string json = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                          "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                          "\"updated_at\":\"2026-10-03T10:00:00+08:00\"}]}";
            QuotaSnapshot s = Parse(json);
            Check("updatedAt.has", s.Products[0].HasUpdated, true);

            // Numeric epoch ms must parse; absurd values must not crash.
            string json2 = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                           "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                           "\"updated_at\":1790998371000}]}";
            QuotaSnapshot s2 = Parse(json2);
            Check("updatedAt.epoch", s2.Products[0].HasUpdated, true);

            string json3 = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                           "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                           "\"updated_at\":99999999999999999999}]}";
            QuotaSnapshot s3 = Parse(json3);
            Check("updatedAt.hugeNoCrash", s3.Status, QuotaStatus.Ok);
            Check("updatedAt.hugeUnknown", s3.Products[0].HasUpdated, false);
        }

        private static void ErrorTextNotLeaked()
        {
            // Raw upstream error text must never be surfaced to the user.
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true," +
                          "\"error\":\"token=secret123 bearer abcdef\"}]}";
            QuotaSnapshot s = Parse(json);
            string err = s.Products[0].Error;
            Check("errText.notNull", err != null, true);
            Check("errText.noSecret", err.IndexOf("secret123", StringComparison.Ordinal) < 0, true);
            Check("errText.noBearer", err.IndexOf("bearer", StringComparison.OrdinalIgnoreCase) < 0, true);
        }

        private static void CliAuthGate()
        {
            // Success requires auth logged_in=true. Test transport flows through
            // the SAME gate as the real path.
            CliMode ok = new CliMode();
            ok.AuthJson = "{\"logged_in\":true}";
            ok.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.ok", new QuotaCli(ok), QuotaStatus.Ok);

            // logged_in=false -> NotLoggedIn
            CliMode no = new CliMode();
            no.AuthJson = "{\"logged_in\":false}";
            no.UsageJson = "{\"items\":[]}";
            CliResultTransport("cli.notLogged", new QuotaCli(no), QuotaStatus.NotLoggedIn);

            // missing logged_in -> FormatError (not usage)
            CliMode missing = new CliMode();
            missing.AuthJson = "{\"auth_method\":\"sso\"}";
            missing.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.missingBool", new QuotaCli(missing), QuotaStatus.FormatError);

            // auth non-zero exit -> Failed (must NOT proceed to usage)
            CliMode authFail = new CliMode();
            authFail.AuthExitCode = 3;
            authFail.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.authFail", new QuotaCli(authFail), QuotaStatus.Failed);
        }

        private static void CliUsageExitCode()
        {
            // usage non-zero exit must not be masked as Ok even with JSON on stdout.
            CliMode usageFail = new CliMode();
            usageFail.AuthJson = "{\"logged_in\":true}";
            usageFail.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            usageFail.UsageExitCode = 1;
            CliResultTransport("cli.usageFail", new QuotaCli(usageFail), QuotaStatus.Failed);

            // auth timeout -> Timeout
            CliMode to = new CliMode();
            to.AuthExitCode = 124;
            CliResultTransport("cli.timeout", new QuotaCli(to), QuotaStatus.Timeout);

            // auth cancelled -> Cancelled
            CliMode cancelled = new CliMode();
            cancelled.AuthJson = "{\"logged_in\":true}";
            cancelled.DelayMs = 5000;
            System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            CliResultTransportToken("cli.cancelled", new QuotaCli(cancelled), QuotaStatus.Cancelled, cts.Token);

            // disposed -> Failed, and no new process may start
            CliMode disposed = new CliMode();
            disposed.AuthJson = "{\"logged_in\":true}";
            disposed.UsageJson = "{\"items\":[]}";
            QuotaCli dc = new QuotaCli(disposed);
            dc.Dispose();
            CliResultTransport("cli.disposed", dc, QuotaStatus.Failed);
            Check("cli.disposedNoStart", TryStartAfterDispose(dc), true);
        }

        private static void CliTimeoutCancelDispose()
        {
            // Test-mode timeout for usage (auth ok).
            CliMode u = new CliMode();
            u.AuthJson = "{\"logged_in\":true}";
            u.UsageExitCode = 124;
            CliResultTransport("cli.usageTimeout", new QuotaCli(u), QuotaStatus.Timeout);
        }

        private static void LayoutMathBounds()
        {
            // Small content stays within min/max.
            Check("layout.small", LayoutMath.DesiredHeight(76, 64, 90, 1000), 76 + 90);
            // Large content is capped at 70% of the work area.
            Check("layout.largeCap", LayoutMath.DesiredHeight(76, 5000, 90, 1000), (int)(1000 * 0.7));
            // Never exceeds the work area, even with absurd minimums.
            Check("layout.neverExceeds", LayoutMath.DesiredHeight(900, 900, 900, 800), 800);
            // Degenerate work area still returns at least 1.
            Check("layout.degenerate", LayoutMath.DesiredHeight(0, 0, 0, 0) >= 1, true);
        }

        private static void DisplayNameMapping()
        {
            Check("name.product", DisplayNames.Product("coding-plan-team"), "Coding Plan 团队版");
            Check("name.period", DisplayNames.Period("weekly"), "每周");
            Check("name.teamFlag", DisplayNames.IsTeam("agent-plan-team"), true);
            Check("name.teamFlagFalse", DisplayNames.IsTeam("agent-plan"), false);
        }

        private static void PercentFormatCases()
        {
            Check("pct.zero", PercentFormat.Remaining(0), "已用尽");
            Check("pct.sub1", PercentFormat.Remaining(0.4), "<1%");
            Check("pct.oneDecimal", PercentFormat.Remaining(75.54), "75.5%");
            Check("pct.integer", PercentFormat.Remaining(75.0), "75%");
            Check("pct.barZero", PercentFormat.RemainingForBar(0), "已用尽");
            Check("pct.barSub1", PercentFormat.RemainingForBar(0.2), "剩余 <1%");
            Check("pct.bar", PercentFormat.RemainingForBar(12.34), "剩余 12.3%");
        }

        private static void RelativeFormatCases()
        {
            DateTime now = new DateTime(2026, 10, 3, 12, 0, 0);
            Check("rel.freshNow", RelativeFormat.Freshness(now.AddSeconds(-10), now), "刚刚查询");
            Check("rel.freshMin", RelativeFormat.Freshness(now.AddMinutes(-5), now), "约 5 分钟前查询");
            // Reset in the past must not imply quota recovered.
            Check("rel.past", RelativeFormat.Countdown(now.AddMinutes(-1), now),
                "已到重置时间，请刷新");
            Check("rel.hours", RelativeFormat.Countdown(now.AddHours(3).AddMinutes(10), now),
                "3 小时 10 分后重置");
            Check("rel.days", RelativeFormat.Countdown(now.AddDays(2).AddHours(1), now),
                "2 天 1 小时后重置");
        }

        private static void RiskSummaryCases()
        {
            // Two products, no summing. agent-plan lowest = 20 (25 -> 75, 80 -> 20);
            // coding-plan lowest = 40. Low flag only when <= 20.
            QuotaSnapshot s = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true,
                    Period("5h", "25", null, null, null) + "," + Period("weekly", "80", null, null, null)),
                Item("coding-plan", null, null, true, Period("monthly", "60", null, null, null))));
            System.Collections.Generic.List<RiskSummary> r = RiskSummaryBuilder.Build(s);
            Check("risk.count", r.Count, 2);
            CheckClose("risk.agentLowest", r[0].LowestPercent, 20.0);
            Check("risk.agentLow", r[0].Low, true);
            CheckClose("risk.codingLowest", r[1].LowestPercent, 40.0);
            Check("risk.codingNotLow", r[1].Low, false);

            // Unknown/error periods are ignored, never counted as 0.
            QuotaSnapshot s2 = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true,
                    Period("5h", null, null, null, null) + ",{\"label\":\"weekly\",\"error\":\"x\"}")));
            System.Collections.Generic.List<RiskSummary> r2 = RiskSummaryBuilder.Build(s2);
            Check("risk.ignore.none", r2[0].HasValue, false);
        }

        private static void ScopeFingerprintCases()
        {
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-1");
            QueryScope s1 = QueryScope.FromAuth(a);
            QueryScope s2 = QueryScope.FromAuth(a);
            Check("scope.known", s1.IsKnown, true);
            Check("scope.sameFingerprint", s1.Matches(s2), true);
            Check("scope.account", s1.Account, "123456789");
            Check("scope.userId", s1.UserId, "alice");
            Check("scope.isRoot", s1.IsRoot, false);

            AuthIdentity root = AuthId("trn:iam::123456789:root", "default", "cn-beijing", "proj-1");
            Check("scope.rootKnown", QueryScope.FromAuth(root).IsKnown, true);
            Check("scope.rootFlag", QueryScope.FromAuth(root).IsRoot, true);

            AuthIdentity b = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-2");
            Check("scope.diffProjectMismatch", s1.Matches(QueryScope.FromAuth(b)), false);

            AuthIdentity missing = AuthId(null, "default", "cn-beijing", "proj-1");
            Check("scope.missingOwnerUnknown", QueryScope.FromAuth(missing).IsKnown, false);

            // Unsupported TRN shapes are unknown, never "same".
            Check("scope.badTrnShape",
                QueryScope.FromAuth(AuthId("owner-A", "d", "r", "p")).IsKnown, false);
            Check("scope.badTrn5parts",
                QueryScope.FromAuth(AuthId("trn:iam::acct:role/x", "d", "r", "p")).IsKnown, false);

            // parts[2] must be the empty region slot: trn:iam::<account>:...
            Check("scope.nonEmptyRegionSlotUnknown",
                QueryScope.FromAuth(AuthId("trn:iam:cn-bj:123:user/alice", "d", "r", "p")).IsKnown,
                false);
            // A user/ principal without an id is not a valid identity.
            Check("scope.emptyUserIdUnknown",
                QueryScope.FromAuth(AuthId("trn:iam::123:user/", "d", "r", "p")).IsKnown, false);

            Check("scope.nullUnknown", QueryScope.FromAuth(null).IsKnown, false);
        }

        private static void ScopeValidationCases()
        {
            QueryScope scope = QueryScope.FromAuth(
                AuthId("trn:iam::123456789:user/alice", "default", "cn-bj", "p1"));

            ViewerIdentity match = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.same", ScopeValidation.Validate(scope, match), ScopeVerdict.Same);

            // Strict equality, not substring: account 12 must NOT match 123456789.
            ViewerIdentity shortAcct = FullViewer("12", "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.acctNotSubstring",
                ScopeValidation.Validate(scope, shortAcct), ScopeVerdict.Mismatch);

            // Region / project conflicts must be Mismatch even when account+profile agree.
            Check("scopeValid.regionMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-sh", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.projectMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-bj", "p2", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.userMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "bob", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.profileMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "other", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);

            // Root auth vs is_root=false must be Mismatch.
            QueryScope rootScope = QueryScope.FromAuth(
                AuthId("trn:iam::123456789:root", "default", "cn-bj", "p1"));
            Check("scopeValid.rootMismatch",
                ScopeValidation.Validate(rootScope,
                    FullViewer("123456789", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.rootSame",
                ScopeValidation.Validate(rootScope,
                    FullViewer("123456789", null, "default", "cn-bj", "p1", true)),
                ScopeVerdict.Same);

            // Sub-user auth vs a viewer that reports root is a principal
            // conflict, even without any user_id.
            ViewerIdentity rootViewer = FullViewer("123456789", null, "default", "cn-bj", "p1", true);
            Check("scopeValid.subUserVsRootMismatch",
                ScopeValidation.Validate(scope, rootViewer), ScopeVerdict.Mismatch);
            // Sub-user auth with is_root explicitly false but a differing user_id
            // must still be Mismatch.
            Check("scopeValid.subUserVsOtherUserMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "bob", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            // Missing is_root stays Unknown (never Same) for a sub-user.
            ViewerIdentity noRootSub = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            noRootSub.IsRootKnown = false;
            Check("scopeValid.subUserMissingRootUnknown",
                ScopeValidation.Validate(scope, noRootSub), ScopeVerdict.Unknown);

            // Empty / whitespace defining fields are MISSING (Unknown), never a
            // conflict. Each of these would previously have been Mismatch.
            Check("scopeValid.whitespaceAccountUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("   ", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.emptyAccountUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceProfileUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "  ", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceRegionUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "\t", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceProjectUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-bj", " ", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceUserUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "  ", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            // Root auth + whitespace account is Unknown, not a bogus conflict.
            Check("scopeValid.rootWhitespaceAccountUnknown",
                ScopeValidation.Validate(rootScope,
                    FullViewer("  ", null, "default", "cn-bj", "p1", true)),
                ScopeVerdict.Unknown);
            // A non-blank but different value must still be a strict Mismatch.
            Check("scopeValid.blankPaddedAccountMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer(" 123456789 ", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);

            // Missing a defining viewer field => Unknown, never Same.
            ViewerIdentity noRegion = FullViewer("123456789", "alice", "default", null, "p1", false);
            Check("scopeValid.missingRegionUnknown",
                ScopeValidation.Validate(scope, noRegion), ScopeVerdict.Unknown);
            ViewerIdentity noRoot = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            noRoot.IsRootKnown = false;
            Check("scopeValid.missingIsRootUnknown",
                ScopeValidation.Validate(scope, noRoot), ScopeVerdict.Unknown);

            // Same profile but missing account must NOT be Same.
            ViewerIdentity noAccount = FullViewer(null, "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.noAccountNotSame",
                ScopeValidation.Validate(scope, noAccount), ScopeVerdict.Unknown);

            ViewerIdentity bare = new ViewerIdentity();
            bare.Present = true;
            Check("scopeValid.bareUnknown", ScopeValidation.Validate(scope, bare), ScopeVerdict.Unknown);

            Check("scopeValid.unknownScope",
                ScopeValidation.Validate(QueryScope.Unknown(), match), ScopeVerdict.Unknown);
        }

        private static ViewerIdentity FullViewer(string account, string userId, string profile,
            string region, string project, bool isRoot)
        {
            ViewerIdentity v = new ViewerIdentity();
            v.Present = true;
            v.AccountId = account;
            v.UserId = userId;
            v.Profile = profile;
            v.Region = region;
            v.ProjectName = project;
            v.IsRoot = isRoot;
            v.IsRootKnown = true;
            return v;
        }

        private static void PanelModelIdentityCases()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-1");

            // First auth: no prior data, then usage confirms and caches.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            PanelView v = m.OnUsageResult(snap, FullViewer("123456789", "alice", "default",
                "cn-beijing", "proj-1", false), ScopeVerdict.Same, null);
            Check("panel.cached", m.Last != null, true);
            Check("panel.showing", v.State, PanelState.ShowingCurrent);

            // UX011: BeginQuery retains the exact existing view while waiting.
            PanelView bq = m.BeginQuery();
            Check("panel.beginQueryKeepsData", bq.Data == snap, true);
            Check("panel.beginQueryState", bq.State, PanelState.ShowingCurrent);

            // Cancellation alone does not prove an identity change.
            PanelView c = m.OnCancelled(null);
            Check("panel.beginCancelKeepsData", c.Data == snap, true);
            Check("panel.beginCancelRetained", m.Last == snap, true);

            // Rebuild a cache for the same-scope continue test.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView v2 = m.OnUsageResult(snap, FullViewer("123456789", "alice", "default",
                "cn-beijing", "proj-1", false), ScopeVerdict.Same, null);
            Check("panel.recached", m.Last != null, true);

            // Same identity keeps history without a process state.
            m.BeginQuery();
            PanelView same = m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            Check("panel.sameRestore", same.Data != null, true);
            Check("panel.sameNoProcessState", same.State, PanelState.ShowingCurrent);

            // Unknown identity cannot prove a new owner: retain history.
            m.BeginQuery();
            PanelView unk = m.OnAuthResult(true, AuthId(null, null, null, null), QuotaStatus.Ok, null);
            Check("panel.unknownKeepsHistory", unk.Data == snap, true);
            Check("panel.unknownRetained", m.Last == snap, true);
        }

        private static void PanelModelStaleCases()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            // Same A: begin -> auth same -> usage FAILURE keeps last data.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView stale = m.OnUsageFailure(QuotaStatus.Failed, "boom", null);
            Check("panel.staleKeepsData", stale.Data != null, true);
            Check("panel.staleState", stale.State, PanelState.StaleError);

            // Re-cache, then auth failure (loggedIn=false) clears everything.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            PanelView err = m.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
            Check("panel.authFailClears", m.Last == null, true);
            Check("panel.authFailCopy", err.AllowCopyLogin, true);
            Check("panel.authFailRetry", err.AllowRetry, true);
            Check("panel.authFailGuide", err.AllowOpenGuide, true);
        }

        private static void PanelModelUnknownVerdictCases()
        {
            // Unknown viewer verdict on a successful usage must NOT be cached.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView v = m.OnUsageResult(snap, null, ScopeVerdict.Unknown, null);
            Check("panel.unknownVerdictShows", v.Data != null, true);
            Check("panel.unknownVerdictNotCached", m.Last == null, true);
            Check("panel.unknownVerdictState", v.State, PanelState.ShowingCurrent);

            // And a later failure cannot resurrect it as stale.
            PanelView f = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panel.unknownThenFailNoStale", f.Data == null, true);
        }

        private static void PanelModelNullIdentityCases()
        {
            // Logged in but active_profile missing: auth-confirmed with null identity.
            // UX011: unknown auth retains history and cannot persist a new result.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            m.BeginQuery();
            PanelView auth = m.OnAuthResult(true, null, QuotaStatus.Ok, null);
            Check("panel.nullIdentityData", auth.Data == snap, true);
            PanelView fail = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panel.nullIdentityFailKeepsStale", fail.Data == snap, true);
        }

        private static void PanelModelReuseTiedToCurrentQuery()
        {
            // Cache a confirmed same-scope snapshot.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            Check("panelReuse.cached", m.Last != null, true);
            Check("panelReuse.confirmedScope", m.ConfirmedScope != null, true);

            // BeginQuery clears pending confirmation, while cancellation retains Last.
            m.BeginQuery();
            PanelView c = m.OnCancelled(null);
            Check("panelReuse.cancelBeforeAuthKeepsData", c.Data == snap, true);
            Check("panelReuse.cancelBeforeAuthRetained", m.Last == snap, true);

            // Rebuild cache, then a same-scope auth restores during refresh.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            m.BeginQuery();
            PanelView same = m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            Check("panelReuse.sameRestores", same.Data != null, true);

            // Now a failure in the same query keeps the restored data.
            PanelView fail = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panelReuse.sameFailureKeepsData", fail.Data != null, true);
            Check("panelReuse.sameFailureStale", fail.State, PanelState.StaleError);
        }

        private static void PanelModelAuthHintClearing()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            // Auth failure clears the stale friendly hint.
            m.BeginQuery();
            PanelView err = m.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
            Check("panelHint.authFailClears", err.IdentityHint, null);
            Check("panelHint.authFailUnknown", err.IdentityUnknown, false);

            // Unknown identity: old hint dropped and "身份未确认" flag is set.
            m.BeginQuery();
            PanelView unk = m.OnAuthResult(true, AuthId(null, null, null, null), QuotaStatus.Ok, null);
            Check("panelHint.unknownClears", unk.IdentityHint, null);
            Check("panelHint.unknownFlag", unk.IdentityUnknown, true);
            Check("panelHint.unknownNoData", unk.Data == null, true);
        }

        private static void PanelStateAfterMismatch()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            PanelView v = m.OnUsageResult(null, null, ScopeVerdict.Mismatch, null);
            Check("panel.mismatchState", v.State, PanelState.IdentityChanged);
            Check("panel.mismatchClears", m.Last == null, true);
            Check("panel.mismatchRetry", v.AllowRetry, true);
        }

        private static void HideControllerCases()
        {
            // A pure trace: Toggle / ConsumeToggle / Evt / Tick with explicit times.

            // Focus loss queues a pending hide (no immediate decision).
            HideController h = new HideController();
            h.State(true, 1000);
            h.Evt(1000, true);
            Check("hide.evtPending", h.Pending, true);
            Check("hide.beforeDeadline", h.Tick(1100, true), false); // < delay
            Check("hide.atDeadline", h.Tick(1220, true), true);      // now+delay

            // Deactivate-before-mousedown: the focus-loss queues a hide, then the
            // press cancels it and captures the actual (visible) state. The click
            // consumes the capture once.
            HideController d = new HideController();
            d.State(true, 4000);
            d.Evt(4000, true);
            d.Toggle(4005, true);
            Check("hide.deactivateThenToggleClearsPending", d.Pending, false);
            Check("hide.deactivateThenConsumeVisible", d.ConsumeToggle(true), true);
            Check("hide.deactivateThenConsumeReset", d.HasToggle, false);

            // Long press: focus can be lost before mouse-up. While a capture is
            // outstanding Evt must NOT queue a hide, so the panel stays visible.
            HideController lp = new HideController();
            lp.State(true, 5000);
            lp.Toggle(5000, true);
            lp.Evt(5080, true); // focus lost mid-hold
            Check("hide.longPressNoPending", lp.Pending, false);
            Check("hide.longPressTickNoHide", lp.Tick(5300, true), false);
            Check("hide.longPressConsumeStaysVisible", lp.ConsumeToggle(true), true);

            // No mouse-down before the click: ConsumeToggle must use the current
            // visibility, NOT a stale capture from an earlier press.
            HideController nd = new HideController();
            nd.State(true, 6000);
            nd.Toggle(6000, true);          // mouse-down while visible
            nd.ConsumeToggle(true);         // click hides it
            nd.State(false, 6010);          // real hide resets the capture
            Check("hide.noDownHasNoCapture", nd.HasToggle, false);
            // Next click with no mouse-down: live visibility (hidden) => open.
            Check("hide.noDownNextClickUsesVisible", nd.ConsumeToggle(false), false);
            Check("hide.noDownConsumeResets", nd.HasToggle, false);

            // Suppression window: the click's own focus loss within the window is
            // suppressed, and the deferred hide still fires later once the timer
            // is allowed to evaluate (no permanent stuck).
            HideController s = new HideController();
            s.State(true, 7000);
            s.Toggle(7000, true);
            Check("hide.suppressConsume", s.ConsumeToggle(true), true);
            s.Evt(7010, true); // own focus loss after click
            Check("hide.suppressedWithinWindow", s.Tick(7020, true), false);
            Check("hide.deferredAfterSuppress", s.Tick(7300, true), true);

            // Cancel clears pending; a later focus loss still hides (no stuck).
            HideController cn = new HideController();
            cn.State(true, 8000);
            cn.Evt(8000, true);
            cn.Cancel();
            Check("hide.cancelClears", cn.Pending, false);
            cn.Evt(8100, true);
            Check("hide.afterCancelStillWorks", cn.Tick(8400, true), true);

            // Already hidden: no pending, no hide.
            HideController e = new HideController();
            e.State(false, 9000);
            e.Evt(9000, false);
            Check("hide.alreadyHiddenNoPending", e.Pending, false);
        }

        private static void ViewEmptyProductsCases()
        {
            // NoSubscription with empty items must still carry a message to show.
            QuotaSnapshot s = QuotaParser.Parse("{\"items\":[]}");
            Check("view.noSubMessage", !string.IsNullOrEmpty(s.Message), true);
            Check("view.noSubStatus", s.Status, QuotaStatus.NoSubscription);
        }

        private static void PanelPositionerCases()
        {
            Rectangle wa = new Rectangle(0, 0, 1920, 1040);
            Rectangle b = PanelPositioner.Bounds(wa, 1.0);
            Check("pos.fits", PanelPositioner.FitsInside(b, wa), true);
            Rectangle b2 = PanelPositioner.Bounds(wa, 1.0);
            Check("pos.stable", b, b2);
            Check("pos.bottomRight", b.Right <= wa.Right && b.Bottom <= wa.Bottom, true);

            // Thin work area: must still fit inside.
            Rectangle thin = new Rectangle(0, 0, 300, 260);
            Rectangle bt = PanelPositioner.Bounds(thin, 1.0);
            Check("pos.thinFits", PanelPositioner.FitsInside(bt, thin), true);

            // Loading and result use the same bounds (fixed size).
            Check("pos.widthRange", LayoutMath.ClampWidth(440, 1.0, 1920), 440);
            Check("pos.widthClampSmall", LayoutMath.ClampWidth(440, 1.0, 200), 200);
        }

        private static void IdentityParseCases()
        {
            bool loggedIn;
            AuthIdentity id;
            bool ok = IdentityParse.TryParseAuth(
                "{\"logged_in\":true,\"active_profile\":{\"name\":\"default\",\"type\":\"volc-sso\","
                + "\"owner_trn\":\"trn:iam::123:user/u\",\"region\":\"cn-beijing\",\"project\":\"proj-1\"}}",
                out loggedIn, out id);
            Check("authParse.ok", ok, true);
            Check("authParse.loggedIn", loggedIn, true);
            Check("authParse.name", id != null ? id.Name : null, "default");
            Check("authParse.type", id != null ? id.Type : null, "volc-sso");

            // Missing logged_in -> false (format error upstream).
            Check("authParse.missing", IdentityParse.TryParseAuth("{\"auth_method\":\"sso\"}",
                out loggedIn, out id), false);

            // A valid synthetic TRN parses through to a known scope.
            AuthIdentity trn = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            Check("authParse.scopeKnown", QueryScope.FromAuth(trn).IsKnown, true);

            // viewer.profile is a STRING, not nested.
            System.Web.Script.Serialization.JavaScriptSerializer ser =
                new System.Web.Script.Serialization.JavaScriptSerializer();
            System.Collections.Generic.Dictionary<string, object> root =
                ser.DeserializeObject("{\"viewer\":{\"account_id\":\"123456789\",\"user_id\":\"alice\","
                + "\"profile\":\"default\",\"tenant\":\"t\",\"region\":\"cn-bj\","
                + "\"project_name\":\"proj-1\",\"is_root\":false},\"items\":[]}")
                as System.Collections.Generic.Dictionary<string, object>;
            ViewerIdentity v = IdentityParse.ParseViewer(root);
            Check("viewer.profileString", v.Profile, "default");
            Check("viewer.accountId", v.AccountId, "123456789");
            Check("viewer.userId", v.UserId, "alice");
            Check("viewer.region", v.Region, "cn-bj");
            Check("viewer.projectName", v.ProjectName, "proj-1");
            Check("viewer.isRoot", v.IsRootKnown && v.IsRoot == false, true);
        }

        private static void IdentityDisplayCases()
        {
            AuthIdentity a = new AuthIdentity();
            a.Present = true;
            a.Type = "agent-plan";
            a.Region = "cn-beijing";
            a.Name = "default";
            a.OwnerTrn = "trn:iam::SECRETACCOUNT:user/SECRETUSER";
            string d = IdentityDisplay.Describe(a);
            Check("display.hasType", d.IndexOf("Agent Plan", StringComparison.Ordinal) >= 0, true);
            Check("display.hasRegion", d.IndexOf("cn-beijing", StringComparison.Ordinal) >= 0, true);
            Check("display.noOwnerTrn", d.IndexOf("SECRETACCOUNT", StringComparison.Ordinal) < 0, true);
            Check("display.noUserName", d.IndexOf("SECRETUSER", StringComparison.Ordinal) < 0, true);

            // Type mapping is product/platform, not the auth method string.
            Check("display.mapAgent", IdentityDisplay.FriendlyType("agent-plan"), "Agent Plan");
            Check("display.mapCoding", IdentityDisplay.FriendlyType("coding-plan"), "Coding Plan");
            Check("display.mapTeam", IdentityDisplay.FriendlyType("team"), "团队");
            Check("display.mapPlatform", IdentityDisplay.FriendlyType("platform"), "平台");
            Check("display.mapSsoNotSpecial", IdentityDisplay.FriendlyType("volc-sso"), "volc-sso");

            // Long custom profile names are length-limited.
            string limited = IdentityDisplay.LimitProfileName(new string('x', 80));
            Check("display.limitedLen", limited.Length <= IdentityDisplay.MaxProfileNameChars + 1, true);
        }

        private static void IpcSignalCases()
        {
            // Uses an isolated event name, never the real singleton name.
            string name = "Local\\ark_left_test_" + Guid.NewGuid().ToString("N");
            bool got = false;
            using (Ipc.Register(name, delegate { got = true; }))
            {
                bool signalled = Ipc.Signal(name, 20, 50);
                Check("ipc.signalled", signalled, true);
                for (int i = 0; i < 40 && !got; i++) System.Threading.Thread.Sleep(50);
            }
            Check("ipc.callbackFired", got, true);

            // Signalling a non-existent event must fail after bounded retries.
            bool none = Ipc.Signal("Local\\ark_left_test_missing_" + Guid.NewGuid().ToString("N"), 2, 1);
            Check("ipc.missingNoBlock", none, false);
        }

        private static void MarkerIsolationCases()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_test_" + Guid.NewGuid().ToString("N"));
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                Check("marker.isolatedPath", Marker.StateDir(), dir);
                Check("marker.notYet", Marker.Exists(), false);
                Marker.WriteFirstRun();
                Check("marker.after", Marker.Exists(), true);
                string content = System.IO.File.ReadAllText(Marker.MarkerPath());
                Check("marker.noSecret", content.IndexOf("SECRET", StringComparison.Ordinal) < 0, true);
                Check("marker.noQuota", content.IndexOf("percent", StringComparison.OrdinalIgnoreCase) < 0, true);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
                catch (Exception) { }
            }
        }

        private static void ProgressStageCases()
        {
            // Auth-confirmed progress carries the identity; usage/done stages fire.
            CliMode mode = new CliMode();
            mode.AuthJson = "{\"logged_in\":true,\"active_profile\":{\"name\":\"d\",\"type\":\"agent-plan\","
                + "\"owner_trn\":\"trn:iam::123456789:user/alice\",\"region\":\"cn-bj\",\"project\":\"p1\"}}";
            mode.UsageJson = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "25", null, null, null)));
            QuotaCli cli = new QuotaCli(mode);
            ProgressCollector pc = new ProgressCollector();
            QueryOutcome o = cli.QueryDetailedAsync(pc, System.Threading.CancellationToken.None).Result;
            Check("progress.authConfirmed", o.AuthConfirmed, true);
            Check("progress.scopeKnown", o.AuthScope != null && o.AuthScope.IsKnown, true);
            Check("progress.identityName", o.Identity != null ? o.Identity.Name : null, "d");
            Check("progress.hasAuthIdentity", pc.SawAuthIdentity, true);
            Check("progress.hasAuthConfirmedFlag", pc.SawAuthConfirmed, true);
            Check("progress.sawUsage", pc.SawUsage, true);
            Check("progress.sawDone", pc.SawDone, true);
            Check("progress.status", o.Snapshot.Status, QuotaStatus.Ok);

            // Logged in but active_profile missing: AuthConfirmed must still fire
            // (identity null) so the UI can clear the old cache.
            CliMode noProfile = new CliMode();
            noProfile.AuthJson = "{\"logged_in\":true}";
            noProfile.UsageJson = "{\"items\":[]}";
            ProgressCollector pc2 = new ProgressCollector();
            QueryOutcome o2 = new QuotaCli(noProfile).QueryDetailedAsync(pc2,
                System.Threading.CancellationToken.None).Result;
            Check("progress.noProfileAuthConfirmed", o2.AuthConfirmed, true);
            Check("progress.noProfileNullIdentity", o2.Identity == null, true);
            Check("progress.noProfileFlagFired", pc2.SawAuthConfirmed, true);
            Check("progress.noProfileScopeUnknown", !(o2.AuthScope != null && o2.AuthScope.IsKnown), true);

            // Cancelled auth -> snapshot Cancelled, no auth confirm.
            CliMode c = new CliMode();
            c.AuthJson = "{\"logged_in\":true}";
            c.DelayMs = 3000;
            System.Threading.CancellationTokenSource cts =
                new System.Threading.CancellationTokenSource();
            cts.Cancel();
            QueryOutcome co = new QuotaCli(c).QueryDetailedAsync(null, cts.Token).Result;
            Check("progress.cancelled", co.Snapshot.Status, QuotaStatus.Cancelled);
            Check("progress.cancelledNotConfirmed", co.AuthConfirmed, false);
        }

        private static void PersistentSnapshotCases()
        {
            AuthIdentity id = AuthId("trn:iam::123:user/u", "sample", "r", "p");
            string fingerprint = QueryScope.FromAuth(id).Fingerprint;
            QuotaSnapshot snap = Parse("{\"items\":[{\"product\":\"coding-plan\",\"error\":\"secret\","
                + "\"updated_at\":\"2026-10-03T10:00:00+08:00\",\"periods\":["
                + "{\"label\":\"monthly\",\"percent\":0,\"used\":0,\"total\":100,"
                + "\"reset_at\":\"2026-11-01T00:00:00+08:00\"},{\"label\":\"weekly\",\"error\":\"secret\"},"
                + "{\"label\":\"5h\"}]}]}");
            snap.FetchedAt = new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Local);
            CachedSnapshot cached = PersistentStateStore.ToCache(snap, fingerprint);
            byte[] bytes = PersistentStateStore.Encode(cached);
            Check("cache.dpapiEncoded", bytes != null, true);
            Check("cache.noPlainFingerprint", Encoding.UTF8.GetString(bytes).Contains(fingerprint), false);
            CachedSnapshot decoded = PersistentStateStore.Decode(bytes);
            QuotaSnapshot restored = PersistentStateStore.FromCache(decoded);
            Check("cache.scope", decoded.ScopeFingerprint, fingerprint);
            Check("cache.time", restored.FetchedAt, snap.FetchedAt);
            Check("cache.partial", restored.Status, QuotaStatus.PartialError);
            Check("cache.subKnown", restored.Products[0].SubscribedKnown, snap.Products[0].SubscribedKnown);
            Check("cache.subscribed", restored.Products[0].Subscribed, snap.Products[0].Subscribed);
            Check("cache.malformed", restored.Products[0].Malformed, snap.Products[0].Malformed);
            Check("cache.productError", restored.Products[0].Error != null, true);
            Check("cache.periodError", restored.Products[0].Periods[1].Error != null, true);
            Check("cache.periodErrorFlag", restored.Products[0].PeriodErrorPresent, true);
            Check("cache.usedZeroKnown", restored.Products[0].Periods[0].UsedKnown, true);
            CheckClose("cache.usedZero", restored.Products[0].Periods[0].Used, 0);
            Check("cache.amountKnown", restored.Products[0].Periods[0].AmountKnown, true);
            CheckClose("cache.amount", restored.Products[0].Periods[0].RemainingAmount, 100);
            Check("cache.unknown", restored.Products[0].Periods[2].PercentKnown, false);
            Check("cache.reset", restored.Products[0].Periods[0].ResetLocal, snap.Products[0].Periods[0].ResetLocal);
            Check("cache.updated", restored.Products[0].UpdatedLocal, snap.Products[0].UpdatedLocal);
            byte[] oldVersion = (byte[])bytes.Clone(); oldVersion[8] = 0;
            Check("cache.oldVersionReject", PersistentStateStore.Decode(oldVersion) == null, true);
            byte[] corrupt = (byte[])bytes.Clone(); corrupt[corrupt.Length - 1] ^= 123;
            Check("cache.dpapiReject", PersistentStateStore.Decode(corrupt) == null, true);
            Check("cache.structureReject", PersistentStateStore.Decode(CacheCrypto.Protect(1,
                Encoding.UTF8.GetBytes("{\"Version\":1,\"ScopeFingerprint\":\"" + fingerprint + "\",\"Products\":[]}"))) == null, true);
            cached.FetchedAt = "invalid"; cached.Products[0].UpdatedLocal = "invalid";
            cached.Products[0].Periods[0].ResetLocal = "invalid";
            restored = PersistentStateStore.FromCache(cached);
            Check("cache.badFetchedUnknown", restored.FetchedAt, DateTime.MinValue);
            Check("cache.badUpdatedUnknown", restored.Products[0].HasUpdated, false);
            Check("cache.badResetUnknown", restored.Products[0].Periods[0].HasReset, false);
            Check("cache.badTimeText", RelativeFormat.Freshness(restored.FetchedAt, DateTime.Now), "更新时间未知");
            cached.FetchedAt = "10:00";
            Check("cache.timeOnlyNeverInfersToday", PersistentStateStore.FromCache(cached).FetchedAt, DateTime.MinValue);
            cached.Products[0].Periods[0].RemainingPercent = double.NaN;
            Check("cache.invalidNumberReject", PersistentStateStore.Decode(PersistentStateStore.Encode(cached)) == null, true);

            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ark_left_cache_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                cached = PersistentStateStore.ToCache(snap, fingerprint);
                Check("cache.save", PersistentStateStore.Save(cached), true);
                Check("cache.restartLoad", PersistentStateStore.Load().ScopeFingerprint, fingerprint);
                PanelModel restarted = new PanelModel();
                PanelView restartView = null;
                using (SnapshotController restart = new SnapshotController(restarted,
                    delegate { return System.Threading.Tasks.Task.FromResult(new QueryOutcome {
                        Snapshot = new QuotaSnapshot { Status = QuotaStatus.Timeout } }); },
                    delegate(PanelView v) { restartView = v; }, delegate { }, PersistentStateStore.Load,
                    PersistentStateStore.Save, PersistentStateStore.Clear))
                {
                    Check("cache.restartDisplaysHistory", restartView.FromCache && restarted.Last != null, true);
                    restart.Start().Wait();
                    Check("cache.restartFailureOriginalTime", restartView.Data.FetchedAt, snap.FetchedAt);
                    Check("cache.restartFailureKeepsDisk", PersistentStateStore.Load().FetchedAt, cached.FetchedAt);
                    restarted.CommitOutcome(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.NotLoggedIn } });
                    Check("cache.logoutClearsDisk", PersistentStateStore.Load() == null, true);
                    Check("cache.logoutClearsMemory", restarted.Last == null, true);
                }
                Check("cache.restoreForReplaceTest", PersistentStateStore.Save(cached), true);
                using (System.IO.FileStream locked = new System.IO.FileStream(PersistentStateStore.FilePath,
                    System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    CachedSnapshot replacement = PersistentStateStore.ToCache(SyntheticSample.Build(), fingerprint);
                    Check("cache.replaceFailure", PersistentStateStore.Save(replacement), false);
                }
                Check("cache.replaceFailureRetains", PersistentStateStore.Load().FetchedAt, cached.FetchedAt);
                Check("cache.nullSaveRetains", PersistentStateStore.Save(null), false);
                Check("cache.afterFailureValid", PersistentStateStore.Load() != null, true);
                PersistentStateStore.Clear();
                Check("cache.clear", PersistentStateStore.Load() == null, true);
                Check("cache.noSubRoundtrip", PersistentStateStore.FromCache(PersistentStateStore.Decode(
                    PersistentStateStore.Encode(PersistentStateStore.ToCache(Parse("{\"items\":[]}"), fingerprint)))).Status,
                    QuotaStatus.NoSubscription);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }

        private static QueryOutcome SuccessfulOutcome(AuthIdentity identity, QuotaSnapshot snapshot)
        {
            return new QueryOutcome { AuthConfirmed = true, Identity = identity,
                AuthScope = QueryScope.FromAuth(identity), Verdict = ScopeVerdict.Same, Snapshot = snapshot };
        }

        private static void SnapshotControllerCases()
        {
            AuthIdentity a = AuthId("trn:iam::123:user/u", "d", "r", "p");
            AuthIdentity b = AuthId("trn:iam::456:user/v", "d", "r", "p");
            QuotaSnapshot history = SyntheticSample.Build();
            history.FetchedAt = new DateTime(2026, 9, 1);
            CachedSnapshot disk = PersistentStateStore.ToCache(history, QueryScope.FromAuth(a).Fingerprint);
            PanelModel model = new PanelModel();
            PanelView shown = null;
            int calls = 0, commits = 0, opens = 0, saves = 0, evictions = 0;
            System.Threading.Tasks.TaskCompletionSource<QueryOutcome> pending = null;
            using (SnapshotController controller = new SnapshotController(model,
                delegate(IProgress<QueryProgress> progress, System.Threading.CancellationToken token) {
                    calls++; pending = new System.Threading.Tasks.TaskCompletionSource<QueryOutcome>(); return pending.Task;
                }, delegate(PanelView view) { commits++; shown = view; }, delegate { opens++; },
                delegate { return disk; }, delegate(CachedSnapshot cache) { saves++; disk = cache; return true; },
                delegate { evictions++; disk = null; }))
            {
                Check("controller.loadedLast", model.Last != null, true);
                Check("controller.loadedFingerprint", model.ConfirmedFingerprint, disk.ScopeFingerprint);
                Check("controller.historyFlags", shown.FromCache && !shown.NewData && !shown.Persist, true);
                for (int i = 0; i < 5; i++) controller.Open();
                Check("controller.allOpensNoQuery", calls, 0);
                Check("controller.opens", opens, 5);
                System.Threading.Tasks.Task start = controller.Start();
                controller.Refresh().Wait(); controller.Poll().Wait(); controller.Start().Wait();
                Check("controller.singleflight", calls, 1);
                Check("controller.waitNoCommit", commits, 1);
                pending.SetResult(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.Failed } });
                start.Wait();
                Check("controller.failedKeepsTime", shown.Data.FetchedAt, history.FetchedAt);
                Check("controller.failedCacheFlag", shown.FromCache, true);
                Check("controller.failedNoSave", saves, 0);
                Check("controller.failedNoEvict", evictions, 0);
                System.Threading.Tasks.Task poll = controller.Poll(); // no Open: hidden polling
                Check("controller.hiddenPoll", calls, 2);
                QueryOutcome unknown = SuccessfulOutcome(a, SyntheticSample.Build()); unknown.Verdict = ScopeVerdict.Unknown;
                pending.SetResult(unknown); poll.Wait();
                Check("controller.unknownKeepsTime", shown.Data.FetchedAt, history.FetchedAt);
                Check("controller.unknownNoSave", saves, 0);
                System.Threading.Tasks.Task refresh = controller.Refresh();
                pending.SetResult(SuccessfulOutcome(a, SyntheticSample.Build())); refresh.Wait();
                Check("controller.successOneCommit", commits, 4);
                Check("controller.successFlags", shown.NewData && shown.Persist && !shown.FromCache, true);
                Check("controller.successSaved", saves, 1);
                Check("controller.noQueuedQuery", calls, 3);
                refresh = controller.Refresh();
                pending.SetResult(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.NotLoggedIn } }); refresh.Wait();
                Check("controller.logoutEvicts", model.Last == null && disk == null, true);
                Check("controller.logoutClearCalled", evictions, 1);
                refresh = controller.Refresh();
                pending.SetResult(SuccessfulOutcome(a, history)); refresh.Wait();
                refresh = controller.Refresh();
                QueryOutcome changedFailure = SuccessfulOutcome(b, new QuotaSnapshot { Status = QuotaStatus.Timeout });
                pending.SetResult(changedFailure); refresh.Wait();
                Check("controller.newScopeEvictsEvenFailure", model.Last == null && disk == null, true);
                Check("controller.newScopeEvictionCount", evictions, 2);
                refresh = controller.Refresh();
                pending.SetResult(SuccessfulOutcome(b, SyntheticSample.Build())); refresh.Wait();
                Check("controller.newScopeSuccess", model.ConfirmedFingerprint, QueryScope.FromAuth(b).Fingerprint);
                refresh = controller.Refresh();
                QueryOutcome mismatch = SuccessfulOutcome(b, SyntheticSample.Build()); mismatch.Verdict = ScopeVerdict.Mismatch;
                pending.SetResult(mismatch); refresh.Wait();
                Check("controller.mismatchEvicts", model.Last == null && disk == null, true);
                Check("controller.mismatchClearCount", evictions, 3);
                refresh = controller.Refresh();
                unknown = SuccessfulOutcome(null, SyntheticSample.Build()); unknown.Verdict = ScopeVerdict.Unknown;
                pending.SetResult(unknown); refresh.Wait();
                Check("controller.unknownNoHistoryLimited", shown.Data != null && shown.IdentityUnknown && !shown.Persist, true);
                Check("controller.unknownNotLast", model.Last == null, true);
                Check("controller.interval", SnapshotController.PollIntervalMs, 300000);
                refresh = controller.Refresh();
                QuotaSnapshot partial = SyntheticSample.Build(); partial.Status = QuotaStatus.PartialError;
                pending.SetResult(SuccessfulOutcome(a, partial)); refresh.Wait();
                Check("controller.partialPersist", shown.Persist && shown.NewData, true);
                refresh = controller.Refresh();
                pending.SetResult(SuccessfulOutcome(a, Parse("{\"items\":[]}"))); refresh.Wait();
                Check("controller.noSubscriptionPersist", shown.Persist && shown.Data.Status == QuotaStatus.NoSubscription, true);
                Check("controller.successKindsSaved", saves, 5);
                model.CommitOutcome(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.NotLoggedIn } });
                Check("controller.evictEvenWithoutLast", evictions, 4);
            }
        }

        private sealed class QueuedContext : System.Threading.SynchronizationContext
        {
            private readonly Queue<Action> _queue = new Queue<Action>();
            public override void Post(System.Threading.SendOrPostCallback callback, object state)
            {
                _queue.Enqueue(delegate { callback(state); });
            }
            public void Drain() { while (_queue.Count != 0) _queue.Dequeue()(); }
        }

        private static void SnapshotProgressCases()
        {
            System.Threading.SynchronizationContext previous = System.Threading.SynchronizationContext.Current;
            QueuedContext context = new QueuedContext();
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                AuthIdentity a = AuthId("trn:iam::123:user/u", "d", "r", "p");
                AuthIdentity b = AuthId("trn:iam::456:user/v", "d", "r", "p");
                CachedSnapshot disk = PersistentStateStore.ToCache(SyntheticSample.Build(), QueryScope.FromAuth(a).Fingerprint);
                PanelModel model = new PanelModel();
                int commits = 0, evictions = 0, saves = 0;
                IProgress<QueryProgress> reporter = null;
                System.Threading.Tasks.TaskCompletionSource<QueryOutcome> pending = null;
                using (SnapshotController controller = new SnapshotController(model,
                    delegate(IProgress<QueryProgress> p, System.Threading.CancellationToken token) {
                        reporter = p; pending = new System.Threading.Tasks.TaskCompletionSource<QueryOutcome>(); return pending.Task;
                    }, delegate { commits++; }, delegate { }, delegate { return disk; },
                    delegate { saves++; return true; }, delegate { evictions++; disk = null; }))
                {
                    System.Threading.Tasks.Task task = controller.Start();
                    reporter.Report(new QueryProgress { Stage = QueryStage.Auth });
                    reporter.Report(new QueryProgress { Stage = QueryStage.Usage, Slow = true });
                    reporter.Report(new QueryProgress { Stage = QueryStage.Auth, AuthConfirmed = true, AuthIdentity = a });
                    reporter.Report(new QueryProgress { Stage = QueryStage.Auth, AuthConfirmed = true, AuthIdentity = null });
                    context.Drain();
                    Check("progressController.waitUnchanged", commits, 1);
                    Check("progressController.waitHistory", model.Last != null, true);
                    reporter.Report(new QueryProgress { Stage = QueryStage.Auth, AuthConfirmed = true, AuthIdentity = b });
                    context.Drain();
                    Check("progressController.newScopeImmediateEvict", model.Last == null && disk == null, true);
                    Check("progressController.newScopeClearOnce", evictions, 1);
                    Check("progressController.newScopeEmpty", model.CurrentView.Message, "暂无数据");
                    Check("progressController.newScopeNoStage", model.CurrentView.Stage, null);
                    pending.SetResult(SuccessfulOutcome(b, SyntheticSample.Build())); context.Drain();
                    Check("progressController.finalCompleted", task.IsCompleted, true);
                    Check("progressController.finalSingleCommit", commits, 3);
                    Check("progressController.finalSave", saves, 1);
                    reporter.Report(new QueryProgress { Stage = QueryStage.Auth, AuthConfirmed = true, AuthIdentity = a });
                    context.Drain();
                    Check("progressController.lateIgnored", commits, 3);
                    Check("progressController.lateScopeRetained", model.ConfirmedFingerprint, QueryScope.FromAuth(b).Fingerprint);
                    IProgress<QueryProgress> oldReporter = reporter;
                    task = controller.Refresh();
                    oldReporter.Report(new QueryProgress { Stage = QueryStage.Auth, AuthConfirmed = true, AuthIdentity = a });
                    context.Drain();
                    Check("progressController.oldGenerationIgnored", commits, 3);
                    pending.SetResult(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.Cancelled } });
                    context.Drain();
                    Check("progressController.cancelRetainsHistory", model.Last != null, true);
                    Check("progressController.cancelNoSave", saves, 1);
                    task = controller.Refresh();
                    controller.Dispose();
                    pending.SetResult(SuccessfulOutcome(a, SyntheticSample.Build())); context.Drain();
                    Check("progressController.disposeNoCommit", commits, 4);
                    Check("progressController.disposeNoSave", saves, 1);
                }
            }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previous); }
        }

        private sealed class ProgressCollector : IProgress<QueryProgress>
        {
            public bool SawUsage;
            public bool SawDone;
            public bool SawAuthConfirmed;
            private bool _authIdentity;
            public void Report(QueryProgress value)
            {
                if (value.Stage == QueryStage.Usage) SawUsage = true;
                if (value.Stage == QueryStage.Done) SawDone = true;
                if (value.Stage == QueryStage.Auth && value.AuthConfirmed) SawAuthConfirmed = true;
                if (value.Stage == QueryStage.Auth && value.AuthIdentity != null) _authIdentity = true;
            }
            public bool SawAuthIdentity { get { return _authIdentity; } }
        }

        private static AuthIdentity AuthId(string owner, string name, string region, string project)
        {
            AuthIdentity a = new AuthIdentity();
            a.Present = true;
            a.OwnerTrn = owner;
            a.Name = name;
            a.Region = region;
            a.Project = project;
            return a;
        }

        // ---- helpers ----

        private static QuotaSnapshot Parse(string json)
        {
            return QuotaParser.Parse(json);
        }

        private static void CliResultTransport(string name, QuotaCli cli, QuotaStatus expected)
        {
            CliResultTransportToken(name, cli, expected, System.Threading.CancellationToken.None);
        }

        private static void CliResultTransportToken(string name, QuotaCli cli, QuotaStatus expected,
            System.Threading.CancellationToken token)
        {
            QuotaSnapshot s = cli.QueryAsync(token).Result;
            Check(name, s.Status, expected);
        }

        // After Dispose, starting a query must not start a process and must fail.
        private static bool TryStartAfterDispose(QuotaCli cli)
        {
            QuotaSnapshot s = cli.QueryAsync(System.Threading.CancellationToken.None).Result;
            return s.Status == QuotaStatus.Failed;
        }

        private static string Wrap(params string[] items)
        {
            return "{\"items\":[" + string.Join(",", items) + "]}";
        }

        private static string Item(string product, string edition, string tier, bool subscribed, string periods)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"product\":\"").Append(product).Append("\"");
            if (edition != null) sb.Append(",\"edition\":\"").Append(edition).Append("\"");
            if (tier != null) sb.Append(",\"tier\":\"").Append(tier).Append("\"");
            sb.Append(",\"subscribed\":").Append(subscribed ? "true" : "false");
            sb.Append(",\"periods\":[").Append(periods).Append("]}");
            return sb.ToString();
        }

        // Builds a period object from optional numeric/string values so the test
        // source contains no fragile nested escapes.
        private static string Period(string label, string percent, string used, string total, string resetAt)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"label\":\"").Append(label).Append("\"");
            if (percent != null) sb.Append(",\"percent\":").Append(percent);
            if (used != null) sb.Append(",\"used\":").Append(used);
            if (total != null) sb.Append(",\"total\":").Append(total);
            if (resetAt != null) sb.Append(",\"reset_at\":\"").Append(resetAt).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        // UX019 (v0.11): pure summary formatter semantics. Exact text match on
        // a fully specified snapshot, "未知" placeholders for missing fields,
        // 0 / 100 / <1% percent semantics, explicit failure retention, cached
        // markers and a hard no-identity guarantee.
        private static void QuotaSummaryCases()
        {
            Check("summary.nullView", QuotaSummary.Build(null) == null, true);
            PanelView empty = new PanelView();
            empty.State = PanelState.NoData;
            Check("summary.noDataNull", QuotaSummary.Build(empty) == null, true);

            QuotaSnapshot snap = new QuotaSnapshot();
            snap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            ProductQuota p1 = new ProductQuota();
            p1.Product = "coding-plan"; p1.DisplayName = "Coding Plan"; p1.Edition = "personal";
            p1.Subscribed = true; p1.SubscribedKnown = true;
            PeriodQuota q1 = new PeriodQuota();
            q1.Label = "monthly";
            q1.PercentKnown = true; q1.RemainingPercent = 25;
            q1.AmountKnown = true; q1.RemainingAmount = 750;
            q1.HasReset = true; q1.ResetLocal = new DateTime(2026, 10, 28, 23, 59, 59);
            p1.Periods.Add(q1);
            PeriodQuota q2 = new PeriodQuota();
            q2.Label = "session"; q2.LabelDisplay = DisplayNames.Period("session");
            p1.Periods.Add(q2);
            snap.Products.Add(p1);

            PanelView v = new PanelView();
            v.State = PanelState.ShowingCurrent; v.Data = snap;
            Check("summary.exact", QuotaSummary.Build(v),
                "方舟订阅额度摘要\r\n" +
                "最后更新 2026-10-04 12:30\r\n" +
                "\r\n" +
                "Coding Plan · 个人版\r\n" +
                "- 每月：剩余 25%，可用剩余 750 额度，2026-10-28 23:59 重置\r\n" +
                "- 会话：剩余未知，可用剩余未知，重置时间未知");

            // Missing fields stay unknown; 0 / 100 / <1% keep the bar wording.
            q2.PercentKnown = true; q2.RemainingPercent = 100;
            Check("summary.percent100", QuotaSummary.Build(v).Contains("- 会话：剩余 100%"), true);
            q2.RemainingPercent = 0;
            Check("summary.percent0", QuotaSummary.Build(v).Contains("- 会话：已用尽"), true);
            q2.RemainingPercent = 0.5;
            Check("summary.percentLt1", QuotaSummary.Build(v).Contains("- 会话：剩余 <1%"), true);
            q2.PercentKnown = false; q2.UnknownNote = "字段缺失";
            Check("summary.unknownNote", QuotaSummary.Build(v).Contains(
                "- 会话：剩余未知，可用剩余未知，重置时间未知（缺少可用的百分比数据。）"), true);
            q2.UnknownNote = null;
            q2.AmountKnown = true; q2.RemainingAmount = 12.5;
            Check("summary.amount", QuotaSummary.Build(v).Contains("可用剩余 12.5"), true);
            q2.AmountKnown = false;
            q2.Error = "该周期获取失败";
            Check("summary.periodError", QuotaSummary.Build(v).Contains("- 会话：该周期获取失败"), true);
            q2.Error = null;

            // A failed product keeps its explicit failure wording.
            ProductQuota p2 = new ProductQuota();
            p2.Product = "agent-plan"; p2.DisplayName = "Agent Plan"; p2.Edition = "personal";
            p2.Tier = "medium"; p2.SubscribedKnown = true; p2.Subscribed = true;
            p2.Error = "套餐获取失败";
            snap.Products.Add(p2);
            Check("summary.productError", QuotaSummary.Build(v).Contains(
                "Agent Plan · 个人版 · 中型：额度获取失败，请稍后重试。"), true);
            snap.Products.Remove(p2);

            // Cached / stale / unknown-identity markers mirror the panel note.
            v.State = PanelState.StaleError;
            Check("summary.stale", QuotaSummary.Build(v).Contains("状态：未能更新，显示上次数据。"), true);
            v.State = PanelState.ShowingCurrent; v.FromCache = true;
            Check("summary.cached", QuotaSummary.Build(v).Contains("状态：上次数据"), true);
            v.FromCache = false; v.IdentityUnknown = true;
            Check("summary.identityUnknown", QuotaSummary.Build(v).Contains("状态：身份未完全确认"), true);
            v.IdentityUnknown = false;

            // Unknown times never become a fabricated date.
            snap.FetchedAt = DateTime.MinValue;
            Check("summary.unknownTime", QuotaSummary.Build(v).Contains("最后更新时间未知"), true);
            snap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);

            // Identity material never leaks into the summary.
            v.IdentityHint = "trn:iam::000000000000:user/sample";
            string s2 = QuotaSummary.Build(v);
            Check("summary.noIdentity", s2.Contains("trn:") || s2.Contains("sample")
                || s2.Contains("profile") || s2.Contains("account"), false);

            // Empty snapshot: honest message, no fabricated products.
            QuotaSnapshot none = new QuotaSnapshot();
            none.Message = "当前身份下未发现已订阅的方舟套餐。";
            PanelView ev = new PanelView();
            ev.State = PanelState.ShowingCurrent; ev.Data = none;
            Check("summary.emptyProducts", QuotaSummary.Build(ev).Contains(
                "当前身份下未发现已订阅的方舟套餐。"), true);

            // UX019 trust boundary: a sentinel placed in the upstream Message /
            // Error / UnknownNote must never survive into the summary. The
            // formatter uses fixed safe wording, independent of the parser's own
            // sanitising, so even an abnormal channel cannot leak raw text.
            const string secret = "SECRET-TOKEN-9f3a";
            none.Message = secret;
            Check("summary.emptyNoSecret", QuotaSummary.Build(ev).Contains(secret), false);
            Check("summary.emptySafeMsg", QuotaSummary.Build(ev).Contains(
                "当前身份下未发现已订阅的方舟套餐。"), true);

            ProductQuota evil = new ProductQuota();
            evil.Product = "agent-plan"; evil.DisplayName = "Agent Plan";
            evil.SubscribedKnown = true; evil.Subscribed = true; evil.Error = secret;
            PeriodQuota eq = new PeriodQuota();
            eq.Label = "5h"; eq.LabelDisplay = DisplayNames.Period("5h");
            eq.PercentKnown = false; eq.UnknownNote = secret;
            evil.Periods.Add(eq);
            QuotaSnapshot evilSnap = new QuotaSnapshot();
            evilSnap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            evilSnap.Message = secret;
            evilSnap.Products.Add(evil);
            PanelView evv = new PanelView();
            evv.State = PanelState.ShowingCurrent; evv.Data = evilSnap;
            string evilText = QuotaSummary.Build(evv);
            Check("summary.noErrorSecret", evilText.Contains(secret), false);
            Check("summary.safeError", evilText.Contains("额度获取失败，请稍后重试。"), true);
            Check("summary.safeNote", evilText.Contains("缺少可用的百分比数据。"), true);

            // Defensive: a null product / null period must not throw; the
            // summary stays usable and honest.
            QuotaSnapshot nulls = new QuotaSnapshot();
            nulls.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            nulls.Products.Add(null);
            ProductQuota np = new ProductQuota();
            np.Product = "coding-plan"; np.DisplayName = "Coding Plan";
            np.SubscribedKnown = true; np.Subscribed = true;
            np.Periods.Add(null);
            nulls.Products.Add(np);
            PanelView nv = new PanelView();
            nv.State = PanelState.ShowingCurrent; nv.Data = nulls;
            string nullText = QuotaSummary.Build(nv);
            Check("summary.nullSafe", nullText != null && nullText.Contains("Coding Plan"), true);
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

        // ---- UX021 v0.13: details-local Ctrl+R / Ctrl+C accelerators ----

        // Reflects the protected Control.ProcessCmdKey so a key can be passed
        // through the REAL pre-processing chain: invoked on the FOCUSED CHILD
        // control it travels the normal parent chain up to the form override
        // (verified offline: a plain probe form sees the child-routed call),
        // and on the form itself for direct guard checks. No SendKeys, no
        // global hotkey and no synthetic OS input is used anywhere; real
        // focus is established with plain Control.Focus() on shown windows.
        private static readonly System.Reflection.MethodInfo ProcessCmdKeyMethod =
            typeof(System.Windows.Forms.Control).GetMethod("ProcessCmdKey",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        private static bool DispatchCmdKey(System.Windows.Forms.Control target,
            System.Windows.Forms.Keys keyData)
        {
            System.Windows.Forms.Message msg = System.Windows.Forms.Message.Create(
                target.Handle, 0x0100, IntPtr.Zero, IntPtr.Zero);
            object[] args = new object[] { msg, keyData };
            return (bool)ProcessCmdKeyMethod.Invoke(target, args);
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

        // UX022: the window hugs the cards — flush left edge, no horizontal
        // overflow, gaps only BETWEEN cards, and (without scrolling) the last
        // card's bottom exactly equals the client bottom.
        private static void CheckCardLayout(PopupForm form, string tag)
        {
            System.Windows.Forms.FlowLayoutPanel content = form.ContentForTest;
            Check(tag + ".cardsPresent", content.Controls.Count > 0, true);
            if (content.Controls.Count == 0) return;
            Check(tag + ".noHorizScroll", content.HorizontalScroll.Visible, false);
            int firstWidth = content.Controls[0].Width;
            int lastBottom = 0;
            for (int i = 0; i < content.Controls.Count; i++)
            {
                System.Windows.Forms.Control card = content.Controls[i];
                Check(tag + ".flushLeft", card.Left, 0);
                Check(tag + ".sameWidth", card.Width, firstWidth);
                Check(tag + ".insideWidth", card.Right <= content.ClientSize.Width, true);
                Check(tag + ".widthPositive", card.Width > 0, true);
                if (i < content.Controls.Count - 1)
                    Check(tag + ".gapBetween", card.Margin.Bottom > 0, true);
                else
                {
                    Check(tag + ".noTailGap", card.Margin.Bottom, 0);
                    lastBottom = card.Bottom;
                }
            }
            bool scrolling = content.AutoScroll && content.VerticalScroll.Visible;
            if (!scrolling)
                Check(tag + ".lastHugsBottom", lastBottom, content.ClientSize.Height);
        }

        // UX022: feedback is a recorded text cleared by the one-shot timer.
        private static void WaitForCopyRestore(PopupForm form,
            System.Windows.Forms.Timer timer)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (form.CopyFeedbackTextForTest != null && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
        }

        // ---- v0.14 UX022: card-only popup (T052) ----

        // Extra coverage beyond the migrated chrome / copy / shortcut cases:
        // multi-card stacking without a tail gap, real two-pass overflow with
        // a reachable last card, the re-anchor consume semantics after a
        // visible size change, the in-place view-level status row inside the
        // first card, focus landing on the first card, and the rounded Region
        // only for the single non-scrolling card shape. All synthetic; zero
        // query, zero clipboard.
        private static void CompactUiCases()
        {
            ModernBlueVisualCases();
            QuotaBar disposedBar = null;
            using (PopupForm compact = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.BuildLarge();
                sample.Products.RemoveRange(1, sample.Products.Count - 1);
                compact.ForceRender(sample);
                compact.ShowPanel();
                compact.SetScaleForTest(1.0);
                Application.DoEvents();
                Check("compact.widthReadable", compact.Width >= 325 && compact.Width <= 344, true);
                Check("compact.heightReadable", compact.Height > 185 && compact.Height <= 340, true);
                Check("compact.noScroll", compact.ContentForTest.VerticalScroll.Visible, false);
                Check("compact.cardBg", compact.BackColor, Color.FromArgb(248, 251, 255));
                CheckCardLayout(compact, "compact.fit");
                CheckPeriodGeometry(compact, "compact.100", 1.0);
                SaveSpacingPreview(compact, "preview-spacing.png");
                compact.SetScaleForTest(1.25);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.125", 1.25);
                compact.SetScaleForTest(1.5);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.150", 1.5);
                compact.SetScaleForTest(2.0);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.200", 2.0);
                SaveSpacingPreview(compact, "preview-spacing-200.png");
                compact.SetScaleForTest(1.0);
                Application.DoEvents();
                CheckQuotaBarMotion(compact);
                foreach (Control card in compact.ContentControls)
                {
                    Check("compact.cardTopSpace", card.Controls[0].Top >= 10, true);
                    foreach (Control child in card.Controls)
                    {
                        Check("compact.childInside", card.ClientRectangle.Contains(child.Bounds), true);
                        Label label = child as Label;
                        if (label != null)
                        {
                            using (Graphics g = label.CreateGraphics())
                            {
                                SizeF measured = g.MeasureString(label.Text, label.Font, Math.Max(1, label.Width));
                                Check("compact.textHeight", measured.Height <= label.Height + 1, true);
                            }
                        }
                    }
                    Check("compact.cardBottomSpace", card.Height - card.Controls[card.Controls.Count - 1].Bottom
                        >= 10, true);
                }
                using (Bitmap bitmap = new Bitmap(compact.Width, compact.Height))
                {
                    compact.DrawToBitmap(bitmap, compact.ClientRectangle);
                    Check("compact.oceanFrame", bitmap.GetPixel(0, compact.Height / 2).ToArgb(),
                        Color.FromArgb(216, 227, 240).ToArgb());
                }
                Console.WriteLine("compact fixture: " + compact.Width + "x" + compact.Height);
                disposedBar = FindQuotaBar(compact);
                CardPanel narrowCard = compact.ContentControls[0] as CardPanel;
                narrowCard.Width = 300;
                narrowCard.InvalidateLayout();
                narrowCard.ForceLayout();
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.narrow300", 1.0);
                compact.HidePanel();
            }
            Check("compact.motion.dispose", disposedBar != null && !disposedBar.MotionRunningForTest, true);

            using (PopupForm longNotes = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.BuildLarge();
                sample.Products.RemoveRange(1, sample.Products.Count - 1);
                longNotes.ForceRender(sample);
                longNotes.ShowPanel();
                Application.DoEvents();
                CardPanel before = longNotes.ContentControls[0] as CardPanel;
                int normalHeight = before.PeriodSurfacesForTest[0].Bounds.Height;
                sample.Products[0].Periods[0].Error =
                    "这是一个很长的周期错误说明，用于确认错误文字会完整换行并推动当前周期背景。";
                sample.Products[0].Periods[1].PercentKnown = false;
                sample.Products[0].Periods[1].UnknownNote =
                    "这是一个很长的未知原因说明，用于确认未知文字会完整换行并推动后续周期。";
                longNotes.ForceRender(sample);
                Application.DoEvents();
                CardPanel after = longNotes.ContentControls[0] as CardPanel;
                CheckPeriodGeometry(longNotes, "compact.longNotes", 1.0);
                Check("compact.longErrorGrows", after.PeriodSurfacesForTest[0].Bounds.Height
                    > normalHeight, true);
                Check("compact.longNoteGap",
                    after.PeriodSurfacesForTest[1].Bounds.Top
                    - after.PeriodSurfacesForTest[0].Bounds.Bottom, 8);
                bool foundLongText = false;
                foreach (Control child in after.Controls)
                {
                    Label label = child as Label;
                    if (label == null || (label.Text.IndexOf("很长", StringComparison.Ordinal) < 0)) continue;
                    foundLongText = true;
                    Check("compact.longTextHeight", TextRenderer.MeasureText(label.Text, label.Font,
                        new Size(label.Width, 300), TextFormatFlags.WordBreak).Height <= label.Height, true);
                    Check("compact.longTextInside", after.PeriodSurfacesForTest[0].Bounds.Contains(label.Bounds)
                        || after.PeriodSurfacesForTest[1].Bounds.Contains(label.Bounds), true);
                }
                Check("compact.longTextFound", foundLongText, true);
                longNotes.HidePanel();
            }

            using (PopupForm longAmount = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.Build();
                if (sample.Products.Count > 1)
                    sample.Products.RemoveRange(1, sample.Products.Count - 1);
                for (int i = 0; i < sample.Products[0].Periods.Count; i++)
                {
                    PeriodQuota period = sample.Products[0].Periods[i];
                    period.AmountKnown = true;
                    period.RemainingAmount = 1e100;
                    period.TotalKnown = true;
                    period.Total = 1e101;
                    period.PercentKnown = true;
                    period.RemainingPercent = 10;
                }
                longAmount.ForceRender(sample);
                longAmount.ShowPanel();
                longAmount.SetScaleForTest(1.0);
                Application.DoEvents();
                CheckLongAmount(longAmount, "compact.long.100");
                longAmount.SetScaleForTest(2.0);
                Application.DoEvents();
                CheckLongAmount(longAmount, "compact.long.200");
                longAmount.HidePanel();
            }

            using (PopupForm unknown = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.Build();
                sample.Products[0].Periods[0].Error = "failed";
                sample.Products[0].Periods[0].AmountKnown = false;
                sample.Products[0].Periods[0].PercentKnown = false;
                unknown.ForceRender(sample);
                unknown.ShowPanel();
                Application.DoEvents();
                CheckPeriodGeometry(unknown, "compact.error", 1.0);
                Check("compact.error.noBarMotion", FindQuotaBar(unknown).MotionRunningForTest, false);
                unknown.HidePanel();
            }
        }

        private sealed class VisualButton : ModernButton
        {
            internal void Hover() { OnMouseEnter(EventArgs.Empty); }
            internal void Press() { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)); }
            internal void Release() { OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)); }
            internal void LeavePointer() { OnMouseLeave(EventArgs.Empty); }
        }

        private sealed class VisualCombo : ModernComboBox
        {
            internal void PaintItem(Graphics g, bool selected, bool closed = false)
            {
                OnDrawItem(new DrawItemEventArgs(g, Font, new Rectangle(0, 0, 180, 26), 0,
                    (selected ? DrawItemState.Selected : DrawItemState.None)
                    | (closed ? DrawItemState.ComboBoxEdit : DrawItemState.None)));
            }
        }

        private static void CheckButtonPixel(VisualButton button, Color expected, string tag)
        {
            using (Bitmap bmp = new Bitmap(button.Width, button.Height))
            {
                button.DrawToBitmap(bmp, button.ClientRectangle);
                Check(tag, bmp.GetPixel(12, 12).ToArgb(), expected.ToArgb());
                Check(tag + ".roundCorner", bmp.GetPixel(0, 0).ToArgb(), Color.White.ToArgb());
            }
        }

        private static void CheckMenuTheme(ToolStripDropDown menu, string tag)
        {
            Check(tag + ".white", menu.BackColor, Color.White);
            Check(tag + ".renderer", menu.Renderer.GetType().Name, "ModernMenuRenderer");
            foreach (ToolStripItem item in menu.Items)
            {
                Check(tag + ".ink", item.ForeColor, UiStyle.Navy);
                ToolStripMenuItem child = item as ToolStripMenuItem;
                if (child == null) continue;
                using (Bitmap bmp = new Bitmap(Math.Max(10, item.Width), Math.Max(10, item.Height)))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    menu.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, item));
                    Check(tag + ".checkedSurface", bmp.GetPixel(5, 5).ToArgb(),
                        (child.Checked ? UiStyle.Selected : Color.White).ToArgb());
                }
                if (child.HasDropDownItems) CheckMenuTheme(child.DropDown, tag + ".child");
            }
        }

        private static void ModernBlueVisualCases()
        {
            Check("modern.primary", UiStyle.Primary.ToArgb(), Color.FromArgb(58, 131, 247).ToArgb());
            Check("modern.text", UiStyle.Navy.ToArgb(), Color.FromArgb(36, 65, 92).ToArgb());
            using (VisualButton b = new VisualButton())
            {
                b.Size = new Size(84, 34);
                UiStyle.StyleButton(b, true);
                CheckButtonPixel(b, UiStyle.Primary, "modern.button.default");
                b.Hover(); CheckButtonPixel(b, UiStyle.PrimaryHover, "modern.button.hover");
                b.Press(); CheckButtonPixel(b, UiStyle.PrimaryPressed, "modern.button.pressed");
                b.Release(); CheckButtonPixel(b, UiStyle.PrimaryHover, "modern.button.release");
                b.Enabled = false; CheckButtonPixel(b, UiStyle.Track, "modern.button.disabled");
                b.Enabled = true; b.LeavePointer(); UiStyle.StyleButton(b, false);
                CheckButtonPixel(b, Color.White, "modern.secondary.default");
                b.Hover(); CheckButtonPixel(b, UiStyle.Secondary, "modern.secondary.hover");
                b.Press(); CheckButtonPixel(b, UiStyle.Selected, "modern.secondary.pressed");
            }
            using (VisualCombo combo = new VisualCombo())
            using (Bitmap bmp = new Bitmap(180, 26))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                combo.Items.Add("Agent Plan · 5 小时");
                combo.PaintItem(g, false);
                Check("modern.selector.default", bmp.GetPixel(170, 12).ToArgb(), Color.White.ToArgb());
                combo.PaintItem(g, true);
                Check("modern.selector.selected", bmp.GetPixel(170, 12).ToArgb(), UiStyle.Selected.ToArgb());
                combo.PaintItem(g, true, true);
                Check("modern.selector.closedSelected", bmp.GetPixel(170, 12).ToArgb(), Color.White.ToArgb());
                bool navyInk = false;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < 160; x++)
                        if (bmp.GetPixel(x, y).ToArgb() == UiStyle.Navy.ToArgb()) navyInk = true;
                Check("modern.selector.closedInk", navyInk, true);
            }
            using (ContextMenuStrip menu = new ContextMenuStrip())
            using (Bitmap bmp = new Bitmap(100, 30))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ToolStripMenuItem item = new ToolStripMenuItem("设置");
                menu.Items.Add(item); UiStyle.StyleMenu(menu);
                item.Select();
                menu.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, item));
                Check("modern.menu.hover", bmp.GetPixel(5, 5).ToArgb(), UiStyle.TealLight.ToArgb());
            }
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));
            using (FloatingSettingsForm settings = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; }))
            {
                Check("modern.settings.white", settings.BackColor, Color.White);
                Check("modern.settings.header", settings.HeaderForTest.BackColor, Color.White);
                Check("modern.settings.button", settings.SaveButtonForTest is ModernButton, true);
                Check("modern.settings.selector", settings.ComboForTest is ModernComboBox, true);
                Check("modern.settings.rounded", settings.Region != null, true);
            }
            using (FloatingQuotaForm floating = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; }, delegate(FloatingPreferences p) { return true; }))
            {
                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                floating.ApplyModelView(model.OnUsageResult(SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null));
                ContextMenuStrip menu = floating.CircleForTest.ContextMenuStrip;
                UiStyle.StyleMenu(menu);
                CheckMenuTheme(menu, "modern.menu");
                ToolStripMenuItem settings = (ToolStripMenuItem)menu.Items[0];
                ToolStripMenuItem content = (ToolStripMenuItem)settings.DropDownItems[0];
                ToolStripItem old = content.DropDownItems[0];
                floating.PopulateContentMenu(content);
                Check("modern.menu.rebuilt", ReferenceEquals(old, content.DropDownItems[0]), false);
                CheckMenuTheme(menu, "modern.menu.rebuilt");
            }
            using (QuotaBar bar = new QuotaBar())
            using (Bitmap bmp = new Bitmap(100, 6))
            {
                bar.Size = bmp.Size; bar.Value = 50;
                bar.DrawToBitmap(bmp, bar.ClientRectangle);
                Check("modern.bar.fill", bmp.GetPixel(20, 3).ToArgb(), UiStyle.Primary.ToArgb());
                Check("modern.bar.track", bmp.GetPixel(80, 3).ToArgb(), UiStyle.Track.ToArgb());
            }
        }

        private static void CheckLongAmount(PopupForm form, string tag)
        {
            bool found = false;
            string expected = DisplayNames.Number(1e100) + " 额度";
            foreach (Control card in form.ContentControls)
                for (int i = 0; i + 2 < card.Controls.Count; i++)
                {
                    QuotaBar bar = card.Controls[i] as QuotaBar;
                    Label amount = card.Controls[i + 2] as Label;
                    if (bar == null || amount == null) continue;
                    found = true;
                    Check(tag + ".fullTooWide", TextRenderer.MeasureText(expected, amount.Font).Width
                        > amount.Width, true);
                    Check(tag + ".scientific", amount.Text.IndexOf("E+", StringComparison.Ordinal) >= 0, true);
                    Check(tag + ".measured", TextRenderer.MeasureText(amount.Text, amount.Font).Width
                        <= amount.Width, true);
                    Check(tag + ".tooltip", form.ToolTipForTest.GetToolTip(amount), expected);
                }
            Check(tag + ".found", found, true);
        }

        private static QuotaBar FindQuotaBar(PopupForm form)
        {
            foreach (Control card in form.ContentControls)
                foreach (Control child in card.Controls)
                    if (child is QuotaBar) return (QuotaBar)child;
            return null;
        }

        private static void SaveSpacingPreview(PopupForm form, string name)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(path);
            }
            Console.WriteLine("spacing preview: " + path);
        }

        private static int ScaleLogical(int logical, double scale)
        {
            return (int)Math.Round(logical * scale);
        }

        private static void CheckPeriodGeometry(PopupForm form, string tag)
        {
            CheckPeriodGeometry(form, tag, 1.0);
        }

        private static void CheckPeriodGeometry(PopupForm form, string tag, double scale)
        {
            foreach (Control card in form.ContentControls)
            {
                CardPanel panel = card as CardPanel;
                Check(tag + ".surfaceCount", panel == null ? 0 : panel.PeriodSurfacesForTest.Count,
                    panel == null ? 0 : Convert.ToInt32(card.Tag));
                if (panel == null) continue;
                int edge = ScaleLogical(10, scale);
                int innerPad = ScaleLogical(10, scale);
                int topPad = ScaleLogical(8, scale);
                int rowGap = ScaleLogical(4, scale);
                int radius = ScaleLogical(8, scale);
                int expectedWidth = card.ClientSize.Width - edge * 2;
                int firstBarLeft = -1;
                int firstBarRight = -1;
                for (int s = 0; s < panel.PeriodSurfacesForTest.Count; s++)
                {
                    CardPanel.PeriodSurface surface = panel.PeriodSurfacesForTest[s];
                    Check(tag + ".surfaceFill" + s, surface.Fill, Color.FromArgb(242, 247, 255));
                    Check(tag + ".surfaceRadius" + s, surface.Radius, radius);
                    Check(tag + ".surfaceLeft" + s, surface.Bounds.Left, edge);
                    Check(tag + ".surfaceRight" + s, card.ClientSize.Width - surface.Bounds.Right, edge);
                    Check(tag + ".surfaceWidth" + s, surface.Bounds.Width, expectedWidth);
                    if (s == panel.PeriodSurfacesForTest.Count - 1)
                        Check(tag + ".surfaceBottomPad" + s,
                            card.ClientSize.Height - surface.Bounds.Bottom, edge);
                    Check(tag + ".surfaceTopPad" + s, surface.Bounds.Top >= 0, true);
                    if (s == 0 && card.Controls.Count > 0)
                        Check(tag + ".titleGap", surface.Bounds.Top - card.Controls[0].Bottom,
                            ScaleLogical(10, scale));
                    if (s > 0)
                        Check(tag + ".surfaceGap" + s,
                            surface.Bounds.Top - panel.PeriodSurfacesForTest[s - 1].Bounds.Bottom,
                            ScaleLogical(8, scale));
                }
                for (int i = 0; i < card.Controls.Count; i++)
                {
                    QuotaBar bar = card.Controls[i] as QuotaBar;
                    if (bar == null || i < 2 || i + 2 >= card.Controls.Count) continue;
                    Label type = card.Controls[i - 2] as Label;
                    Label pct = card.Controls[i - 1] as Label;
                    Label date = card.Controls[i + 1] as Label;
                    Label amount = card.Controls[i + 2] as Label;
                    if (type == null || pct == null || date == null || amount == null) continue;
                    int surfaceIndex = 0;
                    for (int j = 0; j < i; j++)
                        if (card.Controls[j] is QuotaBar) surfaceIndex++;
                    CardPanel.PeriodSurface surface = panel.PeriodSurfacesForTest[surfaceIndex];
                    Check(tag + ".contentLeft", type.Left, surface.Bounds.Left + innerPad);
                    Check(tag + ".contentRight", pct.Right, surface.Bounds.Right - innerPad);
                    Check(tag + ".contentTop", type.Top, surface.Bounds.Top + topPad);
                    Check(tag + ".auxGap", date.Top - type.Bottom, rowGap);
                    if (firstBarLeft < 0)
                    {
                        firstBarLeft = bar.Left;
                        firstBarRight = bar.Right;
                    }
                    Check(tag + ".barStart", bar.Left, firstBarLeft);
                    Check(tag + ".barEnd", bar.Right, firstBarRight);
                    Check(tag + ".surfaceContainsRow", surface.Bounds.Contains(type.Bounds)
                        && surface.Bounds.Contains(pct.Bounds)
                        && surface.Bounds.Contains(date.Bounds)
                        && surface.Bounds.Contains(amount.Bounds), true);
                    Check(tag + ".typeBar", type.Bounds.IntersectsWith(bar.Bounds), false);
                    Check(tag + ".barPct", bar.Bounds.IntersectsWith(pct.Bounds), false);
                    Check(tag + ".barLargest", bar.Width > type.Width && bar.Width > pct.Width, true);
                    Check(tag + ".firstCenter", Math.Abs(type.Bounds.Top + type.Height / 2
                        - (bar.Top + bar.Height / 2)) <= 1, true);
                    Check(tag + ".pctCenter", Math.Abs(pct.Bounds.Top + pct.Height / 2
                        - (bar.Top + bar.Height / 2)) <= 1, true);
                    Check(tag + ".dateNoLabel", date.Text.IndexOf("重置", StringComparison.Ordinal) < 0, true);
                    Check(tag + ".dateLeft", date.Left, type.Left);
                    Check(tag + ".amountRight", amount.Right, pct.Right);
                    Check(tag + ".secondNoOverlap", date.Bounds.IntersectsWith(amount.Bounds), false);
                    Check(tag + ".barHeight", bar.Height, ScaleLogical(6, scale));
                    Check(tag + ".dateMeasured", TextRenderer.MeasureText(date.Text, date.Font).Width
                        <= date.Width, true);
                    Check(tag + ".amountMeasured", TextRenderer.MeasureText(amount.Text, amount.Font).Width
                        <= amount.Width, true);
                    Check(tag + ".dateHeight", TextRenderer.MeasureText(date.Text, date.Font,
                        new Size(date.Width, 100), TextFormatFlags.WordBreak).Height <= date.Height, true);
                    Check(tag + ".amountHeight", TextRenderer.MeasureText(amount.Text, amount.Font,
                        new Size(amount.Width, 100), TextFormatFlags.WordBreak).Height <= amount.Height, true);
                    Check(tag + ".inside", card.ClientRectangle.Contains(type.Bounds)
                        && card.ClientRectangle.Contains(bar.Bounds)
                        && card.ClientRectangle.Contains(pct.Bounds)
                        && card.ClientRectangle.Contains(date.Bounds)
                        && card.ClientRectangle.Contains(amount.Bounds), true);
                    if (amount.Text.IndexOf("E+", StringComparison.Ordinal) >= 0)
                        Check(tag + ".longTooltip", form.ToolTipForTest.GetToolTip(amount).Contains("额度"), true);
                }
            }
        }

        private static void CheckQuotaBarMotion(PopupForm form)
        {
            QuotaBar bar = FindQuotaBar(form);
            Check("compact.motion.exists", bar != null, true);
            if (bar == null) return;
            Check("compact.motion.static", bar.MotionRunningForTest, false);
            int phase = bar.PhaseForTest;
            double value = bar.Value;
            bar.TickForTest();
            Check("compact.motion.valueStable", bar.Value, value);
            Check("compact.motion.noTick", bar.PhaseForTest, phase);
            form.HidePanel();
            Check("compact.motion.hidden", bar.MotionRunningForTest, false);
            form.ShowPanel();
            Check("compact.motion.reopenStatic", bar.MotionRunningForTest, false);
            form.SetReduceMotion(true);
            Check("compact.motion.reduced", bar.MotionRunningForTest, false);
            form.HidePanel();
            form.ShowPanel();
            Check("compact.motion.reducedAcrossHide", bar.MotionRunningForTest, false);
            form.SetReduceMotion(false);
            Check("compact.motion.restoredStatic", bar.MotionRunningForTest, false);
            bar.Value = 0;
            Check("compact.motion.zero", bar.MotionRunningForTest, false);
            bar.Value = -1;
            Check("compact.motion.unknown", bar.MotionRunningForTest, false);
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
                        Color ring = bitmap.GetPixel(0, circle.Height / 2);
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

        private static System.Windows.Forms.Control FindStatusLabelForTest(
            System.Windows.Forms.Control card)
        {
            foreach (Control c in card.Controls)
                if (c is System.Windows.Forms.Label
                    && (string)c.Tag == "status") return c;
            return null;
        }

        // ---- v0.12 UX020: explicit floating window re-home (悬浮窗归位).
        // Production and tests share the same re-home core; work area /
        // scale are injected here so offline tests never depend on real
        // Screen geometry. ----

        private static void RepositionHomeUX020Cases()
        {
            Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            Rectangle home1 = FloatingCircleControl.InitialBounds(area, 1.0);

            // Scales 1 / 1.5 / 2: home == InitialBounds of the injected work
            // area (bottom-right, default safe margin, DPI-scaled 136dp
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

        // ---- Shared settings / visibility / exit menu; the 设置 submenu
        // expands natively to the side (never a modal) and lists 悬浮内容
        // directly, saving FIRST and only applying a successful save. ----
        private static void FloatingMenuUX023Cases()
        {
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                Check("ux023.topCount", app.MenuItemCountForTest, 3);
                Check("ux023.topSettings", app.MenuTextForTest(0), "设置");
                Check("ux023.topExit", app.MenuTextForTest(2), "退出 ark_left");
                Check("ux023.submenuCount", app.MenuSettingsCountForTest, 1);
                Check("ux023.submenuContent", app.MenuSettingsTextForTest(0), "悬浮内容");
                Check("ux023.topToggle", app.MenuTextForTest(1), "隐藏悬浮窗");

                // Clicking 设置 expands the native side dropdown and NEVER opens
                // the settings modal (zero query).
                Check("ux023.settingsNotRequestedYet",
                    app.MenuSettingsDropDownRequestedForTest, false);
                app.PerformMenuSettingsClickForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("ux023.settingsDropDownRequested",
                    app.MenuSettingsDropDownRequestedForTest, true);
                Check("ux023.settingsNoModal", app.SettingsOpenForTest, false);
                Check("ux023.settingsZeroQuery", queries, 0);

                // Real native expansion: show the isolated strip, click 设置,
                // and assert the submenu drop-down is really visible beside it
                // (not merely the intent bool). No desktop input injection; the
                // finally always closes the popups.
                ContextMenuStrip strip = app.MenuForTest;
                ToolStripDropDown drop = app.MenuSettingsForTest.DropDown;
                try
                {
                    strip.Show(new Point(240, 240));
                    System.Windows.Forms.Application.DoEvents();
                    app.PerformMenuSettingsClickForTest();
                    System.Windows.Forms.Application.DoEvents();
                    Check("ux023.realDropDownVisible", drop.Visible, true);
                    Rectangle db = drop.Bounds;
                    Screen dscr = Screen.FromPoint(new Point(db.Left + 1, db.Top + 1));
                    Check("ux023.realDropDownOnScreen",
                        dscr.WorkingArea.IntersectsWith(db), true);
                    Check("ux023.realDropDownSized", db.Width > 0 && db.Height > 0, true);
                    Check("ux023.realDropDownBeside",
                        db.Left >= strip.Bounds.Left || db.Right <= strip.Bounds.Right, true);
                    Check("ux023.realNoModal", app.SettingsOpenForTest, false);
                    Check("ux023.realZeroQuery", queries, 0);
                }
                finally
                {
                    try { drop.Close(); } catch (Exception) { }
                    try { strip.Close(); } catch (Exception) { }
                }

                // Populate content with data and select a non-current period.
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                app.OpenMenuContentForTest();
                Check("ux023.contentCount", app.MenuContentCountForTest > 0, true);
                Check("ux023.contentEnabled", app.MenuContentEnabledForTest, true);
                int current = -1, other = -1, checkedCount = 0;
                for (int i = 0; i < app.MenuContentCountForTest; i++)
                {
                    if (app.MenuContentCheckedForTest(i)) { checkedCount++; current = i; }
                    else if (other < 0) other = i;
                }
                Check("ux023.contentCurrentChecked", current >= 0, true);
                Check("ux023.contentSingleChecked", checkedCount, 1);
                Check("ux023.contentOtherExists", other >= 0, true);
                string before = app.SelectedPeriodTextForTest;
                app.PerformMenuContentForTest(other);
                System.Windows.Forms.Application.DoEvents();
                Check("ux023.contentSwitched", app.SelectedPeriodTextForTest != before, true);
                Check("ux023.contentNoModal", app.SettingsOpenForTest, false);
                Check("ux023.contentZeroQuery", queries, 0);
            }

            // Failure: the save is attempted FIRST and a failed save keeps the
            // old value plus a short hint (no modal, no query).
            int failSaves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { failSaves++; return false; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                f.ApplyModelView(BuildLargeView());
                f.OpenDefaultMenuContentForTest();
                Check("ux023.failCandidates", f.DefaultMenuContentCountForTest > 0, true);
                int pick = f.DefaultMenuContentCheckedForTest(0) ? 1 : 0;
                Check("ux023.failPickExists", pick < f.DefaultMenuContentCountForTest, true);
                f.PerformDefaultMenuContentForTest(pick);
                Check("ux023.failSaveAttempted", failSaves, 1);
                Check("ux023.failKeepsStored", f.StoredSettingsForTest, null);
                Check("ux023.failHint", f.LockHintForTest, "悬浮内容未保存");
                Check("ux023.failNoModal", f.SettingsOpenForTest, false);
            }

            // Empty: no candidates -> the 悬浮内容 submenu is disabled.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                Check("ux023.formTopCount", f.DefaultMenuTopCountForTest, 3);
                Check("ux023.formTopSettings", f.DefaultMenuTopTextForTest(0), "设置");
                Check("ux023.formTopToggle", f.DefaultMenuTopTextForTest(1), "显示悬浮窗");
                Check("ux023.formTopExit", f.DefaultMenuTopTextForTest(2), "退出 ark_left");
                Check("ux023.formSubmenuCount", f.DefaultMenuSettingsCountForTest, 1);
                Check("ux023.formSubmenuContent", f.DefaultMenuSettingsTextForTest(0), "悬浮内容");
                f.PerformDefaultMenuSettingsForTest();
                Check("ux023.formDropDownRequested",
                    f.SettingsDropDownRequestedForTest, true);
                Check("ux023.formNoModal", f.SettingsOpenForTest, false);
                f.OpenDefaultMenuContentForTest();
                Check("ux023.emptyCount", f.DefaultMenuContentCountForTest, 0);
                Check("ux023.emptyDisabled", f.DefaultMenuContentEnabledForTest, false);
            }

            // Stable refresh vs real change: a routine same-semantics refresh
            // (the 10s poll) keeps the menu items / checks and stays selectable;
            // only a genuine candidate or identity change invalidates an item
            // captured before it. Data cleared while open empties / disables.
            int staleSaves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { staleSaves++; return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                f.ApplyModelView(BuildLargeView());
                f.OpenDefaultMenuContentForTest();
                int n0 = f.DefaultMenuContentCountForTest;
                Check("ux023.sameCandidates", n0 > 0, true);
                int gen0 = f.ContentGenerationForTest;
                int checked0 = 0;
                for (int i = 0; i < n0; i++)
                    if (f.DefaultMenuContentCheckedForTest(i)) checked0++;
                Check("ux023.sameCheckedOnce", checked0, 1);

                // Same semantics (same view): no rebuild, no invalidation.
                f.ApplyModelView(BuildLargeView());
                Check("ux023.sameGenStable", f.ContentGenerationForTest, gen0);
                Check("ux023.sameCountStable", f.DefaultMenuContentCountForTest, n0);
                int checked1 = 0;
                for (int i = 0; i < n0; i++)
                    if (f.DefaultMenuContentCheckedForTest(i)) checked1++;
                Check("ux023.sameCheckedStable", checked1, 1);
                int pick = f.DefaultMenuContentCheckedForTest(0) ? 1 : 0;
                f.PerformDefaultMenuContentForTest(pick);
                Check("ux023.sameStillSaves", staleSaves, 1);

                // A truly different candidate set rejects a pre-change item.
                List<FloatingEntry> old = FloatingSelection.SelectableCandidates(
                    FloatingSelection.Build(SyntheticSample.BuildLarge()));
                int genBefore = f.ContentGenerationForTest;
                f.ApplyModelView(BuildSingleView());
                Check("ux023.changeGenBumped", f.ContentGenerationForTest != genBefore, true);
                f.ApplyContentSelection(old[0], genBefore); // stale captured click
                Check("ux023.staleNotSaved", staleSaves, 1);
                Check("ux023.staleHint", f.LockHintForTest, "悬浮内容不可用");

                // Identity change alone (same candidates) also invalidates.
                // T-057: the authority is the scope fingerprint, not the display
                // IdentityHint (a real owner change can keep the same hint).
                PanelView other = OwnerScopeView("trn:iam::000000000000:user/other",
                    PercentSnapshot(10));
                int genId = f.ContentGenerationForTest;
                f.ApplyModelView(other);
                Check("ux023.identityGenBumped", f.ContentGenerationForTest != genId, true);

                f.ApplyModelView(EmptyView());
                Check("ux023.clearedCount", f.DefaultMenuContentCountForTest, 0);
                Check("ux023.clearedDisabled", f.DefaultMenuContentEnabledForTest, false);
            }

            // T-057: two real PanelModel views for the SAME candidates but a
            // genuinely different owner (owner_trn alice -> bob) while type /
            // region / profile name - and therefore the friendly IdentityHint -
            // stay identical. The scope fingerprint differs, so an item captured
            // under the old owner must be rejected; a same-scope refresh must
            // still save.
            int ownerSaves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { ownerSaves++; return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                PanelView alice = OwnerScopeView("trn:iam::000000000000:user/alice",
                    SyntheticSample.BuildLarge());
                PanelView bob = OwnerScopeView("trn:iam::000000000000:user/bob",
                    SyntheticSample.BuildLarge());
                Check("ux023.t057SameHint", alice.IdentityHint, bob.IdentityHint);
                Check("ux023.t057DiffFingerprint",
                    alice.ScopeFingerprint != bob.ScopeFingerprint, true);

                f.ApplyModelView(alice);
                f.OpenDefaultMenuContentForTest();
                int owner0 = f.DefaultMenuContentCountForTest;
                Check("ux023.t057Candidates", owner0 > 0, true);
                int genOwner = f.ContentGenerationForTest;

                // Same scope renewal: no rebuild and the selection still saves.
                f.ApplyModelView(OwnerScopeView("trn:iam::000000000000:user/alice",
                    SyntheticSample.BuildLarge()));
                Check("ux023.t057SameScopeStable", f.ContentGenerationForTest, genOwner);
                int ownerPick = f.DefaultMenuContentCheckedForTest(0) ? 1 : 0;
                f.PerformDefaultMenuContentForTest(ownerPick);
                Check("ux023.t057SameScopeSaves", ownerSaves, 1);

                // Owner switch, same hint + same candidates: the pre-change item
                // must be refused (no stale save). Pick a candidate that is NOT
                // the current selection, so only the generation mismatch (the
                // scope change) can explain the refusal.
                List<FloatingEntry> aliceCandidates = FloatingSelection.SelectableCandidates(
                    FloatingSelection.Build(alice.Data));
                FloatingSettings cur = f.StoredSettingsForTest;
                FloatingEntry stale = null;
                for (int i = 0; i < aliceCandidates.Count && stale == null; i++)
                {
                    FloatingEntry e = aliceCandidates[i];
                    if (cur == null || e.ProductKey != cur.ProductKey || e.Label != cur.PeriodLabel)
                        stale = e;
                }
                Check("ux023.t057StaleCandidateFound", stale != null, true);
                int genBefore = f.ContentGenerationForTest;
                f.ApplyModelView(bob);
                Check("ux023.t057OwnerGenBumped",
                    f.ContentGenerationForTest != genBefore, true);
                f.ApplyContentSelection(stale, genBefore);
                Check("ux023.t057OwnerStaleNotSaved", ownerSaves, 1);
                Check("ux023.t057OwnerStaleHint", f.LockHintForTest, "悬浮内容不可用");
            }

            // (a) ClearContentItems followed immediately by Dispose releases the
            // removed items synchronously; a late posted drain is a safe no-op.
            FloatingQuotaForm d = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; });
            d.ApplyModelView(BuildLargeView());
            d.OpenDefaultMenuContentForTest();          // build the first set
            System.Windows.Forms.Application.DoEvents();
            int builtCount = d.DefaultMenuContentCountForTest;
            Check("ux023.disposeBuilt", builtCount > 0, true);
            int relBefore = d.ContentItemsReleasedForTest;
            d.OpenDefaultMenuContentForTest();          // remove -> queued, drain posted
            d.Dispose();                                // must drain synchronously
            System.Windows.Forms.Application.DoEvents(); // late callback must be safe
            Check("ux023.disposeDrains",
                d.ContentItemsReleasedForTest - relBefore, builtCount);

            // A rebuild deterministically releases the previous items; the
            // release is posted, so it is flushed by the message pump.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                f.ApplyModelView(BuildLargeView());
                f.OpenDefaultMenuContentForTest();
                System.Windows.Forms.Application.DoEvents(); // flush posted releases
                int n = f.DefaultMenuContentCountForTest;
                Check("ux023.releaseFirstBuild", n > 0, true);
                int released0 = f.ContentItemsReleasedForTest;
                f.OpenDefaultMenuContentForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("ux023.releaseRebuild",
                    f.ContentItemsReleasedForTest - released0, n);
                int released1 = f.ContentItemsReleasedForTest;
                f.OpenDefaultMenuContentForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("ux023.releaseRepeat",
                    f.ContentItemsReleasedForTest - released1, n);
            }

            // Details-right settings modal is preserved; its label is now
            // 悬浮内容 (details menu itself is unchanged).
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; }))
            {
                Check("ux023.modalHeading", dlg.HeadingForTest.Text, "悬浮内容");
                Check("ux023.modalAccessible", dlg.AccessibleName, "悬浮内容");
            }
        }

        private static PanelView BuildLargeView()
        {
            PanelModel model = new PanelModel();
            model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
            return model.OnUsageResult(SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
        }

        private static PanelView BuildSingleView()
        {
            PanelModel model = new PanelModel();
            model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
            return model.OnUsageResult(PercentSnapshot(10), null, ScopeVerdict.Same, null);
        }

        // T-057: a real PanelModel-produced view for a given owner, with the
        // same friendly identity descriptor regardless of owner_trn. Used to
        // prove the scope fingerprint - not the hint - drives menu invalidation.
        private static PanelView OwnerScopeView(string ownerTrn, QuotaSnapshot snap)
        {
            PanelModel model = new PanelModel();
            AuthIdentity id = PopupForm.SampleIdentity();
            id.OwnerTrn = ownerTrn;
            model.OnAuthResult(true, id, QuotaStatus.Ok, null);
            return model.OnUsageResult(snap, null, ScopeVerdict.Same, null);
        }

        private static void Check(string name, object actual, object expected)
        {
            bool ok = Equals(actual, expected)
                || (actual != null && expected != null
                    && actual.GetType() != expected.GetType()
                    && Convert.ToString(actual) == Convert.ToString(expected));
            if (ok) { _passed++; }
            else
            {
                _failed++;
                _failures.Add(name + ": expected [" + expected + "] got [" + actual + "]");
            }
        }

        private static void CheckClose(string name, double actual, double expected)
        {
            if (Math.Abs(actual - expected) < 0.0001) { _passed++; }
            else
            {
                _failed++;
                _failures.Add(name + ": expected " + expected + " got " + actual);
            }
        }
    }
}
