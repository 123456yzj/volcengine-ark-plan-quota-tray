using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    internal sealed class DirectAgentPlan : IDisposable
    {
        private readonly IDirectTransport _http;
        private readonly IDirectBrowserLogin _browser;
        private readonly IDirectSessionStore _store;
        private readonly Func<DateTime> _now;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _activitySync = new object();
        private CancellationTokenSource _activity = new CancellationTokenSource();
        private bool _loggingOut;
        private readonly Timer _renew;
        private DirectSession _session;
        private DirectSts _sts;
        private QuotaStatus? _backgroundFailure;
        private bool _disposed;
        internal bool LastRefreshFailed { get; private set; }

        public DirectAgentPlan() : this(new DirectTransport(), new DirectBrowserLogin(),
            new DirectSessionStore(), delegate { return DateTime.UtcNow; }, true) { }

        internal DirectAgentPlan(IDirectTransport http, IDirectBrowserLogin browser,
            IDirectSessionStore store, Func<DateTime> now, bool automaticRenew)
        {
            _http = http; _browser = browser; _store = store; _now = now;
            _session = store.Load();
            if (automaticRenew) _renew = new Timer(Renew, null, 30000, 30000);
        }

        private bool NeedsRefresh { get { return _sts == null || _sts.ExpiresUtc <= _now().AddMinutes(2); } }

        private CancellationTokenSource OperationToken(CancellationToken token)
        {
            lock (_activitySync)
                return CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token,
                    _loggingOut ? new CancellationToken(true) : _activity.Token);
        }

        public async Task<CliResult> LoginAsync(CancellationToken token)
        {
            CliResult result = new CliResult { Started = true };
            using (CancellationTokenSource linked = OperationToken(token))
            {
                bool entered = false;
                try
                {
                    await _gate.WaitAsync(linked.Token).ConfigureAwait(false); entered = true;
                    _sts = null; _session = null; _backgroundFailure = null; LastRefreshFailed = false;
                    _store.Clear();
                    await LoginCoreAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { result.Cancelled = true; }
                catch (DirectFailure failure)
                {
                    result.ExitCode = 1; result.Failure = failure.Status.ToString(); result.StdErr = failure.Message;
                }
                catch (Exception) { result.ExitCode = 1; result.Failure = "LoginFailed"; result.StdErr = "浏览器登录失败，请重试。"; }
                finally { if (entered) _gate.Release(); }
            }
            return result;
        }

        public async Task<CliResult> LogoutAsync(CancellationToken token)
        {
            CliResult result = new CliResult { Started = true };
            CancellationTokenSource activity;
            lock (_activitySync)
            {
                if (_loggingOut) return new CliResult { Started = true, Cancelled = true };
                _loggingOut = true; activity = _activity;
            }
            bool entered = false;
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                try
                {
                    try { activity.Cancel(); } catch (AggregateException) { }
                    await _gate.WaitAsync(linked.Token).ConfigureAwait(false); entered = true;
                    _sts = null; _session = null; _backgroundFailure = null; LastRefreshFailed = false;
                    _store.Clear();
                }
                catch (OperationCanceledException) { result.Cancelled = true; }
                catch (Exception)
                {
                    result.ExitCode = 1; result.Failure = "CredentialStorageFailed";
                    result.StdErr = "无法清除本地登录凭据，请检查状态目录后重试登出。";
                }
                finally
                {
                    lock (_activitySync) { _activity = new CancellationTokenSource(); _loggingOut = false; }
                    activity.Dispose();
                    if (entered) _gate.Release();
                }
            }
            return result;
        }

        public async Task<QueryOutcome> QueryDetailedAsync(IProgress<QueryProgress> progress, CancellationToken token)
        {
            QueryOutcome outcome = new QueryOutcome { FailedStage = QueryStage.Auth };
            using (CancellationTokenSource linked = OperationToken(token))
            {
                bool entered = false;
                try
                {
                    await _gate.WaitAsync(linked.Token).ConfigureAwait(false); entered = true;
                    Report(progress, QueryStage.Auth, false, null);
                    if (_backgroundFailure.HasValue)
                    {
                        QuotaStatus failure = _backgroundFailure.Value; _backgroundFailure = null;
                        throw new DirectFailure(failure, DirectArkUsage.Failure(failure).Message);
                    }
                    if (_session == null) return WithSnapshot(outcome, DirectArkUsage.Failure(QuotaStatus.NotLoggedIn));
                    await EnsureCredentialsAsync(linked.Token).ConfigureAwait(false);
                    outcome.RefreshFailed = LastRefreshFailed;
                    outcome.AuthConfirmed = true;
                    outcome.Identity = new AuthIdentity { Present = true, Name = "浏览器登录", Type = "agent-plan",
                        Region = "cn-beijing", Project = "personal", DirectSessionBinding = _session.Binding };
                    outcome.AuthScope = QueryScope.FromAuth(outcome.Identity);
                    Report(progress, QueryStage.Auth, true, outcome.Identity);
                    outcome.FailedStage = QueryStage.Usage;
                    Report(progress, QueryStage.Usage, false, null);
                    DirectResponse response = await _http.PostAsync(DirectArkUsage.Url, "application/json", DirectArkUsage.Body,
                        DirectArkUsage.Sign(_sts, _now()), linked.Token).ConfigureAwait(false);
                    outcome.Snapshot = DirectArkUsage.Parse(response);
                    // The response belongs to the very STS/session used to sign
                    // this request. There is no separate CLI auth/viewer pair.
                    outcome.Verdict = ScopeVerdict.Same;
                    return outcome;
                }
                catch (OperationCanceledException) { return WithSnapshot(outcome, DirectArkUsage.Failure(QuotaStatus.Cancelled)); }
                catch (DirectFailure failure)
                {
                    outcome.RefreshFailed = LastRefreshFailed;
                    outcome.LoginFailed = failure.Status == QuotaStatus.LoginFailed || failure.Status == QuotaStatus.TokenRefreshFailed;
                    return WithSnapshot(outcome, new QuotaSnapshot { Status = failure.Status, Message = failure.Message, FetchedAt = DateTime.Now });
                }
                catch (Exception) { return WithSnapshot(outcome, DirectArkUsage.Failure(QuotaStatus.Failed)); }
                finally { if (entered) _gate.Release(); Report(progress, QueryStage.Done, false, null); }
            }
        }

        private async Task EnsureCredentialsAsync(CancellationToken token)
        {
            if (!NeedsRefresh) return;
            LastRefreshFailed = false;
            try
            {
                DirectResponse response = await TokenAsync(new Dictionary<string, string> {
                    { "grant_type", "refresh_token" }, { "refresh_token", _session.RefreshToken },
                    { "client_id", DirectBrowserLogin.ClientId }, { "scope", DirectBrowserLogin.Scope } }, token).ConfigureAwait(false);
                AcceptToken(response, false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                token.ThrowIfCancellationRequested();
                LastRefreshFailed = true; _sts = null; _session = null;
                _store.Clear();
            }
            if (LastRefreshFailed)
            {
                try { await LoginCoreAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { throw new DirectFailure(QuotaStatus.TokenRefreshFailed, "登录凭证续期失败，已重新拉起浏览器，但登录未完成。"); }
            }
        }

        private async Task LoginCoreAsync(CancellationToken token)
        {
            DirectAuthorization auth = await _browser.AuthorizeAsync(token).ConfigureAwait(false);
            DirectResponse response = await TokenAsync(new Dictionary<string, string> {
                { "grant_type", "authorization_code" }, { "code", auth.Code },
                { "redirect_uri", auth.RedirectUri }, { "client_id", DirectBrowserLogin.ClientId },
                { "scope", DirectBrowserLogin.Scope }, { "code_verifier", auth.Verifier } }, token).ConfigureAwait(false);
            try { AcceptToken(response, true); }
            catch (DirectFailure failure)
            {
                if (failure.Status == QuotaStatus.CredentialStorageFailed) throw;
                throw new DirectFailure(QuotaStatus.LoginFailed, "浏览器登录凭证交换失败，请重新登录。");
            }
        }

        private Task<DirectResponse> TokenAsync(IDictionary<string, string> values, CancellationToken token)
        {
            return _http.PostAsync(DirectBrowserLogin.TokenUrl, "application/x-www-form-urlencoded", DirectJson.Form(values), null, token);
        }

        private void AcceptToken(DirectResponse response, bool newLogin)
        {
            if (response.Status < 200 || response.Status >= 300)
                throw new DirectFailure(QuotaStatus.LoginFailed, "登录凭证交换失败，请重新登录。");
            Dictionary<string, object> data = DirectJson.Object(response.Body);
            object raw;
            Dictionary<string, object> sts;
            if (!data.TryGetValue("access_token", out raw)) throw new DirectFailure(QuotaStatus.FormatError, "登录响应缺少临时凭证。");
            sts = raw as Dictionary<string, object>;
            if (sts == null && raw is string) sts = DirectJson.Object((string)raw);
            string ak = DirectJson.Text(sts, "access_key_id"), sk = DirectJson.Text(sts, "secret_access_key"), security = DirectJson.Text(sts, "session_token");
            double seconds = 0;
            if (data.TryGetValue("expires_in", out raw))
                double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            if (string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk) || string.IsNullOrWhiteSpace(security)
                || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 120 || seconds > 86400)
                throw new DirectFailure(QuotaStatus.FormatError, "登录响应中的临时凭证或有效期不完整。");
            string refresh = DirectJson.Text(data, "refresh_token");
            if (string.IsNullOrWhiteSpace(refresh) && !newLogin && _session != null) refresh = _session.RefreshToken;
            if (string.IsNullOrWhiteSpace(refresh)) throw new DirectFailure(QuotaStatus.FormatError, "登录响应缺少续期凭证。");
            DirectSession session = new DirectSession { RefreshToken = refresh,
                Binding = newLogin ? Guid.NewGuid().ToString("N") : _session.Binding };
            if (!_store.Save(session)) throw new DirectFailure(QuotaStatus.CredentialStorageFailed, "无法安全保存登录凭证，请检查本地状态目录。");
            _session = session;
            _sts = new DirectSts { AccessKey = ak, SecretKey = sk, SessionToken = security, ExpiresUtc = _now().AddSeconds(seconds) };
        }

        private async void Renew(object ignored)
        {
            await RenewIfNeededAsync().ConfigureAwait(false);
        }

        internal async Task RenewIfNeededAsync()
        {
            using (CancellationTokenSource linked = OperationToken(CancellationToken.None))
            {
                bool entered = false;
                try
                {
                    if (_disposed || !await _gate.WaitAsync(0, linked.Token).ConfigureAwait(false)) return;
                    entered = true;
                    if (_session != null && _sts != null && NeedsRefresh)
                        await EnsureCredentialsAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (DirectFailure failure) { _backgroundFailure = failure.Status; }
                catch (Exception) { _backgroundFailure = QuotaStatus.TokenRefreshFailed; }
                finally { if (entered) _gate.Release(); }
            }
        }

        private static QueryOutcome WithSnapshot(QueryOutcome outcome, QuotaSnapshot snapshot) { outcome.Snapshot = snapshot; return outcome; }
        private static void Report(IProgress<QueryProgress> progress, QueryStage stage, bool confirmed, AuthIdentity identity)
        {
            if (progress != null) progress.Report(new QueryProgress { Stage = stage, AuthConfirmed = confirmed, AuthIdentity = identity });
        }
        public void Dispose()
        {
            _disposed = true; if (_renew != null) _renew.Dispose(); _lifetime.Cancel();
            // The semaphore/token source stay valid until in-flight callbacks exit.
            _sts = null;
        }
    }
}
