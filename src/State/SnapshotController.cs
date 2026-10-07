using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    // Shared runtime/test controller; callers invoke Poll on the same UI thread.
    internal sealed class SnapshotController : IDisposable
    {
        public const int PollIntervalMs = 300000;
        // v0.5 UX013: faster interval while the circle or the details panel is
        // visible; the base PollIntervalMs stays the all-hidden rate.
        public const int VisiblePollIntervalMs = 10000;
        private readonly PanelModel _model;
        private readonly Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> _query;
        private readonly Action<PanelView> _commit;
        private readonly Action _show;
        private readonly Func<CachedSnapshot, bool> _save;
        private CancellationTokenSource _active;
        private bool _started;
        private bool _disposed;
        private int _generation;
        private bool _paused;
        private Task _running;
        public bool Querying { get { return _active != null; } }

        public SnapshotController(PanelModel model,
            Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> query,
            Action<PanelView> commit, Action show, Func<CachedSnapshot> load,
            Func<CachedSnapshot, bool> save, Action clear)
        {
            _model = model; _query = query; _commit = commit; _show = show; _save = save;
            _model.EvictPersistent = clear;
            CachedSnapshot cached = load();
            _commit(cached == null ? model.ShowEmptyNoData()
                : model.ShowSnapshot(PersistentStateStore.FromCache(cached), cached.ScopeFingerprint));
        }

        public void Open() { if (!_disposed) _show(); }
        public Task Start()
        {
            if (_started || _disposed) return Task.FromResult(0);
            _started = true;
            return Refresh();
        }
        public Task Poll() { return Refresh(); }
        public Task Refresh()
        {
            if (_disposed || _paused || Querying) return Task.FromResult(0);
            Task task = RefreshCore();
            _running = task;
            return task;
        }
        private async Task RefreshCore()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            _active = cts;
            int generation = ++_generation;
            _model.BeginQuery();
            try
            {
                Progress<QueryProgress> progress = new Progress<QueryProgress>(delegate(QueryProgress p)
                {
                    if (_disposed || _active != cts || generation != _generation || p == null
                        || !p.AuthConfirmed || p.Stage != QueryStage.Auth) return;
                    QueryScope scope = QueryScope.FromAuth(p.AuthIdentity);
                    if (scope.IsKnown && _model.ConfirmedFingerprint != null
                        && scope.Fingerprint != _model.ConfirmedFingerprint)
                        _commit(_model.OnAuthResult(true, p.AuthIdentity, QuotaStatus.Ok, null));
                });
                QueryOutcome result;
                try { result = await _query(progress, cts.Token); }
                catch (OperationCanceledException) { result = Failure(QuotaStatus.Cancelled); }
                catch (Exception) { result = Failure(QuotaStatus.Failed); }
                if (_disposed || generation != _generation) return;
                PanelView view = _model.CommitOutcome(result ?? Failure(QuotaStatus.Failed));
                if (view.NewData && view.Persist)
                    _save(PersistentStateStore.ToCache(view.Data, _model.ConfirmedFingerprint));
                _commit(view);
            }
            finally { if (_active == cts) _active = null; cts.Dispose(); }
        }
        public async Task PauseAsync()
        {
            _paused = true; ++_generation;
            if (_active != null) _active.Cancel();
            Task running = _running;
            if (running != null) await running;
        }
        public void Resume() { if (!_disposed) _paused = false; }
        private static QueryOutcome Failure(QuotaStatus status)
        {
            return new QueryOutcome { Snapshot = new QuotaSnapshot {
                Status = status, Message = "未能更新，请稍后重试。" } };
        }
        public void Dispose()
        {
            _disposed = true; ++_generation;
            if (_active != null) _active.Cancel();
        }
    }
}
