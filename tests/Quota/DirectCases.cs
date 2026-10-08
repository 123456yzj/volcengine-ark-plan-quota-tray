using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private const string DirectFixture = "{\"Result\":{\"PlanType\":\"medium\","
            + "\"AFPFiveHour\":{\"Quota\":10000,\"Used\":566.8145,\"ResetTime\":1791394599000},"
            + "\"AFPDaily\":{\"Quota\":50000,\"Used\":0,\"ResetTime\":1791388800000},"
            + "\"AFPWeekly\":{\"Quota\":35000,\"Used\":949.684,\"ResetTime\":1791734400000},"
            + "\"AFPMonthly\":{\"Quota\":100000,\"Used\":34809.1513,\"ResetTime\":1793203199000}}}";

        private static void DirectCases()
        {
            DirectSigningAndMapping();
            DirectCredentialCases();
            DirectRenewalCases();
            DirectLogoutCases();
            DirectCallbackCases();
            ManualLoginWindowCases();
            DirectTrayCases();
            DirectHeaderCases();
        }

        private static void DirectSigningAndMapping()
        {
            DirectSts sts = new DirectSts { AccessKey = "test-ak", SecretKey = "test-sk", SessionToken = "test-session" };
            Dictionary<string, string> headers = DirectArkUsage.Sign(sts, new DateTime(2026, 10, 7, 1, 2, 3, DateTimeKind.Utc));
            // Golden value from the separately verified Python PoC signer.
            Check("direct.sign.pythonGolden", headers["Authorization"].EndsWith(
                "Signature=4ef2bb60948bb3b657e8ac73b95fbaa3dc5a81dac61dd65450cdc1ca731b6fe1"), true);
            Check("direct.sign.securityHeader", headers["X-Security-Token"] == sts.SessionToken, true);
            Check("direct.sign.includesSecurity", headers["Authorization"].Contains("x-security-token"), true);
            sts.SessionToken = "different-session";
            Check("direct.sign.tokenChangesSignature", headers["Authorization"] != DirectArkUsage.Sign(sts,
                new DateTime(2026, 10, 7, 1, 2, 3, DateTimeKind.Utc))["Authorization"], true);
            QuotaSnapshot snapshot = DirectArkUsage.Parse(new DirectResponse { Status = 200, Body = DirectFixture });
            Check("direct.map.ok", snapshot.Status, QuotaStatus.Ok);
            ProductQuota product = snapshot.Products[0];
            Check("direct.map.tier", product.Tier, "medium");
            Check("direct.map.fourWindows", product.Periods.Count, 4);
            Check("direct.map.daily", product.Periods[1].LabelDisplay, "每日");
            Check("direct.map.dailyZero", product.Periods[1].UsedKnown && product.Periods[1].Used == 0, true);
            Check("direct.map.dailyRemaining", product.Periods[1].RemainingAmount, 50000.0);
            string cli = "{\"items\":[{\"product\":\"agent-plan\",\"edition\":\"personal\",\"tier\":\"medium\",\"subscribed\":true,\"periods\":["
                + "{\"label\":\"5h\",\"used\":566.8145,\"total\":10000,\"percent\":5.668144999999999,\"reset_at\":\"2026-10-08T01:36:39+08:00\"},"
                + "{\"label\":\"weekly\",\"used\":949.684,\"total\":35000,\"percent\":2.713382857142857,\"reset_at\":\"2026-10-12T00:00:00+08:00\"},"
                + "{\"label\":\"monthly\",\"used\":34809.1513,\"total\":100000,\"percent\":34.809151299999996,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]}]}";
            ProductQuota reference = QuotaParser.Parse(cli).Products[0];
            foreach (PeriodQuota expected in reference.Periods)
            {
                PeriodQuota actual = product.Periods.Find(delegate(PeriodQuota p) { return p.Label == expected.Label; });
                Check("direct.compare." + expected.Label + ".used", actual.Used, expected.Used);
                Check("direct.compare." + expected.Label + ".total", actual.Total, expected.Total);
                CheckClose("direct.compare." + expected.Label + ".percent", actual.RemainingPercent, expected.RemainingPercent);
                Check("direct.compare." + expected.Label + ".reset", actual.ResetLocal, expected.ResetLocal);
            }
            Check("direct.map.noSubscription", DirectArkUsage.Parse(new DirectResponse { Status = 200, Body = "{\"Result\":null}" }).Status, QuotaStatus.NoSubscription);
            Check("direct.map.missingResult", DirectArkUsage.Parse(new DirectResponse { Status = 200, Body = "{}" }).Status, QuotaStatus.FormatError);
            Check("direct.map.missingDaily", DirectArkUsage.Parse(new DirectResponse { Status = 200,
                Body = DirectFixture.Replace("AFPDaily", "UnknownDaily") }).Status, QuotaStatus.PartialError);
            Check("direct.map.missingNumberUnknown", DirectArkUsage.Parse(new DirectResponse { Status = 200,
                Body = DirectFixture.Replace("\"Used\":0", "\"Used\":null") }).Products[0].Periods[1].UsedKnown, false);
            foreach (KeyValuePair<int, QuotaStatus> pair in new Dictionary<int, QuotaStatus> {
                { 401, QuotaStatus.Unauthorized }, { 403, QuotaStatus.Forbidden } })
                Check("direct.error." + pair.Key, DirectArkUsage.Parse(new DirectResponse { Status = pair.Key, Body = "not-json" }).Status, pair.Value);
            QuotaSnapshot error = DirectArkUsage.Parse(new DirectResponse { Status = 403,
                Body = "{\"ResponseMetadata\":{\"Error\":{\"Code\":\"SignatureDoesNotMatch\",\"Message\":\"test-secret\"}}}" });
            Check("direct.error.signatureBefore403", error.Status, QuotaStatus.SignatureFailed);
            Check("direct.error.noRawMessage", error.Message.Contains("test-secret"), false);
        }

        private static void DirectCredentialCases()
        {
            string root = Path.Combine(Marker.StateDir(), "direct-store-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "session.dat");
            try
            {
                DirectSessionStore store = new DirectSessionStore(path);
                DirectSession session = new DirectSession { RefreshToken = "synthetic-refresh", Binding = Guid.NewGuid().ToString("N") };
                Check("direct.store.save", store.Save(session), true);
                Check("direct.store.encrypted", Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(session.RefreshToken), false);
                Check("direct.store.roundtrip", store.Load().RefreshToken == session.RefreshToken, true);
                session.RefreshToken = "rotated-refresh";
                Check("direct.store.replace", store.Save(session), true);
                Check("direct.store.rotation", store.Load().RefreshToken == session.RefreshToken, true);
                Check("direct.store.onlyRefreshAndBinding", typeof(DirectSession).GetFields().Length, 3);
                using (DirectAgentPlan service = new DirectAgentPlan(new DirectFakeHttp(), new DirectFakeBrowser(),
                    store, delegate { return DateTime.UtcNow; }, false))
                {
                    using (FileStream held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        Check("direct.store.logoutLockedFile", service.LogoutAsync(CancellationToken.None).Result.Failure, "CredentialStorageFailed");
                    Check("direct.store.failedLogoutKeepsFile", File.Exists(path), true);
                    Check("direct.store.logoutRetry", service.LogoutAsync(CancellationToken.None).Result.ExitCode, 0);
                    Check("direct.store.logoutDeletesFile", File.Exists(path), false);
                    Check("direct.store.logoutCannotRestore", store.Load() == null, true);
                }
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                Check("direct.store.corrupt", store.Load() == null, true);
                store.Clear();
                Check("direct.store.clear", File.Exists(path), false);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private sealed class DirectMemoryStore : IDirectSessionStore
        {
            public DirectSession Value;
            public bool FailSave;
            public bool FailClear;
            public DirectSession Load() { return Value; }
            public bool Save(DirectSession session) { if (FailSave) return false; Value = session; return true; }
            public void Clear() { if (FailClear) throw new IOException("synthetic clear failure"); Value = null; }
        }
        private sealed class DirectFakeBrowser : IDirectBrowserLogin
        {
            public int Calls;
            public bool Fail;
            public TaskCompletionSource<DirectAuthorization> Pending;
            public Task<DirectAuthorization> AuthorizeAsync(CancellationToken token)
            {
                Calls++; token.ThrowIfCancellationRequested();
                if (Pending != null) { token.Register(delegate { Pending.TrySetCanceled(); }); return Pending.Task; }
                if (Fail) throw new DirectFailure(QuotaStatus.LoginFailed, "synthetic login failure");
                return Task.FromResult(new DirectAuthorization { Code = "synthetic-code", Verifier = "synthetic-verifier",
                    RedirectUri = "http://127.0.0.1:12345/oauth/callback" });
            }
        }
        private sealed class DirectFakeHttp : IDirectTransport
        {
            public int Refreshes, Logins, Queries;
            public bool FailRefresh, NetworkFail, OmitRotatedRefresh;
            public int UsageStatus = 200;
            public string LastRefresh;
            public bool Signed;
            public TaskCompletionSource<DirectResponse> PendingUsage;
            public Task<DirectResponse> PostAsync(string url, string type, byte[] body,
                IDictionary<string, string> headers, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (NetworkFail) throw new DirectFailure(QuotaStatus.NetworkError, "synthetic network failure");
                if (url == DirectArkUsage.Url)
                {
                    Queries++; Signed = headers.ContainsKey("Authorization") && headers.ContainsKey("X-Security-Token")
                        && type == "application/json" && Encoding.UTF8.GetString(body) == "{}";
                    if (PendingUsage != null) { token.Register(delegate { PendingUsage.TrySetCanceled(); }); return PendingUsage.Task; }
                    return Task.FromResult(new DirectResponse { Status = UsageStatus, Body = DirectFixture });
                }
                Dictionary<string, string> form = DirectBrowserLogin.ParseQuery(Encoding.UTF8.GetString(body));
                if (form["grant_type"] == "refresh_token")
                {
                    Refreshes++; LastRefresh = form["refresh_token"];
                    if (FailRefresh) return Task.FromResult(new DirectResponse { Status = 400, Body = "{\"error\":\"invalid_grant\"}" });
                }
                else { Logins++; }
                Dictionary<string, object> value = new Dictionary<string, object> { { "access_token", new Dictionary<string, object> {
                    { "access_key_id", "synthetic-ak" }, { "secret_access_key", "synthetic-sk" }, { "session_token", "synthetic-session" } } },
                    { "expires_in", 900 } };
                if (!OmitRotatedRefresh || Logins > 0) value["refresh_token"] = "rotated-refresh";
                return Task.FromResult(new DirectResponse { Status = 200, Body = new JavaScriptSerializer().Serialize(value) });
            }
        }

        private static void DirectRenewalCases()
        {
            DateTime now = new DateTime(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc);
            DirectMemoryStore store = new DirectMemoryStore();
            DirectFakeBrowser browser = new DirectFakeBrowser();
            DirectFakeHttp http = new DirectFakeHttp();
            using (DirectAgentPlan service = new DirectAgentPlan(http, browser, store, delegate { return now; }, false))
            {
                Check("direct.first.notLoggedIn", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
                Check("direct.first.noImplicitBrowser", browser.Calls, 0);
                Check("direct.login.success", service.LoginAsync(CancellationToken.None).Result.ExitCode, 0);
                QueryOutcome first = service.QueryDetailedAsync(null, CancellationToken.None).Result;
                Check("direct.first.query", first.Snapshot.Status, QuotaStatus.Ok);
                Check("direct.first.signed", http.Signed, true);
                Check("direct.first.knownScope", first.AuthScope.IsKnown, true);
                PanelModel model = new PanelModel();
                Check("direct.first.display", model.CommitOutcome(first).NewData, true);
                Check("direct.first.cache", model.CurrentView.Persist, true);
                now = now.AddSeconds(790);
                service.RenewIfNeededAsync().GetAwaiter().GetResult();
                Check("direct.renew.beforeExpiry", http.Refreshes, 1);
                Check("direct.renew.rotatedToken", http.LastRefresh == "rotated-refresh", true);
                QueryOutcome after = service.QueryDetailedAsync(null, CancellationToken.None).Result;
                Check("direct.renew.scopeStable", after.AuthScope.Fingerprint, first.AuthScope.Fingerprint);
                now = now.AddSeconds(901);
                Task<QueryOutcome>[] concurrent = { service.QueryDetailedAsync(null, CancellationToken.None), service.QueryDetailedAsync(null, CancellationToken.None) };
                Task.WaitAll(concurrent);
                Check("direct.expired.oneRefresh", http.Refreshes, 2);
                Check("direct.expired.queryContinues", concurrent[0].Result.Snapshot.Status, QuotaStatus.Ok);
                http.FailRefresh = true;
                now = now.AddSeconds(901);
                QueryOutcome relogin = service.QueryDetailedAsync(null, CancellationToken.None).Result;
                Check("direct.refreshFailure.browser", browser.Calls, 2);
                Check("direct.refreshFailure.recovered", relogin.Snapshot.Status, QuotaStatus.Ok);
                Check("direct.refreshFailure.flag", relogin.RefreshFailed, true);
                Check("direct.relogin.newScope", relogin.AuthScope.Fingerprint != first.AuthScope.Fingerprint, true);
                browser.Fail = true; now = now.AddSeconds(901);
                QueryOutcome failed = service.QueryDetailedAsync(null, CancellationToken.None).Result;
                Check("direct.refreshFailure.distinct", failed.Snapshot.Status, QuotaStatus.TokenRefreshFailed);
                Check("direct.refreshFailure.noSecret", failed.Snapshot.Message.Contains("synthetic"), false);
            }
            store.Value = new DirectSession { Binding = Guid.NewGuid().ToString("N"), RefreshToken = "persisted-refresh" };
            string binding = store.Value.Binding;
            http = new DirectFakeHttp { OmitRotatedRefresh = true }; browser = new DirectFakeBrowser();
            using (DirectAgentPlan restarted = new DirectAgentPlan(http, browser, store, delegate { return now; }, false))
            {
                QueryOutcome outcome = restarted.QueryDetailedAsync(null, CancellationToken.None).Result;
                Check("direct.restart.refreshWithoutBrowser", browser.Calls, 0);
                Check("direct.restart.success", outcome.Snapshot.Status, QuotaStatus.Ok);
                Check("direct.restart.binding", store.Value.Binding, binding);
                Check("direct.restart.keepRefreshWhenOmitted", store.Value.RefreshToken == "persisted-refresh", true);
                http.NetworkFail = true;
                Check("direct.network.distinct", restarted.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NetworkError);
                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    cancel.Cancel();
                    Check("direct.cancel", restarted.QueryDetailedAsync(null, cancel.Token).Result.Snapshot.Status, QuotaStatus.Cancelled);
                }
            }
            store = new DirectMemoryStore { FailSave = true }; http = new DirectFakeHttp();
            using (DirectAgentPlan service = new DirectAgentPlan(http, new DirectFakeBrowser(), store, delegate { return now; }, false))
            {
                Check("direct.storageFailure", service.LoginAsync(CancellationToken.None).Result.Failure, "CredentialStorageFailed");
                Check("direct.storageFailure.noSession", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
            }
            using (DirectAgentPlan service = new DirectAgentPlan(new DirectFakeHttp(), new DirectFakeBrowser { Fail = true },
                new DirectMemoryStore(), delegate { return now; }, false))
                Check("direct.loginFailure.distinct", service.LoginAsync(CancellationToken.None).Result.Failure, "LoginFailed");
        }

        private static void DirectLogoutCases()
        {
            DateTime now = DateTime.UtcNow;
            DirectMemoryStore store = new DirectMemoryStore();
            DirectFakeBrowser browser = new DirectFakeBrowser();
            DirectFakeHttp http = new DirectFakeHttp();
            using (DirectAgentPlan service = new DirectAgentPlan(http, browser, store, delegate { return now; }, false))
            {
                service.LoginAsync(CancellationToken.None).GetAwaiter().GetResult();
                string binding = store.Value.Binding;
                http.PendingUsage = new TaskCompletionSource<DirectResponse>();
                Task<QueryOutcome> query = service.QueryDetailedAsync(null, CancellationToken.None);
                Task<CliResult> logout = service.LogoutAsync(CancellationToken.None);
                bool completed = Task.WaitAll(new Task[] { query, logout }, 5000);
                Check("direct.logout.activeQueryCompletes", completed, true);
                if (!completed) return;
                Check("direct.logout.queryCancelled", query.Result.Snapshot.Status, QuotaStatus.Cancelled);
                Check("direct.logout.success", logout.Result.ExitCode, 0);
                Check("direct.logout.storeCleared", store.Value == null, true);
                Check("direct.logout.noSession", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
                now = now.AddSeconds(901);
                service.RenewIfNeededAsync().GetAwaiter().GetResult();
                Check("direct.logout.noRenewal", http.Refreshes, 0);
                Check("direct.logout.noImplicitLogin", browser.Calls, 1);
                http.PendingUsage = null;
                service.LoginAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check("direct.logout.reloginNewBinding", store.Value.Binding != binding, true);
                Check("direct.logout.reloginQueries", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.Ok);

                store.FailClear = true;
                Check("direct.logout.storageFailure", service.LogoutAsync(CancellationToken.None).Result.Failure, "CredentialStorageFailed");
                Check("direct.logout.failureDropsMemory", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
                store.FailClear = false;
                Check("direct.logout.retry", service.LogoutAsync(CancellationToken.None).Result.ExitCode, 0);
                Check("direct.logout.retryClearsStore", store.Value == null, true);

                browser.Pending = new TaskCompletionSource<DirectAuthorization>();
                Task<CliResult> login = service.LoginAsync(CancellationToken.None);
                logout = service.LogoutAsync(CancellationToken.None);
                completed = Task.WaitAll(new Task[] { login, logout }, 5000);
                Check("direct.logout.activeLoginCompletes", completed, true);
                if (!completed) return;
                Check("direct.logout.loginCancelled", login.Result.Cancelled, true);
                Check("direct.logout.loginCannotRestoreStore", store.Value == null, true);

                browser.Pending = null;
                service.LoginAsync(CancellationToken.None).GetAwaiter().GetResult();
                now = now.AddSeconds(901); http.FailRefresh = true;
                browser.Pending = new TaskCompletionSource<DirectAuthorization>();
                Task renewal = service.RenewIfNeededAsync();
                Check("direct.logout.backgroundLoginPending", renewal.IsCompleted, false);
                logout = service.LogoutAsync(CancellationToken.None);
                completed = Task.WaitAll(new Task[] { renewal, logout }, 5000);
                Check("direct.logout.backgroundCompletes", completed, true);
                if (!completed) return;
                Check("direct.logout.backgroundCannotRestoreStore", store.Value == null, true);
                Check("direct.logout.backgroundFailureCleared", service.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
            }
            using (DirectAgentPlan restarted = new DirectAgentPlan(http, browser, store, delegate { return now; }, false))
                Check("direct.logout.restartNotLoggedIn", restarted.QueryDetailedAsync(null, CancellationToken.None).Result.Snapshot.Status, QuotaStatus.NotLoggedIn);
        }

        private static bool DirectPump(Task task)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
            return task.IsCompleted;
        }

        private static void DirectTrayCases()
        {
            DateTime now = DateTime.UtcNow;
            DirectMemoryStore store = new DirectMemoryStore();
            DirectFakeBrowser browser = new DirectFakeBrowser();
            DirectFakeHttp http = new DirectFakeHttp();
            using (DirectAgentPlan service = new DirectAgentPlan(http, browser,
                store, delegate { return now; }, false))
            using (TrayApp app = new TrayApp(service.QueryDetailedAsync, null, service.LoginAsync, service.LogoutAsync))
            {
                Task login = app.LoginForTest();
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (!login.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
                Check("direct.tray.loginCompleted", login.IsCompleted, true);
                PopupForm form = app.DetailsFormForTest;
                Check("direct.tray.currentData", form.Model.CurrentView.Data != null, true);
                Check("direct.tray.windows", form.Model.CurrentView.Data.Products[0].Periods.Count, 4);
                app.ShowDetailsForTest(); Application.DoEvents();
                Check("direct.tray.card", form.ContentCardCount, 1);
                Check("direct.tray.dailyHidden", DirectContainsText(form, "每日"), false);
                Check("direct.tray.visiblePeriods", Convert.ToInt32(form.ContentForTest.Controls[0].Tag), 3);
                now = now.AddSeconds(901);
                Task refresh = app.RefreshForTest();
                deadline = DateTime.UtcNow.AddSeconds(5);
                while (!refresh.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
                Check("direct.tray.expiredRefreshCompleted", refresh.IsCompleted, true);
                Check("direct.tray.stillCurrent", form.Model.CurrentView.Data.Status, QuotaStatus.Ok);
                int evictions = 0;
                form.Model.EvictPersistent = delegate { evictions++; };
                string binding = store.Value.Binding;
                http.PendingUsage = new TaskCompletionSource<DirectResponse>();
                refresh = app.RefreshForTest();
                Task logout = app.LogoutForTest();
                bool completed = DirectPump(Task.WhenAll(refresh, logout));
                Check("direct.tray.logoutCompleted", completed, true);
                if (!completed) return;
                Check("direct.tray.logoutClearsCache", evictions > 0 && form.Model.Last == null && form.Model.ConfirmedFingerprint == null, true);
                Check("direct.tray.logoutClearsDisplay", form.Model.CurrentView.Data == null, true);
                Check("direct.tray.logoutClearsCredentials", store.Value == null, true);
                Check("direct.tray.logoutMessage", form.Model.CurrentView.Message.Contains("已登出"), true);
                http.PendingUsage = null;
                login = app.LoginForTest();
                completed = DirectPump(login);
                Check("direct.tray.reloginCompleted", completed, true);
                if (!completed) return;
                Check("direct.tray.reloginNewBinding", store.Value.Binding != binding, true);
                Check("direct.tray.reloginCurrentData", form.Model.CurrentView.Data != null, true);

                store.FailClear = true;
                completed = DirectPump(app.LogoutForTest());
                Check("direct.tray.logoutFailureCompletes", completed, true);
                if (!completed) return;
                Check("direct.tray.logoutFailureReported", form.Model.CurrentView.Message.Contains("登出未完成"), true);
                Check("direct.tray.logoutFailureClearsDisplay", form.Model.Last == null, true);
                Check("direct.tray.logoutFailureEnablesRetry", app.MenuSettingsForTest.DropDownItems[1].Enabled, true);
                store.FailClear = false;
                completed = DirectPump(app.LogoutForTest());
                Check("direct.tray.logoutRetryCompletes", completed && store.Value == null, true);
                if (!completed) return;

                browser.Pending = new TaskCompletionSource<DirectAuthorization>();
                login = app.LoginForTest();
                logout = app.LogoutForTest();
                completed = DirectPump(Task.WhenAll(login, logout));
                Check("direct.tray.logoutDuringLoginCompletes", completed, true);
                if (!completed) return;
                Check("direct.tray.cancelledLoginCannotRestoreData", form.Model.Last == null && store.Value == null, true);
                Check("direct.tray.logoutDuringLoginMessage", form.Model.CurrentView.Message.Contains("已登出"), true);
                Check("direct.tray.loginTextRestored", app.MenuSettingsTextForTest(2), "重新登录");
            }
            DirectBrowserLogin manual = new DirectBrowserLogin(delegate(ManualLoginRequest request, CancellationToken token) {
                return ManualLoginForm.ShowAsync(request, token, delegate { }, delegate(ManualLoginForm form) {
                    string state = DirectBrowserLogin.ParseQuery(new Uri(request.LoginUrl).Query)["state"];
                    form.CallbackInput.Text = request.RedirectUri + "?code=tray-manual-code&state=" + state;
                    form.SubmitButton.PerformClick();
                });
            });
            using (DirectAgentPlan service = new DirectAgentPlan(new DirectFakeHttp(), manual,
                new DirectMemoryStore(), delegate { return now; }, false))
            using (TrayApp app = new TrayApp(service.QueryDetailedAsync, null, service.LoginAsync))
            {
                Task login = app.LoginForTest();
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (!login.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
                Check("direct.manual.tray.loginCompleted", login.IsCompleted, true);
                PanelView view = app.DetailsFormForTest.Model.CurrentView;
                Check("direct.manual.tray.queriesAfterSubmission", view.Data != null && view.Data.Status == QuotaStatus.Ok, true);
            }
        }

        private static void DirectLiveUiCases()
        {
            // Explicit integration entry only. The normal suite stays offline.
            using (DirectAgentPlan service = new DirectAgentPlan())
            {
                QueryOutcome outcome = service.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                Check("direct.live.status", outcome.Snapshot.Status, QuotaStatus.Ok);
                if (outcome.Snapshot.Status != QuotaStatus.Ok) return;
                using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress, CancellationToken token) {
                    return Task.FromResult(outcome); }))
                {
                    app.RunQueryForTest(); app.ShowDetailsForTest(); Application.DoEvents();
                    PopupForm form = app.DetailsFormForTest;
                    Check("direct.live.currentData", form.Model.CurrentView.Data != null, true);
                    Check("direct.live.fourWindows", form.Model.CurrentView.Data.Products[0].Periods.Count, 4);
                    Check("direct.live.tier", form.Model.CurrentView.Data.Products[0].Tier, "medium");
                    Check("direct.live.dailyHidden", DirectContainsText(form, "每日"), false);
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "direct-live-preview.png"));
                    }
                }
            }
        }

        private static void DirectHeaderCases()
        {
            QuotaSnapshot snapshot = DirectArkUsage.Parse(new DirectResponse { Status = 200, Body = DirectFixture });
            snapshot.FetchedAt = new DateTime(2026, 10, 7, 12, 34, 0);
            using (PopupForm form = new PopupForm())
            {
                form.BeginLayoutSession(Screen.PrimaryScreen);
                form.ForceRender(snapshot); form.ShowPanel(); Application.DoEvents();
                Control card = form.ContentControls[0];
                Control title = card.Controls[0];
                Control updated = card.Controls["arkUpdateTime"];
                Button refresh = card.Controls["arkHeaderRefresh"] as Button;
                Check("direct.header.timestamp", updated.Text, "更新 10-07 12:34");
                Check("direct.header.subscriptionTypeReadable", TextRenderer.MeasureText(title.Text, title.Font).Width <= title.Width, true);
                Check("direct.header.singleRow", title.Top == updated.Top && updated.Top == refresh.Top, true);
                Check("direct.header.noOverlap", title.Right <= updated.Left && updated.Right <= refresh.Left, true);
                Check("direct.header.insideCard", card.ClientRectangle.Contains(refresh.Bounds), true);
                int requests = 0;
                form.RefreshRequested += delegate { requests++; };
                refresh.PerformClick();
                Check("direct.header.refreshAction", requests, 1);
                refresh.Focus();
                snapshot.FetchedAt = snapshot.FetchedAt.AddMinutes(1);
                form.ForceRender(snapshot);
                Check("direct.header.timeOnlySameCard", ReferenceEquals(card, form.ContentControls[0]), true);
                Check("direct.header.timeOnlySameLabel", ReferenceEquals(updated, card.Controls["arkUpdateTime"]), true);
                Check("direct.header.updatedInPlace", updated.Text, "更新 10-07 12:35");
                Check("direct.header.focusPreserved", form.ActiveControlForTest == refresh, true);
                form.ForceError(QuotaStatus.NetworkError, "查询失败");
                Check("direct.header.failureKeepsTimestamp", updated.Text, "更新 10-07 12:35");
                Check("direct.header.dailyHidden", DirectContainsText(form, "每日"), false);
                Check("direct.header.dataRetainsDaily", snapshot.Products[0].Periods.Count, 4);
                form.ForceRender(snapshot);
                CheckPeriodGeometry(form, "direct.header.geometry");
                foreach (PeriodQuota period in snapshot.Products[0].Periods)
                    if (period.Label == "daily") period.RemainingPercent = 50;
                form.ForceRender(snapshot);
                Check("direct.header.hiddenDailyChangeSameCard", ReferenceEquals(card, form.ContentControls[0]), true);
                SaveSpacingPreview(form, "preview-detail-header.png");
            }
        }
        private static bool DirectContainsText(Control control, string text)
        {
            if (control.Text.Contains(text)) return true;
            foreach (Control child in control.Controls) if (DirectContainsText(child, text)) return true;
            return false;
        }

        private static void DirectCallbackCases()
        {
            Check("direct.pkce.RFC7636", DirectBrowserLogin.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"),
                "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
            ManualLoginRequest request = null;
            TaskCompletionSource<string> input = null;
            DirectBrowserLogin browser = new DirectBrowserLogin(delegate(ManualLoginRequest value, CancellationToken token) {
                request = value; input = new TaskCompletionSource<string>();
                token.Register(delegate { input.TrySetCanceled(); });
                return input.Task;
            });
            using (CancellationTokenSource cancel = new CancellationTokenSource(10000))
            {
                Task<DirectAuthorization> login = browser.AuthorizeAsync(cancel.Token);
                Dictionary<string, string> query = DirectBrowserLogin.ParseQuery(new Uri(request.LoginUrl).Query);
                Check("direct.callback.s256", query["code_challenge_method"], "S256");
                Uri redirect = new Uri(query["redirect_uri"]);
                Check("direct.callback.loopback", redirect.Host, "127.0.0.1");
                TcpListener probe = new TcpListener(IPAddress.Loopback, redirect.Port);
                try
                {
                    probe.Start();
                    Check("direct.manual.noListener", login.IsCompleted, false);
                }
                finally { probe.Stop(); }
                string code, message;
                Check("direct.manual.rejectState", request.TryAccept(redirect + "?code=test&state=wrong", out code, out message), false);
                Check("direct.manual.wrongStateNotComplete", login.IsCompleted, false);
                Check("direct.manual.accept", request.TryAccept("  " + redirect + "?code=synthetic%2Bcode&state=" + query["state"] + "  ", out code, out message), true);
                input.SetResult(code);
                DirectAuthorization auth = login.GetAwaiter().GetResult();
                Check("direct.manual.codeDecoded", auth.Code, "synthetic+code");
                Check("direct.callback.verifierMatches", DirectBrowserLogin.Challenge(auth.Verifier), query["code_challenge"]);
                string suffix = "?code=test&state=" + query["state"];
                foreach (string invalid in new[] {
                    "https://127.0.0.1:" + redirect.Port + "/oauth/callback" + suffix,
                    "http://localhost:" + redirect.Port + "/oauth/callback" + suffix,
                    "http://127.0.0.1:1/oauth/callback" + suffix,
                    redirect + "/other" + suffix, redirect + suffix + "#fragment",
                    redirect + suffix + "&state=" + query["state"], redirect + suffix + "&code=other",
                    redirect + suffix + "&error=secret-server-error", redirect + "?state=" + query["state"],
                    "synthetic-code", new string('x', 8193) })
                    Check("direct.manual.invalidCallback", request.TryAccept(invalid, out code, out message), false);
            }
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                Task<DirectAuthorization> login = browser.AuthorizeAsync(cancel.Token); cancel.Cancel();
                bool cancelled = false;
                try { login.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Check("direct.callback.cancel", cancelled, true);
            }
            Check("direct.callback.duplicateStateRejected", DirectBrowserLogin.ParseQuery("state=a&state=b").Count, 0);
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                bool prompted = false;
                DirectBrowserLogin cancelledBrowser = new DirectBrowserLogin(delegate(ManualLoginRequest value, CancellationToken token) {
                    prompted = true; return Task.FromResult("unused");
                });
                cancel.Cancel();
                try { cancelledBrowser.AuthorizeAsync(cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                Check("direct.manual.alreadyCancelledDoesNotPrompt", prompted, false);
            }
        }

        private static void ManualLoginWindowCases()
        {
            ManualLoginRequest request = new ManualLoginRequest("https://example.invalid/login", "http://127.0.0.1:54321/oauth/callback", "test-state");
            bool opened = false, retryStayedOpen = false, errorSafe = true;
            bool browserOpenedBeforeDialog = false, dialogVisibleAboveBrowser = false;
            bool openedWhileDialogVisible = false;
            using (CancellationTokenSource cancel = new CancellationTokenSource(5000))
            {
                string code = ManualLoginForm.ShowAsync(request, cancel.Token,
                    delegate(string url) {
                        opened = url == request.LoginUrl;
                        foreach (Form window in Application.OpenForms)
                            if (window is ManualLoginForm && window.Visible) openedWhileDialogVisible = true;
                    }, delegate(ManualLoginForm form) {
                        browserOpenedBeforeDialog = opened && !openedWhileDialogVisible;
                        dialogVisibleAboveBrowser = form.Visible && form.TopMost && form.MinimizeBox;
                        form.CallbackInput.Text = request.RedirectUri + "?code=private-test-code&state=wrong";
                        form.SubmitButton.PerformClick();
                        retryStayedOpen = form.Visible && form.AuthorizationCode == null;
                        foreach (Control control in form.Controls)
                            if (control is Label && control.Text.Contains("private-test-code")) errorSafe = false;
                        using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                            preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manual-login-preview.png"));
                        }
                        form.CallbackInput.Text = request.RedirectUri + "?code=manual-test-code&state=test-state";
                        form.SubmitButton.PerformClick();
                    }).GetAwaiter().GetResult();
                Check("direct.manual.window.opensLoginUrl", opened, true);
                Check("direct.manual.window.browserBeforeDialog", browserOpenedBeforeDialog, true);
                Check("direct.manual.window.visibleTopMostAndMinimizable", dialogVisibleAboveBrowser, true);
                Check("direct.manual.window.invalidAllowsRetry", retryStayedOpen, true);
                Check("direct.manual.window.errorDoesNotEchoCode", errorSafe, true);
                Check("direct.manual.window.submitsCode", code, "manual-test-code");
            }
            using (CancellationTokenSource cancel = new CancellationTokenSource(5000))
            {
                Task<string> dialog = ManualLoginForm.ShowAsync(request, cancel.Token, delegate { },
                    delegate(ManualLoginForm form) { cancel.Cancel(); });
                bool cancelled = false;
                try { dialog.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Check("direct.manual.window.externalCancelCloses", cancelled, true);
            }
            Task<string> closed = ManualLoginForm.ShowAsync(request, CancellationToken.None, delegate { },
                delegate(ManualLoginForm form) { form.Close(); });
            bool userCancelled = false;
            try { closed.GetAwaiter().GetResult(); } catch (OperationCanceledException) { userCancelled = true; }
            Check("direct.manual.window.userCloseCancels", userCancelled, true);
        }
    }
}
