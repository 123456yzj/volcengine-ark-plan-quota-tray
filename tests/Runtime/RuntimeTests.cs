using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static class RuntimeTests
    {
        private const string Auth = "{\"logged_in\":true,\"active_profile\":{\"name\":\"test\",\"type\":\"agent-plan\",\"owner_trn\":\"trn:iam::123:root\",\"region\":\"cn-beijing\",\"project\":\"default\"}}";
        private const string Usage = "{\"viewer\":{\"account_id\":\"123\",\"profile\":\"test\",\"region\":\"cn-beijing\",\"project_name\":\"default\",\"is_root\":true},\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true,\"periods\":[{\"label\":\"5h\",\"percent\":25},{\"label\":\"weekly\",\"used\":20,\"total\":100},{\"label\":\"monthly\",\"percent\":30,\"reset_at\":\"2027-01-01T00:00:00Z\"}]}]}";
        private static string Self { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
        // The runner itself is a synthetic native child executable for transport tests.
        public static int RunChild(string[] args)
        {
            int delay;
            if (int.TryParse(Environment.GetEnvironmentVariable("ARK_LEFT_TEST_DELAY"), out delay)) Thread.Sleep(delay);
            string command = string.Join(" ", args);
            if (command == "--version") { Console.WriteLine("arkcli version 1.0.37"); return 0; }
            if (command == "auth status --format json")
            {
                string gateName = Environment.GetEnvironmentVariable("ARK_LEFT_TEST_GATE");
                if (gateName != null) using (EventWaitHandle gate = EventWaitHandle.OpenExisting(gateName))
                    if (!gate.WaitOne(5000)) return 18;
                string behavior = Environment.GetEnvironmentVariable("ARK_LEFT_TEST_AUTH");
                if (behavior == "exit") return 1;
                if (behavior == "401") return 401;
                if (behavior == "403") return 403;
                if (behavior == "5xx") return 500;
                if (behavior == "crash") return unchecked((int)0xc0000005);
                Console.WriteLine(behavior == "out" ? "{\"logged_in\":false}" : behavior == "format" ? "{}" : Auth);
                return 0;
            }
            if (command == "usage plan --product agent-plan --format json")
            {
                string behavior = Environment.GetEnvironmentVariable("ARK_LEFT_TEST_USAGE");
                if (behavior == "exit") return 1;
                Console.WriteLine(behavior == "mismatch" ? Usage.Replace("\"123\"", "\"456\"")
                    : behavior == "format" ? "{}" : Usage);
                return 0;
            }
            if (command == "auth login volc-sso") return 0;
            return 17; // wrong arguments (including missing --product) must fail
        }

        private sealed class Verifier : IArkCliBinaryVerifier
        {
            public ArkCliRuntimeError Reject;
            public string RejectVersion;
            public int Calls;
            public Task VerifyAsync(string path, string version, string digest, CancellationToken token)
            {
                Calls++; token.ThrowIfCancellationRequested();
                if (!File.Exists(path)) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeMissing);
                if (Reject != ArkCliRuntimeError.None && (RejectVersion == null || RejectVersion == version))
                    throw new ArkCliRuntimeException(Reject);
                return Task.FromResult(0);
            }
        }
        private sealed class Releases : IArkCliReleaseSource
        {
            public string Version = "1.0.38";
            public int Calls;
            public bool Offline;
            public bool Timeout;
            public Task<ArkCliRelease> LatestAsync(string architecture, CancellationToken token)
            {
                Calls++; token.ThrowIfCancellationRequested();
                if (Offline) throw new System.Net.WebException("offline synthetic token=NEVER_LOG_ME");
                if (Timeout) throw new System.Net.WebException("timeout", System.Net.WebExceptionStatus.Timeout);
                return Task.FromResult(new ArkCliRelease { Version = Version, Size = 1, DownloadUrl = "https://api.github.com/repos/volcengine/ark-cli/releases/assets/1" });
            }
        }
        private sealed class Downloader : IArkCliDownloader
        {
            public bool Fail;
            public bool Executable;
            public int Calls;
            public TaskCompletionSource<bool> Block, Entered;
            public async Task DownloadAsync(ArkCliRelease release, string part, CancellationToken token)
            {
                Calls++;
                if (Entered != null) Entered.TrySetResult(true);
                if (Block != null) await Block.Task;
                if (Executable) File.Copy(Self, part); else File.WriteAllText(part, "candidate");
                if (Fail) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeDownloadFailed);
            }
        }
        private static ArkCliRuntimeStore NewStore(string root, string name)
        {
            ArkCliRuntimeStore store = new ArkCliRuntimeStore(Path.Combine(root, name));
            Directory.CreateDirectory(store.Root);
            return store;
        }
        private static void Put(ArkCliRuntimeStore store, string version, bool executable)
        {
            string path = store.ExePath(version); Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (executable) File.Copy(Self, path); else File.WriteAllText(path, "test");
        }
        private static ArkCliRuntimeManager Manager(ArkCliInvoker invoker, ArkCliRuntimeStore store, Verifier verifier,
            Releases releases = null, Downloader downloader = null)
        {
            return new ArkCliRuntimeManager(invoker, store, Path.Combine(store.Root, "no-bootstrap"), "amd64", verifier, releases, downloader);
        }
        private static ArkCliRuntimeError EnsureError(ArkCliRuntimeManager manager)
        {
            try { manager.EnsureRuntimeAsync(CancellationToken.None).GetAwaiter().GetResult(); return ArkCliRuntimeError.None; }
            catch (ArkCliRuntimeException e) { return e.Category; }
        }
        private static void ExpectError(Action<string, object, object> check, string name, Action action, ArkCliRuntimeError error)
        {
            try { action(); check(name, "no error", error); }
            catch (ArkCliRuntimeException e) { check(name, e.Category, error); }
        }
        public static void Run(Action<string, object, object> check)
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-tests-" + Guid.NewGuid().ToString("N"));
            string[] names = { "ARK_LEFT_CLI", "PATH", "ARK_LEFT_RUNTIME_DIR", "ARK_LEFT_TEST_CHILD", "ARK_LEFT_TEST_DELAY", "ARK_LEFT_TEST_AUTH", "ARK_LEFT_TEST_USAGE", "ARK_LEFT_TEST_GATE" };
            string[] previous = new string[names.Length];
            for (int i = 0; i < names.Length; i++) previous[i] = Environment.GetEnvironmentVariable(names[i]);
            Directory.CreateDirectory(root);
            try
            {
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_CHILD", "1");
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_DELAY", null);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", null);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_USAGE", null);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_GATE", null);
                Environment.SetEnvironmentVariable("ARK_LEFT_CLI", null);
                Environment.SetEnvironmentVariable("PATH", "");
                Environment.SetEnvironmentVariable("ARK_LEFT_RUNTIME_DIR", Path.Combine(root, "default"));
                VersionAndRelease(check);
                StoreCases(check, root);
                ResolveCases(check, root);
                BootstrapCases(check, root);
                UpdateCases(check, root);
                TransportAndQuery(check, root);
                PauseCases(check);
                LoginUiCases(check);
                QueryActivationCase(check, root);
            }
            finally
            {
                for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previous[i]);
                try { Directory.Delete(root, true); } catch (Exception) { }
            }
        }
        private static void VersionAndRelease(Action<string, object, object> c)
        {
            c("runtime.semver.numeric", ArkCliVersion.Parse("1.0.10").CompareTo(ArkCliVersion.Parse("1.0.9")) > 0, true);
            foreach (string invalid in new[] { "../1.0.37", "1.0", "1.0.37-beta", "01.0.37", "1.0.37/..", "999999999999.0.1" })
                c("runtime.semver.reject." + invalid, ArkCliVersion.Parse(invalid) == null, true);
            c("runtime.versionOutput", ArkCliVersion.FromOutput("arkcli version 1.0.37\n").ToString(), "1.0.37");
            c("runtime.versionOutputAmbiguous", ArkCliVersion.FromOutput("garbage 1.0.37\n1.0.38") == null, true);
            string json = "{\"tag_name\":\"v1.0.38\",\"draft\":false,\"prerelease\":false,\"assets\":[{\"name\":\"arkcli-1.0.38-windows-amd64.exe\",\"size\":10,\"digest\":null,\"url\":\"https://api.github.com/repos/volcengine/ark-cli/releases/assets/123\",\"browser_download_url\":\"https://github.com/volcengine/ark-cli/releases/download/v1.0.38/arkcli-1.0.38-windows-amd64.exe\"}]}";
            c("runtime.releaseAsset", ArkCliReleaseClient.Parse(json, "amd64").Version, "1.0.38");
            ExpectError(c, "runtime.releaseWrongArchitecture", () => ArkCliReleaseClient.Parse(json, "arm64"), ArkCliRuntimeError.RuntimeUpdateUnavailable);
            ExpectError(c, "runtime.releaseUnknownArchitecture", () => ArkCliReleaseClient.Parse(json, null), ArkCliRuntimeError.RuntimeIncompatible);
            ExpectError(c, "runtime.releaseForeignRepository", () => ArkCliReleaseClient.Parse(json.Replace("volcengine", "attacker"), "amd64"), ArkCliRuntimeError.RuntimeUpdateUnavailable);
            ExpectError(c, "runtime.releasePrerelease", () => ArkCliReleaseClient.Parse(json.Replace("\"prerelease\":false", "\"prerelease\":true"), "amd64"), ArkCliRuntimeError.RuntimeUpdateUnavailable);
            c("runtime.redirectHttps", ArkCliDownloader.AllowedUrl("http://github.com/volcengine/ark-cli"), false);
            c("runtime.redirectForeign", ArkCliDownloader.AllowedUrl("https://github.com.attacker.test/file"), false);
            c("runtime.redirectCredentials", ArkCliDownloader.AllowedUrl("https://secret@github.com/file"), false);
            c("runtime.redirectCdn", ArkCliDownloader.AllowedUrl("https://release-assets.githubusercontent.com/file"), true);
        }
        private static void StoreCases(Action<string, object, object> c, string root)
        {
            ArkCliRuntimeStore store = NewStore(root, "store");
            c("runtime.stateMissing", store.Load().activeVersion, null);
            c("runtime.pathTraversal", store.ExePath("../../outside"), null);
            Put(store, "1.0.37", false); Put(store, "1.0.38", false); Put(store, "1.0.39", false);
            store.Save(new ArkCliRuntimeState { activeVersion = "1.0.37" });
            store.Save(new ArkCliRuntimeState { activeVersion = "1.0.38", previousVersion = "1.0.37", pendingVersion = "1.0.39" });
            c("runtime.atomicActive", store.Load().activeVersion, "1.0.38");
            c("runtime.atomicPrevious", store.Load().previousVersion, "1.0.37");
            c("runtime.atomicPending", store.Load().pendingVersion, "1.0.39");
            c("runtime.atomicNoTemp", Directory.GetFiles(store.Root, "*.tmp").Length, 0);
            File.WriteAllText(store.StatePath, "broken json");
            c("runtime.corruptBackup", store.Load().activeVersion, "1.0.37");
            File.Delete(store.StatePath + ".bak");
            c("runtime.corruptWithoutBackup", store.Load().activeVersion, null);
            store.Save(new ArkCliRuntimeState { activeVersion = "1.0.38", previousVersion = "1.0.37" });
            File.Delete(store.ExePath("1.0.38"));
            c("runtime.missingActiveUsesPrevious", store.Load().activeVersion, "1.0.37");
            store.Save(new ArkCliRuntimeState { activeVersion = "../outside", previousVersion = "1.0.37", pendingVersion = "9.9.9" });
            c("runtime.invalidStatePathUsesPrevious", store.Load().activeVersion, "1.0.37");
            c("runtime.invalidPendingCleared", store.Load().pendingVersion, null);
            c("runtime.stateNoIdentity", File.ReadAllText(store.StatePath).Contains("owner_trn"), false);
            Put(store, "1.0.36", false);
            store.Cleanup(new ArkCliRuntimeState { activeVersion = "1.0.37", previousVersion = "1.0.39" });
            c("runtime.cleanupActive", store.Exists("1.0.37"), true);
            c("runtime.cleanupPrevious", store.Exists("1.0.39"), true);
            c("runtime.cleanupOld", store.Exists("1.0.36"), false);
        }
        private static void ResolveCases(Action<string, object, object> c, string root)
        {
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "resolve");
                ArkCliRuntimeManager manager = Manager(invoker, store, new Verifier());
                c("runtime.allUnavailable", QuotaCli.Resolve(manager).Error, ArkCliRuntimeError.RuntimeMissing);
                string pathDir = Path.Combine(root, "path"); Directory.CreateDirectory(pathDir);
                File.Copy(Self, Path.Combine(pathDir, "arkcli.exe"));
                Environment.SetEnvironmentVariable("PATH", pathDir);
                c("runtime.systemFallback", QuotaCli.Resolve(manager).ExePath, Path.Combine(pathDir, "arkcli.exe"));
                Put(store, "1.0.37", false); store.Save(new ArkCliRuntimeState { activeVersion = "1.0.37" });
                manager = Manager(invoker, store, new Verifier());
                c("runtime.managedBeforePath", QuotaCli.Resolve(manager).ExePath, store.ExePath("1.0.37"));
                Environment.SetEnvironmentVariable("ARK_LEFT_CLI", Self);
                c("runtime.overrideBeforeManaged", QuotaCli.Resolve(manager).ExePath, Self);
                Environment.SetEnvironmentVariable("ARK_LEFT_CLI", Path.Combine(root, "missing.exe"));
                c("runtime.invalidOverrideNoFallback", QuotaCli.Resolve(manager).IsUsable, false);
                Environment.SetEnvironmentVariable("ARK_LEFT_CLI", "relative.exe");
                c("runtime.relativeOverrideRejected", QuotaCli.Resolve(manager).IsUsable, false);
                Environment.SetEnvironmentVariable("ARK_LEFT_CLI", null); Environment.SetEnvironmentVariable("PATH", "");
            }
        }
        private static void BootstrapCases(Action<string, object, object> c, string root)
        {
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-bootstrap");
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "bootstrap");
                ArkCliRuntimeManager manager = new ArkCliRuntimeManager(invoker, store, bundled, "amd64", null);
                c("runtime.officialBootstrapSignature", ArkCliBinaryVerifier.VerifySignature(Path.Combine(bundled, "amd64", "arkcli.exe")), true);
                c("runtime.officialBootstrapNoPath", EnsureError(manager), ArkCliRuntimeError.None);
                c("runtime.bootstrapImported", manager.GetActiveRuntime().ExePath, store.ExePath("1.0.37"));
                c("runtime.bootstrapVersion", manager.State.activeVersion, "1.0.37");
                c("runtime.bootstrapHash", "sha256:" + ArkCliBinaryVerifier.Sha256(store.ExePath("1.0.37")), ArkCliRuntimeConfig.BootstrapDigest("amd64"));
                File.Delete(store.StatePath); ArkCliRuntimeStore.DeleteQuiet(store.StatePath + ".bak");
                manager = new ArkCliRuntimeManager(invoker, store, bundled, "amd64", null);
                c("runtime.bootstrapStateDeleted", EnsureError(manager), ArkCliRuntimeError.None);
                File.WriteAllText(store.StatePath, "{broken"); ArkCliRuntimeStore.DeleteQuiet(store.StatePath + ".bak");
                manager = new ArkCliRuntimeManager(invoker, store, bundled, "amd64", null);
                c("runtime.bootstrapStateCorrupt", EnsureError(manager), ArkCliRuntimeError.None);
                File.WriteAllText(store.ExePath("1.0.37"), "damaged executable");
                manager = new ArkCliRuntimeManager(invoker, store, bundled, "amd64", null);
                c("runtime.bootstrapRepairCorruptActive", EnsureError(manager), ArkCliRuntimeError.None);
                string corrupt = Path.Combine(root, "corrupt-bootstrap", "amd64"); Directory.CreateDirectory(corrupt);
                File.WriteAllText(Path.Combine(corrupt, "arkcli.exe"), "corrupt");
                ArkCliRuntimeStore broken = NewStore(root, "broken-bootstrap");
                manager = new ArkCliRuntimeManager(invoker, broken, Path.GetDirectoryName(corrupt), "amd64", null);
                c("runtime.bootstrapCorrupt", EnsureError(manager), ArkCliRuntimeError.RuntimeIntegrityFailed);
                c("runtime.bootstrapCorruptNoActive", manager.State.activeVersion, null);
                c("runtime.bootstrapCandidateRemoved", Directory.GetFiles(broken.DownloadsDirectory, "*.part").Length, 0);
                manager = Manager(invoker, NewStore(root, "missing-bootstrap"), new Verifier());
                c("runtime.bootstrapMissing", EnsureError(manager), ArkCliRuntimeError.RuntimeMissing);
                ExpectError(c, "runtime.digestMismatch", () => ArkCliBinaryVerifier.VerifyDigest(Self, "sha256:" + new string('0', 64)), ArkCliRuntimeError.RuntimeIntegrityFailed);
                c("runtime.unsignedRejected", ArkCliBinaryVerifier.VerifySignature(Self), false);
                ArkCliBinaryVerifier verifier = new ArkCliBinaryVerifier(invoker);
                ExpectError(c, "runtime.versionMismatch", () => verifier.VerifyAsync(store.ExePath("1.0.37"), "1.0.38", null, CancellationToken.None).GetAwaiter().GetResult(), ArkCliRuntimeError.RuntimeIncompatible);
            }
        }
        private static void UpdateCases(Action<string, object, object> c, string root)
        {
            foreach (string scenario in new[] { "same", "new", "offline", "timeout", "partial", "digest", "signature", "version", "major", "pending", "cancel" })
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "update-" + scenario); Put(store, "1.0.37", false);
                store.Save(new ArkCliRuntimeState { activeVersion = "1.0.37" });
                Verifier verifier = new Verifier(); Releases releases = new Releases(); Downloader downloader = new Downloader();
                ArkCliRuntimeManager manager = Manager(invoker, store, verifier, releases, downloader);
                c("runtime.updateBaseline." + scenario, EnsureError(manager), ArkCliRuntimeError.None);
                if (scenario == "same") releases.Version = "1.0.37";
                if (scenario == "major") releases.Version = "2.0.0";
                if (scenario == "offline") releases.Offline = true;
                if (scenario == "timeout") releases.Timeout = true;
                if (scenario == "partial") downloader.Fail = true;
                if (scenario == "digest") { verifier.Reject = ArkCliRuntimeError.RuntimeIntegrityFailed; verifier.RejectVersion = "1.0.38"; }
                if (scenario == "signature") { verifier.Reject = ArkCliRuntimeError.RuntimeSignatureInvalid; verifier.RejectVersion = "1.0.38"; }
                if (scenario == "version") { verifier.Reject = ArkCliRuntimeError.RuntimeIncompatible; verifier.RejectVersion = "1.0.38"; }
                ArkCliRuntimeLease lease = scenario == "pending" ? manager.AcquireRuntime() : null;
                CancellationTokenSource cancel = new CancellationTokenSource();
                if (scenario == "cancel") cancel.Cancel();
                ArkCliRuntimeError error;
                try { error = manager.CheckForUpdateAsync(cancel.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { error = ArkCliRuntimeError.None; }
                cancel.Dispose();
                ArkCliRuntimeError expected = scenario == "offline" || scenario == "timeout" ? ArkCliRuntimeError.RuntimeUpdateUnavailable
                    : scenario == "partial" ? ArkCliRuntimeError.RuntimeDownloadFailed
                    : scenario == "digest" ? ArkCliRuntimeError.RuntimeIntegrityFailed
                    : scenario == "signature" ? ArkCliRuntimeError.RuntimeSignatureInvalid
                    : scenario == "version" || scenario == "major" ? ArkCliRuntimeError.RuntimeIncompatible : ArkCliRuntimeError.None;
                c("runtime.updateError." + scenario, error, expected);
                c("runtime.updateActive." + scenario, manager.State.activeVersion, scenario == "new" ? "1.0.38" : "1.0.37");
                c("runtime.updateRetainsOld." + scenario, store.Exists("1.0.37"), true);
                c("runtime.updateNoPartial." + scenario, Directory.Exists(store.DownloadsDirectory)
                    ? Directory.GetFiles(store.DownloadsDirectory, "*.part").Length : 0, 0);
                if (lease != null)
                {
                    c("runtime.pendingDuringLease", manager.State.pendingVersion, "1.0.38");
                    c("runtime.leasePinsOldVersion", lease.Runtime.Version, "1.0.37");
                    lease.Dispose();
                    c("runtime.pendingActivatesAfterLease", manager.State.activeVersion, "1.0.38");
                    c("runtime.pendingKeepsPrevious", manager.State.previousVersion, "1.0.37");
                    c("runtime.pendingCleared", manager.State.pendingVersion, null);
                }
                if (scenario != "cancel")
                {
                    int calls = releases.Calls;
                    manager.CheckForUpdateAsync(CancellationToken.None).GetAwaiter().GetResult();
                    c("runtime.update24h." + scenario, releases.Calls, calls);
                }
                string log = Path.Combine(store.Root, "logs", "runtime.log");
                if (File.Exists(log)) c("runtime.logNoUpstreamSecret." + scenario, File.ReadAllText(log).Contains("NEVER_LOG_ME"), false);
                if (scenario == "new")
                {
                    c("runtime.rollbackPrevious", manager.RollbackRuntimeAsync("1.0.38", CancellationToken.None).GetAwaiter().GetResult(), true);
                    c("runtime.rollbackActive", manager.State.activeVersion, "1.0.37");
                    c("runtime.rollbackMissingPrevious", manager.RollbackRuntimeAsync("1.0.37", CancellationToken.None).GetAwaiter().GetResult(), false);
                }
            }
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "nonblocking"); Put(store, "1.0.37", false); store.Save(new ArkCliRuntimeState { activeVersion = "1.0.37" });
                Downloader downloader = new Downloader { Block = new TaskCompletionSource<bool>(), Entered = new TaskCompletionSource<bool>() };
                ArkCliRuntimeManager manager = Manager(invoker, store, new Verifier(), new Releases(), downloader);
                manager.EnsureRuntimeAsync(CancellationToken.None).GetAwaiter().GetResult();
                Task update = manager.CheckForUpdateAsync(CancellationToken.None);
                downloader.Entered.Task.GetAwaiter().GetResult();
                c("runtime.queryNotBlockedByDownload", manager.EnsureRuntimeAsync(CancellationToken.None).IsCompleted, true);
                using (ArkCliRuntimeLease lease = manager.AcquireRuntime()) c("runtime.queryDuringDownload", lease.Runtime.Version, "1.0.37");
                downloader.Block.SetResult(true); update.GetAwaiter().GetResult();
            }
        }
        private static void TransportAndQuery(Action<string, object, object> c, string root)
        {
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                CliResult result = invoker.RunAsync(Self, "--version", 2000, CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.invokerOutput", result.StdOut.Trim(), "arkcli version 1.0.37");
                c("runtime.invokerExit", result.ExitCode, 0);
                c("runtime.invokerEnvNoUpdate", ArkCliInvoker.BuildStartInfo(Self, "--version").EnvironmentVariables["ARKCLI_NO_UPDATE_NOTIFIER"], "1");
                c("runtime.invokerNoCaller", ArkCliInvoker.BuildStartInfo(Self, "--version").EnvironmentVariables.ContainsKey("ARKCLI_CALLER_NAME"), false);
                result = invoker.RunAsync(Path.Combine(root, "no.exe"), "--version", 1000, CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.invokerStartFail", result.Failure, "start-failed");
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_DELAY", "2000");
                result = invoker.RunAsync(Self, "auth login volc-sso", 100, CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.loginTransportTimeout", result.TimedOut, true);
                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    cancel.CancelAfter(100);
                    result = invoker.RunAsync(Self, "auth login volc-sso", 3000, cancel.Token).GetAwaiter().GetResult();
                    c("runtime.loginTransportCancel", result.Cancelled, true);
                }
                c("runtime.invokerIdleAfterCancel", invoker.HasActiveProcesses, false);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_DELAY", null);
            }
            Environment.SetEnvironmentVariable("ARK_LEFT_CLI", Self);
            QuotaCli cli = new QuotaCli(null);
            try
            {
                CliResult login = cli.LoginAsync(CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.loginCommand", login.ExitCode, 0); c("runtime.loginStarted", login.Started, true);
                c("runtime.loginTimeoutConfigured", QuotaCli.LoginTimeoutMs, 600000);
                c("runtime.queryTimeoutPreserved", QuotaCli.DefaultTimeoutMs, 30000);
                QueryOutcome result = cli.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.agentPlanCommandAndAuth", result.Snapshot.Status, QuotaStatus.Ok);
                c("runtime.realTransportScope", result.Verdict, ScopeVerdict.Same);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", "out");
                c("runtime.notLoggedInNoAutomaticLogin", cli.QueryAsync(CancellationToken.None).GetAwaiter().GetResult().Status, QuotaStatus.NotLoggedIn);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", "format");
                c("runtime.authFormat", cli.QueryAsync(CancellationToken.None).GetAwaiter().GetResult().Status, QuotaStatus.FormatError);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", "exit");
                c("runtime.authFailure", cli.QueryAsync(CancellationToken.None).GetAwaiter().GetResult().Status, QuotaStatus.Failed);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", null); Environment.SetEnvironmentVariable("ARK_LEFT_TEST_USAGE", "exit");
                c("runtime.usageFailure", cli.QueryAsync(CancellationToken.None).GetAwaiter().GetResult().Status, QuotaStatus.Failed);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_USAGE", "mismatch");
                c("runtime.scopeMismatch", cli.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult().Verdict, ScopeVerdict.Mismatch);
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_USAGE", "format");
                c("runtime.usageFormat", cli.QueryAsync(CancellationToken.None).GetAwaiter().GetResult().Status, QuotaStatus.FormatError);
            }
            finally { cli.Dispose(); Environment.SetEnvironmentVariable("ARK_LEFT_CLI", null); Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", null); Environment.SetEnvironmentVariable("ARK_LEFT_TEST_USAGE", null); }
            // A managed start failure must retry the entire auth->usage chain on previous.
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "query-rollback"); Put(store, "1.0.38", false); Put(store, "1.0.37", true);
                store.Save(new ArkCliRuntimeState { activeVersion = "1.0.38", previousVersion = "1.0.37" });
                ArkCliRuntimeManager manager = Manager(invoker, store, new Verifier());
                cli = new QuotaCli(null, invoker, manager);
                QueryOutcome result = cli.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.queryAutomaticRollback", manager.State.activeVersion, "1.0.37");
                c("runtime.queryRetryAfterRollback", result.Snapshot.Status, QuotaStatus.Ok);
                c("runtime.queryRollbackScope", result.Verdict, ScopeVerdict.Same);
                foreach (string behavior in new[] { "out", "401", "403", "5xx", "exit", "format" })
                {
                    Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", behavior);
                    cli.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                    c("runtime.businessErrorNoRollback." + behavior, manager.State.activeVersion, "1.0.37");
                }
                Environment.SetEnvironmentVariable("ARK_LEFT_TEST_AUTH", null);
                cli.Dispose();
            }
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "login-rollback"); Put(store, "1.0.38", false); Put(store, "1.0.37", true);
                store.Save(new ArkCliRuntimeState { activeVersion = "1.0.38", previousVersion = "1.0.37" });
                ArkCliRuntimeManager manager = Manager(invoker, store, new Verifier());
                cli = new QuotaCli(null, invoker, manager);
                CliResult login = cli.LoginAsync(CancellationToken.None).GetAwaiter().GetResult();
                c("runtime.loginAutomaticRollback", manager.State.activeVersion, "1.0.37");
                c("runtime.loginRetryAfterRollback", login.ExitCode, 0);
                c("runtime.loginRetryStarted", login.Started, true);
                cli.Dispose();
            }
        }
        private static void PauseCases(Action<string, object, object> c)
        {
            SynchronizationContext before = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try { PauseCasesCore(c); }
            finally { SynchronizationContext.SetSynchronizationContext(before); }
        }
        private static void PauseCasesCore(Action<string, object, object> c)
        {
            int queries = 0, commits = 0;
            TaskCompletionSource<QueryOutcome> completion = new TaskCompletionSource<QueryOutcome>();
            bool cancelled = false;
            using (SnapshotController controller = new SnapshotController(new PanelModel(), delegate(IProgress<QueryProgress> p, CancellationToken token) {
                queries++; token.Register(delegate { cancelled = true; }); return completion.Task;
            }, delegate { commits++; }, delegate { }, delegate { return null; }, delegate { return true; }, delegate { }))
            {
                Task query = controller.Refresh(); Task pause = controller.PauseAsync();
                c("runtime.switchCancelsQuery", cancelled, true);
                controller.Poll().GetAwaiter().GetResult(); c("runtime.switchBlocksPoll", queries, 1);
                completion.SetResult(new QueryOutcome { Snapshot = new QuotaSnapshot { Status = QuotaStatus.Ok } });
                pause.GetAwaiter().GetResult(); query.GetAwaiter().GetResult();
                c("runtime.switchLateResultIgnored", commits, 1); // initial empty commit only
                controller.Resume(); controller.Refresh().GetAwaiter().GetResult();
                c("runtime.switchResumeQueries", queries, 2);
            }
        }
        private static System.Windows.Forms.Button FindButton(System.Windows.Forms.Control control, string text)
        {
            System.Windows.Forms.Button button = control as System.Windows.Forms.Button;
            if (button != null && button.Text == text) return button;
            foreach (System.Windows.Forms.Control child in control.Controls)
            {
                button = FindButton(child, text);
                if (button != null) return button;
            }
            return null;
        }
        private static void PumpUntil(Task task)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
            if (!task.IsCompleted) throw new TimeoutException("synthetic UI login did not finish");
            task.GetAwaiter().GetResult();
        }
        private static void LoginUiCases(Action<string, object, object> c)
        {
            using (PopupForm popup = new PopupForm())
            {
                int clicked = 0;
                popup.LoginRequested += delegate { clicked++; };
                popup.ApplyModelView(new PanelView { State = PanelState.Error, Message = "未登录", AllowCopyLogin = true });
                popup.ShowPanel();
                System.Windows.Forms.Button button = FindButton(popup, "登录方舟");
                c("runtime.loginButtonRendered", button != null, true);
                if (button != null) button.PerformClick();
                c("runtime.loginButtonRoutesEvent", clicked, 1);
                popup.ApplyModelView(new PanelView { State = PanelState.Error, Message = "未登录", AllowCopyLogin = false });
                c("runtime.loginButtonRemovesOnStateChange", FindButton(popup, "登录方舟") == null, true);
                popup.HidePanel();
            }
            foreach (string behavior in new[] { "success", "cancel", "timeout", "browser-fail" })
            {
                int queries = 0;
                bool queryCancelled = false;
                TaskCompletionSource<QueryOutcome> oldQuery = new TaskCompletionSource<QueryOutcome>();
                TaskCompletionSource<CliResult> loginResult = new TaskCompletionSource<CliResult>();
                using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress, CancellationToken token) {
                    queries++;
                    if (queries == 1)
                    {
                        token.Register(delegate { queryCancelled = true; oldQuery.TrySetResult(new QueryOutcome {
                            Snapshot = new QuotaSnapshot { Status = QuotaStatus.Ok }, AuthConfirmed = true }); });
                        return oldQuery.Task;
                    }
                    bool logged; AuthIdentity identity;
                    IdentityParse.TryParseAuth(Auth, out logged, out identity);
                    return Task.FromResult(new QueryOutcome { AuthConfirmed = true, Identity = identity,
                        AuthScope = QueryScope.FromAuth(identity), Verdict = ScopeVerdict.Same, Snapshot = QuotaParser.Parse(Usage) });
                }, null, delegate(CancellationToken token) {
                    token.Register(delegate { loginResult.TrySetResult(new CliResult { Cancelled = true }); });
                    return loginResult.Task;
                }))
                {
                    app.ApplyViewForTest(SyntheticSample.Build());
                    Task refreshing = app.RefreshForTest();
                    Task login = app.LoginForTest();
                    PumpUntil(refreshing);
                    System.Windows.Forms.Application.DoEvents();
                    c("runtime.uiSwitchCancels." + behavior, queryCancelled, true);
                    c("runtime.uiSwitchClearsOld." + behavior, app.DetailsFormForTest.Model.Last == null, true);
                    c("runtime.uiSwitchState." + behavior, app.DetailsFormForTest.Model.CurrentView.State, PanelState.ConfirmingIdentity);
                    app.RefreshForTest().GetAwaiter().GetResult();
                    c("runtime.uiSwitchBlocksQuery." + behavior, queries, 1);
                    c("runtime.uiSwitchCancelMenu." + behavior, app.MenuTextForTest(0), "取消登录");
                    if (behavior == "cancel") PumpUntil(app.LoginForTest());
                    else loginResult.SetResult(new CliResult { Started = true, TimedOut = behavior == "timeout",
                        ExitCode = behavior == "browser-fail" ? 1 : 0 });
                    PumpUntil(login);
                    c("runtime.uiLoginRequeries." + behavior, queries, behavior == "success" ? 2 : 1);
                    c("runtime.uiLoginData." + behavior, app.DetailsFormForTest.Model.Last != null, behavior == "success");
                    c("runtime.uiLoginModelState." + behavior, app.DetailsFormForTest.Model.CurrentView.State,
                        behavior == "success" ? PanelState.ShowingCurrent : PanelState.Error);
                    c("runtime.uiLoginMenuRestored." + behavior, app.MenuTextForTest(0), "登录方舟 / 重新登录 / 切换账号");
                }
            }
            int evicted = 0;
            PanelModel model = new PanelModel();
            model.EvictPersistent = delegate { evicted++; };
            model.ShowSnapshot(new QuotaSnapshot { Status = QuotaStatus.Ok }, "old-scope");
            model.BeginIdentityChange();
            c("runtime.switchEvictsDisk", evicted, 1);
            c("runtime.switchClearsFingerprint", model.ConfirmedFingerprint, null);
        }
        private static void QueryActivationCase(Action<string, object, object> c, string root)
        {
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                ArkCliRuntimeStore store = NewStore(root, "live-query-activation"); Put(store, "1.0.37", true);
                store.Save(new ArkCliRuntimeState { activeVersion = "1.0.37" });
                ArkCliRuntimeManager manager = Manager(invoker, store, new Verifier(), new Releases(), new Downloader { Executable = true });
                QuotaCli cli = new QuotaCli(null, invoker, manager);
                try
                {
                    // Pass the actual named handle name to the child; release
                    // authentication only after the update has reached pending.
                    string gateName = "Local\\ArkLeftRuntimeQuerySignal_" + Guid.NewGuid().ToString("N");
                    using (EventWaitHandle signal = new EventWaitHandle(false, EventResetMode.ManualReset, gateName))
                    {
                        Environment.SetEnvironmentVariable("ARK_LEFT_TEST_GATE", gateName);
                        Task<QueryOutcome> query = cli.QueryDetailedAsync(null, CancellationToken.None);
                        c("runtime.activeQueryChildStarted", SpinWait.SpinUntil(() => invoker.HasActiveProcesses, 3000), true);
                        manager.CheckForUpdateAsync(CancellationToken.None).GetAwaiter().GetResult();
                        c("runtime.activeQueryKeepsActive", manager.State.activeVersion, "1.0.37");
                        c("runtime.activeQueryPending", manager.State.pendingVersion, "1.0.38");
                        signal.Set();
                        QueryOutcome outcome = query.GetAwaiter().GetResult();
                        string originalFingerprint = outcome.AuthScope.Fingerprint;
                        c("runtime.activeQueryPinnedResult", outcome.RuntimeVersion, "1.0.37");
                        c("runtime.activeQueryScopeSafe", outcome.Verdict, ScopeVerdict.Same);
                        c("runtime.activeQueryActivatesAfterCompletion", manager.State.activeVersion, "1.0.38");
                        Environment.SetEnvironmentVariable("ARK_LEFT_TEST_GATE", null);
                        outcome = cli.QueryDetailedAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                        c("runtime.nextQueryUsesNewRuntime", outcome.RuntimeVersion, "1.0.38");
                        c("runtime.versionChangeKeepsBusinessScope", outcome.AuthScope.Fingerprint, originalFingerprint);
                    }
                }
                finally { Environment.SetEnvironmentVariable("ARK_LEFT_TEST_GATE", null); cli.Dispose(); }
            }
        }
    }
}
