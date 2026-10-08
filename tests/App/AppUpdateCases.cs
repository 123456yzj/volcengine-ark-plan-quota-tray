using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private static string AppReleaseFixture(string tag)
        {
            string version = tag.TrimStart('v');
            string name = "ark_left-" + version + "-windows-setup.exe";
            return new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "tag_name", tag }, { "draft", false }, { "prerelease", false },
                { "html_url", AppUpdateClient.RepositoryUrl + "/releases/tag/" + tag },
                { "assets", new[] { new Dictionary<string, object> { { "name", name },
                    { "state", "uploaded" }, { "browser_download_url", AppUpdateClient.RepositoryUrl
                        + "/releases/download/" + tag + "/" + name } } } } });
        }

        private static void AppUpdateCases()
        {
            Version current = new Version(0, 22, 0, 0);
            AppUpdateResult newer = AppUpdateClient.Parse(AppReleaseFixture("v0.23.0"), current);
            Check("update.newer", newer.Status, AppUpdateStatus.Available);
            Check("update.versionNormalized", newer.Version, new Version(0, 23, 0, 0));
            Check("update.same", AppUpdateClient.Parse(AppReleaseFixture("v0.22.0"), current).Status, AppUpdateStatus.Current);
            Check("update.older", AppUpdateClient.Parse(AppReleaseFixture("v0.21.1"), current).Status, AppUpdateStatus.Current);
            Check("update.numericNotLexical", AppUpdateClient.Parse(AppReleaseFixture("v0.100.0"), current).Status, AppUpdateStatus.Available);
            Check("update.numericPatch", AppUpdateClient.Parse(AppReleaseFixture("v0.21.10"), new Version(0, 21, 9)).Status, AppUpdateStatus.Available);
            Check("update.revision", AppUpdateClient.Parse(AppReleaseFixture("v0.22.0.1"), current).Status, AppUpdateStatus.Available);
            Check("update.display", AppUpdateClient.DisplayVersion(current), "0.22.0");
            Check("update.displayRevision", AppUpdateClient.DisplayVersion(new Version(0, 22, 0, 1)), "0.22.0.1");

            string good = AppReleaseFixture("v0.23.0");
            foreach (string invalid in new[] {
                "not-json", "{}", good.Replace("\"draft\":false", "\"draft\":true"),
                good.Replace("\"prerelease\":false", "\"prerelease\":true"),
                good.Replace("\"prerelease\":false", "\"prerelease\":\"false\""),
                good.Replace("v0.23.0", "v0.23.0-beta"), good.Replace("v0.23.0", "v0.23"),
                good.Replace("v0.23.0", "v999999999999.23.0"),
                good.Replace(AppUpdateClient.RepositoryUrl, "https://example.invalid/repo"),
                good.Replace("\"uploaded\"", "\"new\""),
                good.Replace("\"assets\"", "\"missing_assets\""),
                good.Replace("windows-setup.exe", "linux.tar.gz"),
                good.Replace("/releases/download/", "/other/download/") })
            {
                bool rejected = false;
                try { AppUpdateClient.Parse(invalid, current); }
                catch (Exception) { rejected = true; }
                Check("update.rejectInvalidMetadata", rejected, true);
            }
            AppUpdateClient client = new AppUpdateClient(current, delegate(CancellationToken token) { return Task.FromResult(good); });
            Check("update.fetchSuccess", client.CheckAsync(CancellationToken.None).Result.Status, AppUpdateStatus.Available);
            client = new AppUpdateClient(current, delegate(CancellationToken token) { throw new Exception("raw sensitive upstream error"); });
            Check("update.networkFailure", client.CheckAsync(CancellationToken.None).Result.Status, AppUpdateStatus.Failed);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                int calls = 0;
                client = new AppUpdateClient(current, delegate(CancellationToken token) { calls++; return Task.FromResult(good); });
                cancellation.Cancel();
                Check("update.cancelled", client.CheckAsync(cancellation.Token).Result.Status, AppUpdateStatus.Cancelled);
                Check("update.cancelledNoRequest", calls, 0);
            }
            AppUpdateTrayCases(newer);
            AppUpdateDialogCases(newer);
            AppUpdatePendingDialogCases(newer);
        }

        private static void AppUpdateTrayCases(AppUpdateResult newer)
        {
            int requests = 0, queries = 0, opened = 0;
            string openedUrl = null;
            TaskCompletionSource<AppUpdateResult> pending = new TaskCompletionSource<AppUpdateResult>();
            Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> query = delegate {
                queries++; return Task.FromResult(new QueryOutcome());
            };
            using (TrayApp app = new TrayApp(query, null, null, null, delegate(CancellationToken token) {
                requests++; return pending.Task;
            }, delegate(string url) { opened++; openedUrl = url; }))
            {
                app.OpenAppReleaseForTest();
                Check("update.noUnverifiedBrowser", opened, 0);
                Task check = app.CheckAppUpdateForTest(false);
                Check("update.inFlight", app.AppUpdateCheckingForTest, true);
                app.CheckAppUpdateForTest(true).GetAwaiter().GetResult();
                Check("update.singleFlight", requests, 1);
                pending.SetResult(newer);
                Check("update.completes", DirectPump(check), true);
                Check("update.availableCommitted", app.AppUpdateResultForTest.Status, AppUpdateStatus.Available);
                Check("update.backgroundNotifies", app.AppUpdateNotifyCountForTest, 1);
                app.CheckAppUpdateForTest(false).GetAwaiter().GetResult();
                Check("update.sameVersionNotNotifiedTwice", app.AppUpdateNotifyCountForTest, 1);
                app.OpenAppReleaseForTest();
                Check("update.explicitBrowser", opened, 1);
                Check("update.exactReleasePage", openedUrl, newer.ReleaseUrl);
                Check("update.zeroQuotaQuery", queries, 0);
                Check("update.noWindowForcedOpen", app.FloatingVisibleForTest || app.DetailsVisibleForTest, false);
            }
            pending = new TaskCompletionSource<AppUpdateResult>();
            TrayApp exiting = new TrayApp(query, null, null, null, delegate(CancellationToken token) {
                token.Register(delegate { pending.TrySetCanceled(); }); return pending.Task;
            });
            Task cancelled = exiting.CheckAppUpdateForTest(false);
            exiting.Dispose();
            Check("update.exitCancelsRequest", DirectPump(cancelled), true);
            Check("update.exitRejectsResult", exiting.AppUpdateResultForTest == null, true);
            Check("update.exitNoNotification", exiting.AppUpdateNotifyCountForTest, 0);
            pending = new TaskCompletionSource<AppUpdateResult>();
            exiting = new TrayApp(query, null, null, null, delegate { return pending.Task; });
            Task late = exiting.CheckAppUpdateForTest(false);
            exiting.Dispose();
            pending.SetResult(newer);
            Check("update.lateSuccessCompletes", DirectPump(late), true);
            Check("update.lateSuccessIgnored", exiting.AppUpdateResultForTest == null, true);
            Check("update.lateSuccessNoNotification", exiting.AppUpdateNotifyCountForTest, 0);
        }

        private static void AppUpdateDialogCases(AppUpdateResult newer)
        {
            int checks = 0, opens = 0;
            bool browserFailure = false;
            AppUpdateResult result = newer;
            using (TrayApp app = new TrayApp(delegate { return Task.FromResult(new QueryOutcome()); },
                null, null, null, delegate { checks++; return Task.FromResult(result); }, delegate {
                    opens++; if (browserFailure) throw new Exception("upstream browser error");
                }))
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 25 })
            {
                bool reached = false;
                timer.Tick += delegate {
                    AppUpdateForm form = app.AboutDialogForTest;
                    if (form == null || !form.Visible) return;
                    timer.Stop(); reached = true;
                    Check("update.dialogShowsAvailable", form.StatusForTest.Contains("0.23.0"), true);
                    Check("update.dialogDownloadEnabled", form.DownloadButton.Enabled, true);
                    bool runtimeEntry = false;
                    foreach (Control control in form.Controls)
                        if (control.Text.Contains("ArkCLI")) runtimeEntry = true;
                    Check("update.noRuntimeEntry", runtimeEntry, false);
                    form.DownloadButton.PerformClick();
                    browserFailure = true;
                    form.DownloadButton.PerformClick();
                    Check("update.browserFailureRecoverable", form.StatusForTest.Contains("无法打开浏览器") && form.DownloadButton.Enabled, true);
                    browserFailure = false;
                    form.DownloadButton.PerformClick();
                    result = new AppUpdateResult { Status = AppUpdateStatus.Failed };
                    form.CheckButton.PerformClick();
                    Check("update.dialogFailureRecoverable", form.CheckButton.Enabled && form.StatusForTest.Contains("重试"), true);
                    Check("update.dialogFailureNoRawError", form.StatusForTest.Contains("raw"), false);
                    Check("update.dialogFailureDisablesDownload", form.DownloadButton.Enabled, false);
                    result = new AppUpdateResult { Status = AppUpdateStatus.Current, Version = new Version(0, 22, 0) };
                    form.CheckButton.PerformClick();
                    Check("update.dialogCurrent", form.StatusForTest.Contains("最新版本"), true);
                    Check("update.dialogCurrentNoDownload", form.DownloadButton.Enabled, false);
                    Check("update.manualNoBalloon", app.AppUpdateNotifyCountForTest, 0);
                    form.Close();
                };
                timer.Start();
                app.PerformMenuForTest(2);
                Check("update.aboutMenuUsesAppDialog", reached, true);
                Check("update.dialogChecks", checks, 3);
                Check("update.dialogBrowserOnlyOnClick", opens, 3);
                Check("update.dialogReleased", app.AboutDialogForTest == null, true);
            }
        }

        private static void AppUpdatePendingDialogCases(AppUpdateResult newer)
        {
            TaskCompletionSource<AppUpdateResult> pending = new TaskCompletionSource<AppUpdateResult>();
            using (TrayApp app = new TrayApp(delegate { return Task.FromResult(new QueryOutcome()); },
                null, null, null, delegate { return pending.Task; }))
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 25 })
            {
                Task check = app.CheckAppUpdateForTest(false);
                timer.Tick += delegate {
                    AppUpdateForm form = app.AboutDialogForTest;
                    if (form == null || !form.Visible) return;
                    timer.Stop();
                    Check("update.pendingDialogChecking", form.StatusForTest.Contains("正在检查"), true);
                    Check("update.pendingDialogButtonsDisabled", !form.CheckButton.Enabled && !form.DownloadButton.Enabled, true);
                    form.Close();
                };
                timer.Start();
                app.PerformMenuForTest(2);
                Check("update.closeDialogKeepsAppAlive", app.AppUpdateCheckingForTest && app.AboutDialogForTest == null, true);
                pending.SetResult(newer);
                Check("update.resultAfterDialogClosed", DirectPump(check), true);
                Check("update.resultRetainedForReopen", app.AppUpdateResultForTest.Status, AppUpdateStatus.Available);
            }
        }

        private static void AppUpdateLiveCase()
        {
            AppUpdateResult result = new AppUpdateClient(new Version(0, 21, 0, 0)).CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check("update.liveValidStableRelease", result.Status, AppUpdateStatus.Available);
            if (result.Version != null) Console.WriteLine("github_latest=" + AppUpdateClient.DisplayVersion(result.Version)
                + " status=" + result.Status + " release=" + result.ReleaseUrl);
        }
    }
}
