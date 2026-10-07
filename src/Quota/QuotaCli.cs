using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    public enum QueryStage { Auth, Usage, Done }

    // Progress reported to the UI. Slow=true is emitted once after the slow
    // threshold elapses while a query is still running.
    public class QueryProgress
    {
        public QueryStage Stage;
        public bool Slow;
        public bool AuthConfirmed;        // explicit: auth gate returned logged_in=true
        public AuthIdentity AuthIdentity; // may be null when active_profile is absent
    }

    // Rich query result so the UI can reason about identity/scope without the
    // CLI layer knowing anything about the UI. QueryAsync(CancellationToken)
    // remains as a thin compatibility overload returning just the snapshot.
    public class QueryOutcome
    {
        public QuotaSnapshot Snapshot;
        public bool AuthConfirmed;      // auth reached logged_in=true
        public QueryStage FailedStage;  // set when a stage returned a terminal failure
        public AuthIdentity Identity;   // parsed active_profile (may be null)
        public QueryScope AuthScope;    // scope derived from auth (may be unknown)
        public ViewerIdentity Viewer;   // parsed usage viewer (may be null)
        public ScopeVerdict Verdict;    // auth-scope vs usage-viewer check
        public string RuntimeVersion;
        public ArkCliRuntimeError RuntimeError;
    }

    // Immutable mode for tests / diagnostics. When Null, normal real behavior.
    // The test transport returns CliResult objects that flow through the SAME
    // decision logic as the real path (see QueryAsync), so it cannot mask bugs.
    public class CliMode
    {
        public string AuthJson;       // returned by `auth status --format json`
        public string UsageJson;      // returned by `usage plan --format json`
        public int? AuthExitCode;     // force exit code for auth
        public int? UsageExitCode;    // force exit code for usage
        public int DelayMs;           // simulated latency (for timeout/cancel tests)

        public bool IsTest { get { return AuthJson != null || UsageJson != null || AuthExitCode.HasValue || UsageExitCode.HasValue || DelayMs > 0; } }
    }

    public class CliResolveResult
    {
        public bool Found;
        public string ExePath;      // native .exe, when found
        public string Message;
        public ArkCliRuntimeInfo Runtime;
        public ArkCliRuntimeError Error;

        public bool IsUsable { get { return Found && ExePath != null; } }
    }

    public class CliResult
    {
        public bool Started;
        public int ExitCode;
        public string StdOut;
        public string StdErr;
        public bool TimedOut;
        public bool Cancelled;

        // Short machine reason when the invocation itself failed:
        //   "no-cli" / "start-failed" / "process-error" / "disposed"
        public string Failure;
        public ArkCliRuntimeError RuntimeError;
    }

    // Locates and runs the local ArkCLI native executable without ever shelling
    // through unescaped strings. Credentials are never read or written here.
    //
    // Only a native arkcli*.exe is supported (no PowerShell .ps1 fallback), which
    // keeps the invocation simple and avoids shim grandchild processes holding
    // the stdout/stderr pipes open past our timeout.
    public class QuotaCli
    {
        public const int DefaultTimeoutMs = 30000;
        public const int LoginTimeoutMs = 600000;
        private readonly CliMode _mode;
        private readonly ArkCliInvoker _invoker;
        private readonly ArkCliRuntimeManager _runtime;
        private volatile bool _disposed;

        public QuotaCli(CliMode mode) : this(mode, new ArkCliInvoker(), null) { }

        internal QuotaCli(CliMode mode, ArkCliInvoker invoker, ArkCliRuntimeManager runtime)
        {
            _mode = mode;
            _invoker = invoker;
            _runtime = runtime ?? new ArkCliRuntimeManager(_invoker);
        }

        public ArkCliRuntimeManager RuntimeManager { get { return _runtime; } }

        // Explicit user operation. Never called from QueryDetailedAsync.
        public async Task<CliResult> LoginAsync(CancellationToken token)
        {
            if (_disposed) return new CliResult { Failure = "disposed" };
            await EnsureManagedAsync(token).ConfigureAwait(false);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                CliResolveResult resolved;
                CliResult result;
                using (ArkCliRuntimeLease lease = _runtime.AcquireRuntime())
                {
                    if (token.IsCancellationRequested) return new CliResult { Cancelled = true };
                    resolved = Resolve(_runtime);
                    if (!resolved.IsUsable) return new CliResult { Failure = "no-cli", RuntimeError = resolved.Error };
                    if (resolved.Runtime != null && resolved.Runtime.IsManaged && lease != null)
                    { resolved.Runtime = lease.Runtime; resolved.ExePath = lease.Runtime.ExePath; }
                    result = await _invoker.RunAsync(resolved.ExePath, "auth login volc-sso", LoginTimeoutMs, token).ConfigureAwait(false);
                    ClassifyRuntimeResult(resolved, result);
                }
                if (attempt != 0 || result.RuntimeError != ArkCliRuntimeError.RuntimeStartFailed || token.IsCancellationRequested
                    || !await _runtime.RollbackRuntimeAsync(resolved.Runtime.Version, token).ConfigureAwait(false)) return result;
            }
            throw new InvalidOperationException();
        }

        // Slow-response threshold. Reported once via IProgress while a query is
        // still running; it never triggers a network call by itself.
        public const int SlowThresholdMs = 8000;

        private volatile QueryStage _stage = QueryStage.Auth;

        // Compatibility entry point: same decision chain, returns only the snapshot.
        public async Task<QuotaSnapshot> QueryAsync(CancellationToken token)
        {
            QueryOutcome outcome = await QueryDetailedAsync(null, token);
            return outcome.Snapshot;
        }

        // Single detailed entry point. Test mode is just a different transport,
        // but the authentication gate, scope derivation and result judgement are
        // shared by both. IProgress reports the auth stage twice: once on entry
        // and again right after auth succeeds (with the parsed identity), so the
        // UI can transition from "confirming identity" to "querying" without a
        // separate callback channel.
        public async Task<QueryOutcome> QueryDetailedAsync(IProgress<QueryProgress> progress, CancellationToken token)
        {
            QueryOutcome outcome = await QueryCoreAsync(progress, token).ConfigureAwait(false);
            if ((outcome.RuntimeError == ArkCliRuntimeError.RuntimeStartFailed || outcome.RuntimeError == ArkCliRuntimeError.RuntimeIncompatible)
                && outcome.RuntimeVersion != null && !token.IsCancellationRequested
                && await _runtime.RollbackRuntimeAsync(outcome.RuntimeVersion, token).ConfigureAwait(false))
                return await QueryCoreAsync(progress, token).ConfigureAwait(false);
            return outcome;
        }

        private async Task<QueryOutcome> QueryCoreAsync(IProgress<QueryProgress> progress, CancellationToken token)
        {
            QueryOutcome outcome = new QueryOutcome();
            CancellationTokenSource slowCts = new CancellationTokenSource();
            Task slowTask = WatchSlowAsync(progress, slowCts.Token);
            ArkCliRuntimeLease lease = null;
            CliResolveResult resolved = null;
            try
            {
                if (_mode == null || !_mode.IsTest)
                {
                    await EnsureManagedAsync(token).ConfigureAwait(false);
                    resolved = Resolve(_runtime);
                    outcome.RuntimeVersion = resolved.Runtime == null ? null : resolved.Runtime.Version;
                    if (resolved.Runtime != null && resolved.Runtime.IsManaged)
                    {
                        lease = _runtime.AcquireRuntime();
                        if (lease != null)
                        {
                            resolved.Runtime = lease.Runtime; resolved.ExePath = lease.Runtime.ExePath;
                            outcome.RuntimeVersion = lease.Runtime.Version;
                        }
                    }
                }
                // 1) authentication gate
                Report(progress, QueryStage.Auth, false, false, null);
                _stage = QueryStage.Auth;
                CliResult auth = await RunResolvedAsync(resolved, "auth status --format json", token);
                outcome.RuntimeError = auth.RuntimeError;
                QuotaSnapshot authVerdict = JudgeAuth(auth, outcome);
                if (authVerdict != null)
                {
                    outcome.Snapshot = authVerdict;
                    outcome.FailedStage = QueryStage.Auth;
                    return outcome;
                }

                // Signal the confirmed auth identity before running usage. This
                // fires for EVERY successful auth, even when active_profile is
                // absent (identity null) — AuthConfirmed is the explicit flag.
                ReportAuth(progress, outcome.Identity);

                // 2) quota query (only reached when logged_in=true)
                Report(progress, QueryStage.Usage, false, false, null);
                _stage = QueryStage.Usage;
                CliResult usage = await RunResolvedAsync(resolved, "usage plan --product agent-plan --format json", token);
                outcome.RuntimeError = usage.RuntimeError;
                outcome.Snapshot = JudgeUsage(usage, outcome);
                if (outcome.Snapshot != null && IsTerminalFailure(outcome.Snapshot.Status))
                    outcome.FailedStage = QueryStage.Usage;
                return outcome;
            }
            finally
            {
                if (lease != null) lease.Dispose();
                slowCts.Cancel();
                slowCts.Dispose();
                try { slowTask.Wait(50); } catch (Exception) { }
                Report(progress, QueryStage.Done, false, false, null);
            }
        }

        private static void Report(IProgress<QueryProgress> p, QueryStage stage, bool slow,
            bool authConfirmed, AuthIdentity identity)
        {
            if (p == null) return;
            try
            {
                QueryProgress q = new QueryProgress();
                q.Stage = stage;
                q.Slow = slow;
                q.AuthConfirmed = authConfirmed;
                q.AuthIdentity = identity;
                p.Report(q);
            }
            catch (Exception) { }
        }

        private static void ReportAuth(IProgress<QueryProgress> p, AuthIdentity identity)
        {
            if (p == null) return;
            try
            {
                QueryProgress q = new QueryProgress();
                q.Stage = QueryStage.Auth;
                q.AuthConfirmed = true;
                q.AuthIdentity = identity;
                p.Report(q);
            }
            catch (Exception) { }
        }

        private async Task WatchSlowAsync(IProgress<QueryProgress> p, CancellationToken token)
        {
            try { await Task.Delay(SlowThresholdMs, token); }
            catch (Exception) { return; }
            Report(p, _stage, true, false, null);
        }

        // Returns null when authentication succeeded and the caller may proceed.
        // On success fills outcome.Identity / AuthScope.
        private static QuotaSnapshot JudgeAuth(CliResult r, QueryOutcome outcome)
        {
            QuotaSnapshot terminal = JudgeInvocation(r, "查询 ArkCLI 登录状态超时。", "查询 ArkCLI 登录状态失败。");
            if (terminal != null) return terminal;

            bool loggedIn;
            AuthIdentity identity;
            if (!IdentityParse.TryParseAuth(r.StdOut, out loggedIn, out identity))
            {
                return Fail(QuotaStatus.FormatError, "无法确认 ArkCLI 登录状态（返回缺少有效字段）。");
            }
            if (!loggedIn)
            {
                return Fail(QuotaStatus.NotLoggedIn,
                    "尚未登录方舟账号，请点击“登录方舟”。");
            }
            outcome.AuthConfirmed = true;
            outcome.Identity = identity;
            outcome.AuthScope = QueryScope.FromAuth(identity);
            outcome.Verdict = ScopeVerdict.Unknown;
            return null;
        }

        private static QuotaSnapshot JudgeUsage(CliResult r, QueryOutcome outcome)
        {
            QuotaSnapshot terminal = JudgeInvocation(r, "查询套餐额度超时，请稍后重试。", "查询套餐额度失败。");
            if (terminal != null) return terminal;

            if (string.IsNullOrEmpty(r.StdOut) || r.StdOut.Trim().Length == 0)
            {
                return Fail(QuotaStatus.FormatError, "套餐额度返回为空。");
            }

            try
            {
                ViewerIdentity viewer = ParseViewerQuiet(r.StdOut);
                outcome.Viewer = viewer;
                outcome.Verdict = ScopeValidation.Validate(outcome.AuthScope, viewer);
                return QuotaParser.Parse(r.StdOut);
            }
            catch (Exception)
            {
                return Fail(QuotaStatus.FormatError, "返回数据解析失败。");
            }
        }

        // Best-effort viewer extraction for scope validation. Never throws.
        private static ViewerIdentity ParseViewerQuiet(string json)
        {
            try
            {
                System.Web.Script.Serialization.JavaScriptSerializer ser =
                    new System.Web.Script.Serialization.JavaScriptSerializer();
                Dictionary<string, object> root = ser.DeserializeObject(json) as Dictionary<string, object>;
                return IdentityParse.ParseViewer(root);
            }
            catch (Exception) { return null; }
        }

        // Shared invocation verdict for both auth and usage.
        // Returns null only when the command started, exited 0 and produced output.
        private static QuotaSnapshot JudgeInvocation(CliResult r, string timeoutMsg, string failMsg)
        {
            if (r.Cancelled) return Cancelled();
            if (r.RuntimeError == ArkCliRuntimeError.RuntimeStartFailed)
                return Fail(QuotaStatus.RuntimeStartFailed, "运行组件无法启动，正在尝试恢复上一版本。");
            if (r.RuntimeError == ArkCliRuntimeError.RuntimeIncompatible)
                return Fail(QuotaStatus.RuntimeIncompatible, "运行组件不兼容，请检查安装文件。");
            if (r.Failure == "no-cli")
            {
                return Fail(QuotaStatus.RuntimeMissing, "ArkCLI 运行组件缺失，请检查安装文件。");
            }
            if (!r.Started) return Fail(QuotaStatus.Failed, failMsg);
            if (r.TimedOut) return Fail(QuotaStatus.Timeout, timeoutMsg);
            if (r.Failure != null) return Fail(QuotaStatus.Failed, failMsg);
            if (r.ExitCode != 0) return Fail(QuotaStatus.Failed, failMsg);
            return null;
        }

        public async Task<CliResult> RunAsync(string arguments, CancellationToken token)
        {
            if (_disposed)
            {
                CliResult d = new CliResult();
                d.Failure = "disposed";
                return d;
            }

            if (_mode != null && _mode.IsTest)
            {
                return await RunTestAsync(arguments, token);
            }
            await EnsureManagedAsync(token).ConfigureAwait(false);
            using (ArkCliRuntimeLease lease = _runtime.AcquireRuntime())
            {
                CliResolveResult resolved = Resolve(_runtime);
                if (resolved.Runtime != null && resolved.Runtime.IsManaged && lease != null) resolved.ExePath = lease.Runtime.ExePath;
                return await RunResolvedAsync(resolved, arguments, token).ConfigureAwait(false);
            }
        }

        private async Task EnsureManagedAsync(CancellationToken token)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARK_LEFT_CLI"))) return;
            try { await Task.Run(() => _runtime.EnsureRuntimeAsync(token), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception) { } // system fallback remains available
        }

        private async Task<CliResult> RunResolvedAsync(CliResolveResult resolved, string arguments, CancellationToken token)
        {
            if (_disposed) return new CliResult { Failure = "disposed" };
            if (_mode != null && _mode.IsTest) return await RunTestAsync(arguments, token);
            if (token.IsCancellationRequested) return new CliResult { Cancelled = true };
            if (resolved == null || !resolved.IsUsable)
                return new CliResult { Failure = "no-cli", RuntimeError = resolved == null ? ArkCliRuntimeError.RuntimeMissing : resolved.Error };
            CliResult result = await _invoker.RunAsync(resolved.ExePath, arguments, DefaultTimeoutMs, token).ConfigureAwait(false);
            ClassifyRuntimeResult(resolved, result);
            return result;
        }

        private static void ClassifyRuntimeResult(CliResolveResult resolved, CliResult result)
        {
            if (resolved.Runtime != null && resolved.Runtime.IsManaged && !result.Cancelled && !result.TimedOut
                && (result.Failure == "start-failed" || (result.Started && result.Failure == null && result.ExitCode < 0)))
                result.RuntimeError = ArkCliRuntimeError.RuntimeStartFailed;
        }

        // Test transport: produces a CliResult shaped exactly like the real one.
        private async Task<CliResult> RunTestAsync(string arguments, CancellationToken token)
        {
            CliResult result = new CliResult();

            if (_mode.DelayMs > 0)
            {
                try
                {
                    await Task.Delay(_mode.DelayMs, token);
                }
                catch (OperationCanceledException)
                {
                    result.Cancelled = true;
                    return result;
                }
            }
            if (token.IsCancellationRequested)
            {
                result.Cancelled = true;
                return result;
            }

            bool isAuth = arguments.StartsWith("auth", StringComparison.Ordinal);
            result.Started = true;

            if (isAuth)
            {
                if (_mode.AuthExitCode.HasValue)
                {
                    result.ExitCode = _mode.AuthExitCode.Value;
                    if (result.ExitCode == 124) result.TimedOut = true;
                }
                else
                {
                    result.StdOut = _mode.AuthJson; // may be null -> format error upstream
                }
            }
            else
            {
                if (_mode.UsageExitCode.HasValue)
                {
                    result.ExitCode = _mode.UsageExitCode.Value;
                    if (result.ExitCode == 124) result.TimedOut = true;
                }
                else
                {
                    result.StdOut = _mode.UsageJson; // may be null -> format error upstream
                }
            }

            return result;
        }

        public static CliResolveResult Resolve()
        {
            return Resolve(new ArkCliRuntimeManager(new ArkCliInvoker()));
        }

        public static CliResolveResult Resolve(ArkCliRuntimeManager runtime)
        {
            CliResolveResult r = new CliResolveResult();

            // 1) explicit override: must be an absolute path to an existing .exe
            string overridePath = Environment.GetEnvironmentVariable("ARK_LEFT_CLI");
            if (!string.IsNullOrEmpty(overridePath))
            {
                bool valid = false;
                try
                {
                    valid = Path.IsPathRooted(overridePath)
                            && overridePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(overridePath);
                }
                catch (Exception) { }
                if (valid)
                {
                    r.Found = true;
                    r.ExePath = overridePath;
                    return r;
                }
                r.Found = false;
                r.Message = "ARK_LEFT_CLI 必须是存在的绝对 .exe 路径。";
                r.Error = ArkCliRuntimeError.RuntimeMissing;
                return r;
            }

            // 2) Managed runtime is the production default.
            ArkCliRuntimeInfo managed = runtime.GetActiveRuntime();
            if (managed != null)
            {
                r.Found = true; r.ExePath = managed.ExePath; r.Runtime = managed;
                return r;
            }

            // 3) PATH: native arkcli.exe, or the npm shim's bound native exe.
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] dirs = pathEnv.Split(';');
            List<string> candidates = new List<string>();
            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i];
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    string direct = Path.Combine(dir, "arkcli.exe");
                    if (File.Exists(direct)) { candidates.Add(direct); continue; }
                    string architecture = ArkCliRuntimeConfig.Architecture();
                    if (architecture == null) continue;
                    string native = Path.Combine(dir, "node_modules", "@volcengine", "ark-cli", "bin", "arkcli-windows-" + architecture + ".exe");
                    if (File.Exists(native)) candidates.Add(native);
                }
                catch (Exception) { }
            }

            if (candidates.Count > 0)
            {
                r.Found = true;
                r.ExePath = candidates[0];
                return r;
            }

            r.Found = false;
            r.Message = "ArkCLI 运行组件不可用，请检查安装文件。";
            r.Error = runtime.LastError == ArkCliRuntimeError.None ? ArkCliRuntimeError.RuntimeMissing : runtime.LastError;
            return r;
        }

        private static bool TryReadLoggedIn(string json, out bool loggedIn)
        {
            loggedIn = false;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                System.Web.Script.Serialization.JavaScriptSerializer ser =
                    new System.Web.Script.Serialization.JavaScriptSerializer();
                object root = ser.DeserializeObject(json);
                Dictionary<string, object> d = root as Dictionary<string, object>;
                if (d == null) return false;
                object raw;
                if (!d.TryGetValue("logged_in", out raw) || raw == null) return false;
                if (raw is bool) { loggedIn = (bool)raw; return true; }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static QuotaSnapshot Fail(QuotaStatus status, string message)
        {
            QuotaSnapshot s = new QuotaSnapshot();
            s.Status = status;
            s.Message = message;
            s.FetchedAt = DateTime.Now;
            return s;
        }

        private static QuotaSnapshot Cancelled()
        {
            return Fail(QuotaStatus.Cancelled, "查询已取消。");
        }

        private static bool IsTerminalFailure(QuotaStatus s)
        {
            return s == QuotaStatus.Failed || s == QuotaStatus.Timeout
                || s == QuotaStatus.FormatError || s == QuotaStatus.RuntimeStartFailed
                || s == QuotaStatus.RuntimeMissing || s == QuotaStatus.RuntimeIncompatible;
        }

        public void KillActive()
        {
            _invoker.KillActive();
        }

        public void Dispose()
        {
            _disposed = true;
            _invoker.Dispose();
        }
    }
}
