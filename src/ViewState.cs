using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArkLeft
{
    // Panel states for the temporary popup. Transitions are driven by
    // PanelModel; the form only renders the resulting PanelView.
    public enum PanelState
    {
        Loading,             // first query, no prior confirmed data
        ConfirmingIdentity,  // auth phase, old data hidden
        ShowingCurrent,      // fresh, scope-confirmed data
        RefreshingSameScope, // showing last data while re-querying
        StaleError,          // showing last data; update failed
        CancelledStale,      // showing last data; query cancelled
        IdentityChanged,     // scope mismatch/unknown -> data cleared
        NoData,              // no cache yet; fixed, honest empty state
        Error                // no usable data, failure message
    }

    // Immutable render instruction produced by PanelModel.
    public class PanelView
    {
        public PanelState State;
        public QuotaSnapshot Data;      // non-null => render cards
        public string Message;          // shown when Data == null
        public bool MessageStrong;
        public string Note;             // note line accompanying data
        public bool NoteStrong;         // note should be styled as a warning
        public string Stage;            // stage / activity hint
        public bool AllowCancel;
        public bool AllowRetry;
        public bool AllowCopyLogin;
        public bool AllowOpenGuide;
        public string IdentityHint;     // friendly, privacy-safe identity line
        public bool IdentityUnknown;
        // T-057: fingerprint of the identity scope the displayed Data belongs to
        // (null when there is no Data or the Data is not scope-confirmed). This
        // is the ONLY scope authority for the floating menu's identity-change
        // detection; the friendly IdentityHint above is display text that can be
        // identical across a real owner change. Opaque; never surfaced to the UI
        // or logs.
        public string ScopeFingerprint;
        // True when Data came from a cross-restart snapshot rather than a fresh
        // query; the footer must then present its time as a last-update time.
        public bool FromCache;
        // The controller's stable, low-coupling commit interface for T-026: a
        // fresh result that should be persisted (only a scope-confirmed success)
        // and a fresh result newly committed (not a history snapshot). Persisting
        // must only happen when NewData && Persist && Data != null.
        public bool NewData;
        public bool Persist;
    }

    // Pure, testable panel state machine. Owns the in-memory last snapshot and
    // the confirmed identity scope. Never touches the UI, disk or network.
    //
    // UX011: retain history until a confirmed identity boundary or fresh success.
    public class PanelModel
    {
        public QueryScope ConfirmedScope { get; private set; }
        public QuotaSnapshot Last { get; private set; }
        public PanelState State { get; private set; }

        private QueryScope _pendingScope;
        private bool _pendingScopeKnown;
        private bool _authConfirmedThisQuery;
        private string _identityHint;
        private bool _showingCache;
        // Persist only the irreversible ownership binding across restarts.
        public string ConfirmedFingerprint { get; private set; }
        private PanelView _current;

        public PanelModel()
        {
            State = PanelState.NoData;
        }

        // Invoked exactly when the model evicts a confirmed in-memory snapshot
        // because the identity is definitively gone or changed (NotLoggedIn /
        // new scope / usage Mismatch). The controller wires this to Clear() the
        // on-disk snapshot (and only then). No other path clears disk.
        public Action EvictPersistent;

        // True while the currently displayed data came from the persistent
        // snapshot (until a fresh successful query replaces it).
        public bool ShowingCache { get { return _showingCache; } }

        // Always notify disk eviction, including a repeated logout with no Last.
        private void Evict()
        {
            Last = null;
            ConfirmedScope = null;
            ConfirmedFingerprint = null;
            _showingCache = false;
            if (EvictPersistent != null)
            {
                try { EvictPersistent(); } catch (Exception) { }
            }
        }

        // Displays the cross-restart snapshot immediately, WITHOUT triggering any
        // query. The state machine is put into a plain ShowingCurrent so that a
        // later BeginQuery leaves data on screen. Retain the fingerprint so auth
        // can evict a different owner before usage returns; loading never saves.
        public PanelView ShowSnapshot(QuotaSnapshot snap, string fingerprint)
        {
            _pendingScope = null;
            _pendingScopeKnown = false;
            _authConfirmedThisQuery = false;
            Last = snap;
            ConfirmedFingerprint = fingerprint;
            ConfirmedScope = null;
            _identityHint = null;
            _showingCache = true;
            State = PanelState.ShowingCurrent;
            PanelView v = View(snap, "上次更新数据", null, false, false, false, false);
            v.FromCache = true;
            return v;
        }

        // Initial state when there is no cache: a fixed, honest empty state that
        // never fabricates a 0. Does not trigger a query by itself.
        public PanelView ShowEmptyNoData()
        {
            _pendingScope = null;
            _pendingScopeKnown = false;
            _authConfirmedThisQuery = false;
            _identityHint = null;
            _showingCache = false;
            Last = null;
            ConfirmedScope = null;
            ConfirmedFingerprint = null;
            State = PanelState.NoData;
            return View(null, "暂无数据", null, false, false, false, false);
        }

        public void SetIdentityHint(string hint)
        {
            _identityHint = hint;
        }

        // Reset only query-local confirmation; the rendered view stays unchanged.
        public PanelView BeginQuery()
        {
            _pendingScope = null;
            _pendingScopeKnown = false;
            _authConfirmedThisQuery = false;
            return CurrentView;
        }

        // Auth phase result.
        //  - loggedIn=false / failure: keep the last successful data visible (if
        //    any) with a brief note; only clear when there is no data to keep.
        //  - success: mark this query auth-confirmed; derive pending scope.
        //    Unknown identity => keep showing the retained data but never cache
        //    the result (auth failure alone cannot prove the identity changed).
        //    Same confirmed scope => keep the data on screen (no process state).
        //    New/changed scope => clear and load fresh.
        public PanelView OnAuthResult(bool loggedIn, AuthIdentity identity, QuotaStatus status,
            string message)
        {
            if (status == QuotaStatus.NotLoggedIn)
            {
                Evict();
                _identityHint = null;
                _authConfirmedThisQuery = false;
                State = PanelState.Error;
                return ErrorView(status, message);
            }
            if (identity != null)
                _identityHint = IdentityDisplay.Describe(identity);

            if (!loggedIn || status != QuotaStatus.Ok)
            {
                _pendingScope = null;
                _pendingScopeKnown = false;
                _authConfirmedThisQuery = false;
                if (Last != null)
                {
                    // Data is retained; this query did not confirm a new identity.
                    State = PanelState.StaleError;
                    PanelView rv = View(Last, "未能更新，显示上次数据。", null,
                        false, true, false, false);
                    rv.NoteStrong = true;
                    return rv;
                }
                ConfirmedScope = null;
                _identityHint = null; // never show a stale identity hint
                State = PanelState.Error;
                return ErrorView(status, message);
            }

            _authConfirmedThisQuery = true;
            _pendingScope = QueryScope.FromAuth(identity);
            _pendingScopeKnown = _pendingScope.IsKnown;

            if (!_pendingScopeKnown)
            {
                // Cannot prove the identity changed, so keep the retained data.
                if (Last != null)
                {
                    State = PanelState.ShowingCurrent;
                    PanelView uv = View(Last, "上次更新数据", null, false, false, false, false);
                    uv.IdentityUnknown = true;
                    return uv;
                }
                _identityHint = null; // unknown identity: stale hint must not persist
                State = PanelState.NoData;
                PanelView v = View(null, "暂无数据", null, false, false, false, false);
                v.IdentityUnknown = true;
                return v;
            }

            if (ConfirmedFingerprint == _pendingScope.Fingerprint && Last != null)
            {
                // Keep the data visible; no "更新中" process state is shown.
                State = PanelState.ShowingCurrent;
                return View(Last, "上次更新数据", null, false, false, false, false);
            }

            if (ConfirmedFingerprint != null && ConfirmedFingerprint != _pendingScope.Fingerprint)
            {
                Evict();
            }
            State = PanelState.NoData;
            return View(null, "暂无数据", null, false, false, false, false);
        }

        // Usage phase result after auth was confirmed.
        //  - Mismatch: identity changed, clear.
        //  - Unknown: display this result but NEVER cache it (mark unconfirmed).
        //  - Same with a known pending scope: cache and show.
        public PanelView OnUsageResult(QuotaSnapshot snap, ViewerIdentity viewer,
            ScopeVerdict verdict, string identityHint)
        {
            if (!string.IsNullOrEmpty(identityHint)) _identityHint = identityHint;

            if (verdict == ScopeVerdict.Mismatch)
            {
                Evict();
                State = PanelState.IdentityChanged;
                PanelView v = View(null,
                    "检测到身份变化，已停止复用上次结果。请重新查询。",
                    null, false, true, false, false);
                v.IdentityUnknown = true;
                return v;
            }

            bool canCache = verdict == ScopeVerdict.Same
                            && _authConfirmedThisQuery && _pendingScopeKnown;

            if (snap == null || (snap.Status != QuotaStatus.Ok && snap.Status != QuotaStatus.PartialError
                && snap.Status != QuotaStatus.NoSubscription))
                return OnUsageFailure(snap == null ? QuotaStatus.Failed : snap.Status,
                    snap == null ? "未能更新，请稍后重试。" : snap.Message, null);

            if (!canCache)
            {
                if (Last != null) return View(Last, "上次更新数据", null, false, false, false, false);
                // Display but never cache without a verified, consistent identity.
                _showingCache = false;
                Last = null;
                ConfirmedScope = null;
                State = PanelState.ShowingCurrent;
                PanelView uv = View(snap, "身份未完全确认", null, false, false, false, false);
                uv.IdentityUnknown = true;
                // An unconfirmed result must not inherit a stale confirmed scope:
                // its Data was never verified against this fingerprint.
                uv.ScopeFingerprint = null;
                uv.NewData = snap != null;
                return uv;
            }

            _showingCache = false;
            ConfirmedScope = _pendingScope;
            ConfirmedFingerprint = _pendingScope.Fingerprint;
            Last = snap;
            State = PanelState.ShowingCurrent;
            PanelView committed = View(snap, null, null, false, false, false, false);
            committed.NewData = true;
            committed.Persist = snap != null && (snap.Status == QuotaStatus.Ok
                || snap.Status == QuotaStatus.PartialError || snap.Status == QuotaStatus.NoSubscription);
            return committed;
        }

        // Retains the last successful data on failure, keeping its original
        // fetch time; a brief note is shown instead of a process state.
        public PanelView OnUsageFailure(QuotaStatus status, string message, string identityHint)
        {
            if (!string.IsNullOrEmpty(identityHint)) _identityHint = identityHint;
            if (Last != null)
            {
                State = PanelState.StaleError;
                PanelView v = View(Last, "未能更新，显示上次数据。", null, false, true, false, false);
                v.NoteStrong = true;
                return v;
            }
            ConfirmedScope = null;
            State = PanelState.Error;
            return ErrorView(status, message);
        }

        // Retains the last successful data on cancel; no process state is shown.
        public PanelView OnCancelled(string identityHint)
        {
            if (!string.IsNullOrEmpty(identityHint)) _identityHint = identityHint;
            if (Last != null)
            {
                State = PanelState.CancelledStale;
                return View(Last, "已取消，显示上次数据。", null, false, true, false, false);
            }
            ConfirmedScope = null;
            ConfirmedFingerprint = null;
            State = PanelState.Error;
            return ErrorView(QuotaStatus.Cancelled, "查询已取消。");
        }

        public void Reset()
        {
            Last = null;
            ConfirmedScope = null;
            _pendingScope = null;
            _pendingScopeKnown = false;
            _authConfirmedThisQuery = false;
            _identityHint = null;
            ConfirmedFingerprint = null;
            _showingCache = false;
            _current = null;
            State = PanelState.NoData;
        }

        private PanelView View(QuotaSnapshot data, string note, string stage, bool cancel,
            bool retry, bool copyLogin, bool openGuide)
        {
            PanelView v = new PanelView();
            v.Data = data;
            v.Note = note;
            v.Stage = stage;
            v.AllowCancel = cancel;
            v.AllowRetry = retry;
            v.AllowCopyLogin = copyLogin;
            v.AllowOpenGuide = openGuide;
            v.IdentityHint = _identityHint;
            v.ScopeFingerprint = data != null ? ConfirmedFingerprint : null;
            v.State = State;
            v.FromCache = _showingCache && data != null;
            if (data == null && State == PanelState.NoData) v.Message = "暂无数据";
            _current = v;
            return v;
        }

        private PanelView ErrorView(QuotaStatus status, string message)
        {
            PanelView v = new PanelView();
            v.State = State;
            v.Message = message;
            v.MessageStrong = (status == QuotaStatus.NotLoggedIn || status == QuotaStatus.CliMissing);
            // NotLoggedIn can be retried after the user logs in, and also offers
            // the copy-login-command / open-guide actions.
            v.AllowRetry = true;
            v.AllowCopyLogin = (status == QuotaStatus.NotLoggedIn);
            v.AllowOpenGuide = (status == QuotaStatus.CliMissing || status == QuotaStatus.Failed
                                || status == QuotaStatus.Timeout || status == QuotaStatus.FormatError
                                || status == QuotaStatus.NotLoggedIn);
            v.IdentityHint = _identityHint;
            v.ScopeFingerprint = null; // error states never carry a scope
            _current = v;
            return v;
        }

        public PanelView CurrentView
        {
            get { return _current ?? ShowEmptyNoData(); }
        }

        // Final outcome is authoritative even when Progress was delayed/dropped.
        public PanelView CommitOutcome(QueryOutcome outcome)
        {
            BeginQuery();
            QuotaStatus status = outcome.Snapshot == null ? QuotaStatus.Failed : outcome.Snapshot.Status;
            string message = outcome.Snapshot == null ? "查询失败，请稍后重试。" : outcome.Snapshot.Message;
            if (status == QuotaStatus.NotLoggedIn)
                return OnAuthResult(false, null, status, message);
            if (outcome.AuthConfirmed) OnAuthResult(true, outcome.Identity, QuotaStatus.Ok, null);
            if (outcome.Verdict == ScopeVerdict.Mismatch)
                return OnUsageResult(null, null, ScopeVerdict.Mismatch, null);
            if (status == QuotaStatus.Cancelled) return OnCancelled(null);
            if (!outcome.AuthConfirmed || (status != QuotaStatus.Ok && status != QuotaStatus.PartialError
                && status != QuotaStatus.NoSubscription)) return OnUsageFailure(status, message, null);
            return OnUsageResult(outcome.Snapshot, outcome.Viewer, outcome.Verdict, null);
        }
    }

    // Pure formatting helpers for the countdown / freshness / percentages.
    public static class RelativeFormat
    {
        public static string Freshness(DateTime fetchedLocal, DateTime now)
        {
            if (fetchedLocal == DateTime.MinValue) return "更新时间未知";
            TimeSpan d = now - fetchedLocal;
            if (d < TimeSpan.Zero) d = TimeSpan.Zero;
            if (d.TotalSeconds < 45) return "刚刚查询";
            if (d.TotalMinutes < 60) return "约 " + ((int)Math.Round(d.TotalMinutes)) + " 分钟前查询";
            if (d.TotalHours < 24) return "约 " + ((int)Math.Round(d.TotalHours)) + " 小时前查询";
            return "约 " + ((int)Math.Round(d.TotalDays)) + " 天前查询";
        }

        // Reset countdown. Never infers that quota recovered once reset passed.
        public static string Countdown(DateTime resetLocal, DateTime now)
        {
            if (resetLocal <= now) return "已到重置时间，请刷新";
            TimeSpan d = resetLocal - now;
            if (d.TotalDays >= 1)
                return ((int)d.TotalDays) + " 天 " + d.Hours + " 小时后重置";
            if (d.TotalHours >= 1)
                return ((int)d.TotalHours) + " 小时 " + d.Minutes + " 分后重置";
            if (d.TotalMinutes >= 1)
                return ((int)d.TotalMinutes) + " 分 " + d.Seconds + " 秒后重置";
            return d.Seconds + " 秒后重置";
        }
    }

    // Percentage rendering per the interaction rules.
    public static class PercentFormat
    {
        // 0 => "已用尽"; (0,1) => "<1%"; otherwise trimmed to at most one decimal.
        public static string Remaining(double v)
        {
            if (v <= 0.0) return "已用尽";
            if (v < 1.0) return "<1%";
            double r = Math.Round(v, 1);
            return r.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        public static string RemainingForBar(double v)
        {
            if (v <= 0.0) return "已用尽";
            if (v < 1.0) return "剩余 <1%";
            double r = Math.Round(v, 1);
            return "剩余 " + r.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }

    // Per-product risk summary. Picks the lowest valid remaining percentage
    // WITHIN each product; never sums amounts across periods or products.
    public class RiskSummary
    {
        public string ProductName;
        public string PeriodName;    // the period that is most constrained
        public bool HasValue;
        public double LowestPercent;
        public bool Low;

        public const double LowThreshold = 20.0; // engineering interaction choice
    }

    public static class RiskSummaryBuilder
    {
        public static List<RiskSummary> Build(QuotaSnapshot snap)
        {
            List<RiskSummary> list = new List<RiskSummary>();
            if (snap == null || snap.Products == null) return list;
            for (int i = 0; i < snap.Products.Count; i++)
            {
                ProductQuota pq = snap.Products[i];
                RiskSummary rs = new RiskSummary();
                rs.ProductName = pq.DisplayName;
                rs.PeriodName = null;
                rs.HasValue = false;
                double lowest = double.MaxValue;
                for (int j = 0; j < pq.Periods.Count; j++)
                {
                    PeriodQuota p = pq.Periods[j];
                    if (p.Error != null || !p.PercentKnown) continue; // ignore unknown/error
                    if (p.RemainingPercent < lowest)
                    {
                        lowest = p.RemainingPercent;
                        rs.PeriodName = p.LabelDisplay;
                    }
                }
                if (lowest < double.MaxValue)
                {
                    rs.HasValue = true;
                    rs.LowestPercent = lowest;
                    rs.Low = lowest <= RiskSummary.LowThreshold;
                }
                else rs.LowestPercent = 0;
                list.Add(rs);
            }
            return list;
        }
    }
}
