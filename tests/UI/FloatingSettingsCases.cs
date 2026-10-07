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

    }
}
