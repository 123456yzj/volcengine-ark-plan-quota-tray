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
            // no real NotifyIcon); visible circle uses the same tray path ----
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

                // Circle visible: failures still notify through the tray;
                // no query, state still kept old.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.visibleNow", app.FloatingVisibleForTest, true);
                app.PerformMenuLockForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs.visibleFailHint", app.LockHintForTest, "锁定状态未保存");
                Check("prefs.visibleFailNotifyIntent", app.LockFailNotifyCountForTest, 2);
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

                // Format 3 round-trip keeps every field (lock + motion + theme).
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = true;
                p.ReduceMotion = true;
                p.AccentIndex = 4;
                p.DarkMode = true;
                Check("prefs2.save", FloatingPreferencesStore.Save(p), true);
                FloatingPreferences back = FloatingPreferencesStore.Load();
                Check("prefs2.version", back.Version, 3);
                Check("prefs2.locked", back.PositionLocked, true);
                Check("prefs2.reduceMotion", back.ReduceMotion, true);
                Check("prefs2.accent", back.AccentIndex, 4);
                Check("prefs2.dark", back.DarkMode, true);
                string raw = System.IO.File.ReadAllText(FloatingPreferencesStore.FilePath,
                    System.Text.Encoding.UTF8);
                Check("prefs2.rawFormat3",
                    raw.Contains("\"Version\":3") && raw.Contains("\"PositionLocked\"")
                    && raw.Contains("\"ReduceMotion\"") && raw.Contains("\"AccentIndex\"")
                    && raw.Contains("\"DarkMode\""), true);

                // A later lock-only change still carries the other flags.
                FloatingPreferences lockOnly = new FloatingPreferences();
                lockOnly.Version = FloatingPreferencesStore.FormatVersion;
                lockOnly.PositionLocked = false;
                lockOnly.ReduceMotion = true;
                lockOnly.AccentIndex = 4;
                lockOnly.DarkMode = true;
                Check("prefs2.resave", FloatingPreferencesStore.Save(lockOnly), true);
                FloatingPreferences both = FloatingPreferencesStore.Load();
                Check("prefs2.lockSaved", both.PositionLocked, false);
                Check("prefs2.motionKept", both.ReduceMotion, true);
                Check("prefs2.accentKept", both.AccentIndex, 4);
                Check("prefs2.darkKept", both.DarkMode, true);

                // Legacy format 2 (strict three fields) migrates IN MEMORY to
                // format 3 with the default theme; the file is untouched.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":2,\"PositionLocked\":true,\"ReduceMotion\":true}", Encoding.UTF8);
                string before2 = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                FloatingPreferences migrated2 = FloatingPreferencesStore.Load();
                Check("prefs2.migrated2Locked", migrated2.PositionLocked, true);
                Check("prefs2.migrated2Motion", migrated2.ReduceMotion, true);
                Check("prefs2.migrated2Version", migrated2.Version, 3);
                Check("prefs2.migrated2AccentDefault", migrated2.AccentIndex, 0);
                Check("prefs2.migrated2DarkDefault", migrated2.DarkMode, false);
                string afterRead2 = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                Check("prefs2.migrate2NoWrite", afterRead2, before2);

                // Legacy format 1 (strict two fields) migrates IN MEMORY to
                // format 3 with ReduceMotion = false and the default theme; the
                // file is untouched.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":1,\"PositionLocked\":true}", Encoding.UTF8);
                string before = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                FloatingPreferences migrated = FloatingPreferencesStore.Load();
                Check("prefs2.migratedLocked", migrated.PositionLocked, true);
                Check("prefs2.migratedVersion", migrated.Version, 3);
                Check("prefs2.migratedMotionDefault", migrated.ReduceMotion, false);
                Check("prefs2.migratedAccentDefault", migrated.AccentIndex, 0);
                Check("prefs2.migratedDarkDefault", migrated.DarkMode, false);
                string afterRead = System.IO.File.ReadAllText(
                    FloatingPreferencesStore.FilePath, System.Text.Encoding.UTF8);
                Check("prefs2.migrateNoWrite", afterRead, before);

                // Non-schema is never accepted: wrong version, wrong types,
                // unknown extra fields, missing fields - in any format.
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":9,\"PositionLocked\":true,\"ReduceMotion\":false}", Encoding.UTF8);
                Check("prefs2.wrongVersionNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":3,\"PositionLocked\":1,\"ReduceMotion\":false,"
                    + "\"AccentIndex\":0,\"DarkMode\":false}", Encoding.UTF8);
                Check("prefs2.wrongTypeNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":3,\"PositionLocked\":true,\"ReduceMotion\":false,"
                    + "\"AccentIndex\":0,\"DarkMode\":false,\"Extra\":1}", Encoding.UTF8);
                Check("prefs2.extraFieldNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":3,\"PositionLocked\":true,\"ReduceMotion\":false}", Encoding.UTF8);
                Check("prefs2.missingFieldNull", FloatingPreferencesStore.Load(), null);
                System.IO.File.WriteAllText(FloatingPreferencesStore.FilePath,
                    "{\"Version\":3,\"PositionLocked\":true,\"ReduceMotion\":false,"
                    + "\"AccentIndex\":\"x\",\"DarkMode\":false}", Encoding.UTF8);
                Check("prefs2.wrongAccentTypeNull", FloatingPreferencesStore.Load(), null);
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

            // Shared menu / preference handlers: zero query, poll interval untouched.
            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                app.HideDetailsForTest();
                Check("prefs2.trayMenuCount", app.MenuItemCountForTest, 5);
                Check("prefs2.traySettingsCount", app.MenuSettingsCountForTest, 3);
                Check("prefs2.trayToggleShifted", app.MenuTextForTest(2), "隐藏悬浮窗");
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

                // Circle visible: failures still notify through the tray;
                // zero query, poll cadence intact.
                app.OpenEntryForTest("circle");
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.visibleNow", app.FloatingVisibleForTest, true);
                app.PerformMenuMotionForTest();
                System.Windows.Forms.Application.DoEvents();
                Check("prefs2.visibleMotionHint", app.LockHintForTest, "动画设置未保存");
                Check("prefs2.visibleMotionNotifyIntent",
                    app.MotionFailNotifyCountForTest, 2);
                Check("prefs2.visibleMotionZeroQuery", mqueries, 0);
                Check("prefs2.visiblePoll10s", app.PollIntervalForTest, 10000);
            }
        }

    }
}
