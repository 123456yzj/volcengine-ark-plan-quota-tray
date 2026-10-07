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

    }
}
