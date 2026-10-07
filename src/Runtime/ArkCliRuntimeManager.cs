using System;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    public sealed class ArkCliRuntimeLease : IDisposable
    {
        public readonly ArkCliRuntimeInfo Runtime;
        private Action _release;
        internal ArkCliRuntimeLease(ArkCliRuntimeInfo runtime, Action release) { Runtime = runtime; _release = release; }
        public void Dispose() { Action release = Interlocked.Exchange(ref _release, null); if (release != null) release(); }
    }

    public sealed class ArkCliRuntimeManager
    {
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _maintenance = new SemaphoreSlim(1, 1);
        private readonly ArkCliRuntimeStore _store;
        private readonly ArkCliInvoker _invoker;
        private readonly IArkCliBinaryVerifier _verifier;
        private readonly string _bootstrapRoot, _architecture;
        private readonly IArkCliReleaseSource _releases;
        private readonly IArkCliDownloader _downloader;
        private ArkCliRuntimeState _state;
        private int _leases;
        private volatile bool _ready;
        private ArkCliRuntimeError _error;

        public ArkCliRuntimeManager(ArkCliInvoker invoker) : this(invoker,
            new ArkCliRuntimeStore(ArkCliRuntimeStore.DefaultRoot()),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-bootstrap"),
            ArkCliRuntimeConfig.Architecture(), null) { }
        public ArkCliRuntimeManager(ArkCliInvoker invoker, ArkCliRuntimeStore store,
            string bootstrapRoot, string architecture, IArkCliBinaryVerifier verifier,
            IArkCliReleaseSource releases = null, IArkCliDownloader downloader = null)
        {
            _invoker = invoker; _store = store; _bootstrapRoot = bootstrapRoot; _architecture = architecture;
            _verifier = verifier ?? new ArkCliBinaryVerifier(invoker);
            _releases = releases ?? new ArkCliReleaseClient();
            _downloader = downloader ?? new ArkCliDownloader();
            _state = _store.Load();
        }
        public ArkCliRuntimeState State { get { lock (_gate) return _state.Copy(); } }
        public ArkCliRuntimeError LastError { get { lock (_gate) return _error; } }
        private ArkCliRuntimeInfo Info(string version)
        {
            ArkCliVersion parsed = ArkCliVersion.Parse(version);
            if (!_store.Exists(version) || parsed == null
                || parsed.CompareTo(ArkCliVersion.Parse(ArkCliRuntimeConfig.MinimumSupportedVersion)) < 0) return null;
            return new ArkCliRuntimeInfo { Version = version, ExePath = _store.ExePath(version), IsManaged = true,
                IsBundled = version == ArkCliRuntimeConfig.BootstrapVersion };
        }
        public ArkCliRuntimeInfo GetActiveRuntime() { lock (_gate) return Info(_state.activeVersion); }
        public ArkCliRuntimeLease AcquireRuntime()
        {
            lock (_gate)
            {
                ArkCliRuntimeInfo info = Info(_state.activeVersion);
                if (info == null) return null;
                _leases++;
                return new ArkCliRuntimeLease(info, delegate {
                    lock (_gate)
                    {
                        _leases--;
                        try { ActivatePendingLocked(); } catch (Exception) { _error = ArkCliRuntimeError.RuntimeUpdateUnavailable; }
                    }
                });
            }
        }
        private void Commit(ArkCliRuntimeState state) { _store.Save(state); _state = state; }
        public async Task EnsureRuntimeAsync(CancellationToken token)
        {
            // A background download never delays a query using a ready runtime.
            if (_ready && GetActiveRuntime() != null) return;
            await _maintenance.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ready && GetActiveRuntime() != null) return;
                ArkCliRuntimeInfo active = GetActiveRuntime();
                if (active != null)
                {
                    try
                    {
                        await _verifier.VerifyAsync(active.ExePath, active.Version, null, token).ConfigureAwait(false);
                        lock (_gate)
                        {
                            ArkCliRuntimeState state = _state.Copy(); state.lastSuccessfulVersion = active.Version;
                            Commit(state);
                        }
                        string pending = State.pendingVersion;
                        if (pending != null)
                        {
                            try
                            {
                                ArkCliVersion next = ArkCliVersion.Parse(pending);
                                if (next.Major != ArkCliVersion.Parse(active.Version).Major || next.CompareTo(ArkCliVersion.Parse(active.Version)) <= 0)
                                    throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
                                await _verifier.VerifyAsync(_store.ExePath(pending), pending, null, token).ConfigureAwait(false);
                                lock (_gate) ActivatePendingLocked();
                            }
                            catch (ArkCliRuntimeException e)
                            {
                                lock (_gate) { ArkCliRuntimeState state = _state.Copy(); state.pendingVersion = null; Commit(state); _error = e.Category; }
                            }
                        }
                        _ready = true; return;
                    }
                    catch (ArkCliRuntimeException e) { SetError(e.Category); }
                }
                ArkCliRuntimeInfo previous;
                lock (_gate) previous = Info(_state.previousVersion);
                if (previous != null)
                {
                    try
                    {
                        await _verifier.VerifyAsync(previous.ExePath, previous.Version, null, token).ConfigureAwait(false);
                        lock (_gate)
                        {
                            ArkCliRuntimeState state = _state.Copy();
                            state.activeVersion = previous.Version; state.previousVersion = null; state.pendingVersion = null;
                            Commit(state);
                        }
                        _ready = true; return;
                    }
                    catch (ArkCliRuntimeException e) { SetError(e.Category); }
                }
                lock (_gate)
                {
                    // Failed validation must not leave an executable eligible for Resolve.
                    if (_leases != 0) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
                    ArkCliRuntimeState state = _state.Copy(); state.activeVersion = null; state.pendingVersion = null; Commit(state);
                }
                if (_architecture == null) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
                string source = Path.Combine(_bootstrapRoot, _architecture, "arkcli.exe");
                if (!File.Exists(source)) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeMissing);
                Directory.CreateDirectory(_store.DownloadsDirectory);
                string candidate = Path.Combine(_store.DownloadsDirectory, "bootstrap-" + Guid.NewGuid().ToString("N") + ".exe.part");
                try
                {
                    File.Copy(source, candidate);
                    // A damaged bootstrap-version directory can be repaired only
                    // after validation and while no query/process owns it.
                    await _verifier.VerifyAsync(candidate, ArkCliRuntimeConfig.BootstrapVersion,
                        ArkCliRuntimeConfig.BootstrapDigest(_architecture), token).ConfigureAwait(false);
                    string bootstrapPath = _store.ExePath(ArkCliRuntimeConfig.BootstrapVersion);
                    if (File.Exists(bootstrapPath))
                    {
                        try { await _verifier.VerifyAsync(bootstrapPath, ArkCliRuntimeConfig.BootstrapVersion,
                            ArkCliRuntimeConfig.BootstrapDigest(_architecture), token).ConfigureAwait(false); }
                        catch (ArkCliRuntimeException)
                        {
                            lock (_gate)
                            {
                                if (_leases != 0 || !_invoker.WhenIdle(delegate {
                                    ArkCliRuntimeState state = _state.Copy();
                                    if (state.activeVersion == ArkCliRuntimeConfig.BootstrapVersion) state.activeVersion = null;
                                    if (state.previousVersion == ArkCliRuntimeConfig.BootstrapVersion) state.previousVersion = null;
                                    state.pendingVersion = null; Commit(state);
                                    File.Move(bootstrapPath, Path.Combine(_store.DownloadsDirectory, Guid.NewGuid().ToString("N") + ".rejected"));
                                })) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
                            }
                        }
                    }
                    await InstallCandidateAsync(candidate, ArkCliRuntimeConfig.BootstrapVersion,
                        ArkCliRuntimeConfig.BootstrapDigest(_architecture), token).ConfigureAwait(false);
                    _ready = GetActiveRuntime() != null;
                }
                finally { ArkCliRuntimeStore.DeleteQuiet(candidate); }
            }
            catch (ArkCliRuntimeException e) { SetError(e.Category); throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { SetError(ArkCliRuntimeError.RuntimeMissing); throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeMissing); }
            finally { _maintenance.Release(); }
        }
        private async Task InstallCandidateAsync(string candidate, string version, string digest, CancellationToken token)
        {
            await _verifier.VerifyAsync(candidate, version, digest, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            string destination = _store.ExePath(version);
            if (destination == null) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            // Never replace an active executable; a retained target is verified again.
            if (File.Exists(destination))
                await _verifier.VerifyAsync(destination, version, digest, token).ConfigureAwait(false);
            else File.Move(candidate, destination);
            lock (_gate)
            {
                ArkCliRuntimeState state = _state.Copy(); state.pendingVersion = version;
                Commit(state); ActivatePendingLocked();
            }
        }
        private bool ActivatePendingLocked()
        {
            if (_leases != 0 || Info(_state.pendingVersion) == null) return false;
            return _invoker.WhenIdle(delegate
            {
                ArkCliRuntimeState state = _state.Copy();
                if (state.activeVersion != state.pendingVersion) state.previousVersion = state.activeVersion;
                state.activeVersion = state.pendingVersion; state.pendingVersion = null;
                state.lastSuccessfulVersion = state.activeVersion;
                Commit(state); _error = ArkCliRuntimeError.None;
                _store.Cleanup(state);
            });
        }
        private void SetError(ArkCliRuntimeError error) { lock (_gate) _error = error; }

        // Returns promptly if another maintenance operation owns the update lock.
        // Failure categories are diagnostics only; the existing active runtime remains usable.
        public async Task<ArkCliRuntimeError> CheckForUpdateAsync(CancellationToken token)
        {
            try { if (!await _maintenance.WaitAsync(0, token).ConfigureAwait(false)) return LastError; }
            catch (OperationCanceledException) { return LastError; }
            string part = null;
            try
            {
                ArkCliRuntimeInfo active = GetActiveRuntime();
                if (active == null) return ArkCliRuntimeError.RuntimeMissing;
                DateTime last;
                ArkCliRuntimeState before = State;
                if (DateTime.TryParse(before.lastCheckAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out last)
                    && DateTime.UtcNow - last.ToUniversalTime() < TimeSpan.FromHours(24)) return LastError;
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    ArkCliRuntimeState state = _state.Copy(); state.lastCheckAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    Commit(state); // record the attempt even when offline
                }
                ArkCliRelease release = await _releases.LatestAsync(_architecture, token).ConfigureAwait(false);
                ArkCliVersion latest = ArkCliVersion.Parse(release.Version);
                ArkCliVersion current = ArkCliVersion.Parse(active.Version);
                if (latest == null || latest.CompareTo(ArkCliVersion.Parse(ArkCliRuntimeConfig.MinimumSupportedVersion)) < 0)
                    throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
                if (latest.CompareTo(current) <= 0) { SetError(ArkCliRuntimeError.None); return LastError; }
                if (latest.Major != current.Major)
                {
                    SetError(ArkCliRuntimeError.RuntimeIncompatible); Log(release.Version, LastError); return LastError;
                }
                Directory.CreateDirectory(_store.DownloadsDirectory);
                part = Path.Combine(_store.DownloadsDirectory, "arkcli-" + latest + "-" + Guid.NewGuid().ToString("N") + ".exe.part");
                await _downloader.DownloadAsync(release, part, token).ConfigureAwait(false);
                await InstallCandidateAsync(part, latest.ToString(), release.Digest, token).ConfigureAwait(false);
                SetError(ArkCliRuntimeError.None); Log(latest.ToString(), ArkCliRuntimeError.None);
                return LastError;
            }
            catch (OperationCanceledException) { return LastError; }
            catch (ArkCliRuntimeException e) { SetError(e.Category); Log(null, e.Category); return e.Category; }
            catch (Exception) { SetError(ArkCliRuntimeError.RuntimeUpdateUnavailable); Log(null, LastError); return LastError; }
            finally { ArkCliRuntimeStore.DeleteQuiet(part); _maintenance.Release(); }
        }

        public async Task<bool> RollbackRuntimeAsync(string failedVersion, CancellationToken token)
        {
            try { await _maintenance.WaitAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
            try
            {
                ArkCliRuntimeInfo previous;
                lock (_gate)
                {
                    if (_leases != 0 || _state.activeVersion != failedVersion) return false;
                    _error = ArkCliRuntimeError.RuntimeStartFailed;
                    previous = Info(_state.previousVersion);
                }
                if (previous == null) return false;
                await _verifier.VerifyAsync(previous.ExePath, previous.Version, null, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (_leases != 0 || _state.activeVersion != failedVersion) return false;
                    bool switched = _invoker.WhenIdle(delegate {
                        ArkCliRuntimeState state = _state.Copy(); state.activeVersion = previous.Version;
                        // Failed versions are never retained as the next rollback target.
                        state.previousVersion = null; state.pendingVersion = null; state.lastSuccessfulVersion = previous.Version;
                        Commit(state); _ready = true; _error = ArkCliRuntimeError.None;
                    });
                    if (switched) Log(previous.Version, ArkCliRuntimeError.RuntimeStartFailed);
                    return switched;
                }
            }
            catch (OperationCanceledException) { return false; }
            catch (ArkCliRuntimeException e) { SetError(e.Category); return false; }
            catch (Exception) { SetError(ArkCliRuntimeError.RuntimeUpdateUnavailable); return false; }
            finally { _maintenance.Release(); }
        }
        private void Log(string version, ArkCliRuntimeError category)
        {
            try
            {
                string dir = Path.Combine(_store.Root, "logs"); Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "runtime.log");
                if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Delete(path);
                File.AppendAllText(path, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    + " version=" + (ArkCliVersion.Parse(version) == null ? "unknown" : version)
                    + " category=" + category + Environment.NewLine);
            }
            catch (Exception) { } // diagnostic storage never affects business operations
        }
    }
}
