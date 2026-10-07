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
                Check("ux023.topCount", app.MenuItemCountForTest, 5);
                Check("ux023.topSettings", app.MenuTextForTest(1), "设置");
                Check("ux023.topExit", app.MenuTextForTest(4), "退出 ark_left");
                Check("ux023.submenuCount", app.MenuSettingsCountForTest, 1);
                Check("ux023.submenuContent", app.MenuSettingsTextForTest(0), "悬浮内容");
                Check("ux023.topToggle", app.MenuTextForTest(2), "隐藏悬浮窗");

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

    }
}
