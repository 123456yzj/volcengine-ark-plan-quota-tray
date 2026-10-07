using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm
    {
        // Draws the settings glyph on the header button; rebuilt only when the
        // DPI-scaled size changes (the old bitmap is disposed first).
        // ---- model binding / rendering ----

        public PanelModel Model { get { return _model; } }

        public void ApplyModelView(PanelView v)
        {
            if (v == null) return;
            string key = DisplayKey(v);
            bool changed = key != _displayKey;
            Point scroll = _content.AutoScrollPosition;
            Control focused = ActiveControl;
            _view = v;
            // UX022: the view-level note (stale / cache / identity) is shown
            // as a compact line INSIDE the first card; null when fresh.
            _statusNote = v.State == PanelState.StaleError || v.State == PanelState.CancelledStale
                ? "未能更新，显示上次数据。" : (v.IdentityUnknown ? "身份未完全确认" :
                (v.FromCache ? "上次数据" : null));
            _statusStrong = v.NoteStrong;
            _content.SuspendLayout();
            try
            {
                if (changed && v.Data != null)
                {
                    RenderCards(v.Data);
                }
                else if (changed)
                {
                    ReplaceContent(new List<Control> { BuildMessageCard(
                        string.IsNullOrEmpty(v.Message) ? "暂无数据" : v.Message,
                        v.MessageStrong ? TextDark : TextMuted) });
                    if (v.AllowCopyLogin)
                        _content.Controls.Add(BuildActionButton("登录方舟", delegate {
                            if (LoginRequested != null) LoginRequested(this, EventArgs.Empty); }));
                    SyncStatusInPlace();
                    SizeToFit();
                }
                else
                {
                    // UX022: same semantics — the status line (and the update
                    // time tooltip) update IN PLACE: no card rebuild, no focus
                    // / scroll reset, and the window still hugs the card if
                    // the status line appeared or disappeared.
                    SyncStatusInPlace();
                    // No full two-pass re-evaluation: keeping the current
                    // width decision prevents the scrollbar pass from
                    // rebuilding the cards on every same-semantics refresh.
                    SizeToFit(false);
                }
                _introCard = null;
                if (_miCopy != null) _miCopy.Enabled = v.Data != null;
                _displayKey = key;
                SyncUpdateTimeTooltip();
            }
            finally
            {
                _content.ResumeLayout(true);
            }
            if (changed)
            {
                // UX022: replay the scroll offset ONLY when the rebuilt content
                // still scrolls. Setting AutoScrollPosition while AutoScroll is
                // off silently shifts the (single) card up and breaks the
                // bottom hug / rounded-card fit, even though the window kept
                // its measured height.
                if (_scrolling && _content.VerticalScroll.Visible)
                    _content.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
                else
                    _content.AutoScrollPosition = Point.Empty;
            }
            if (focused != null && !focused.IsDisposed && focused.CanFocus) focused.Focus();
            if (Visible) Invalidate(true);
        }

        private static string LastUpdate(DateTime time)
        {
            return time == DateTime.MinValue ? "更新时间未知" : "最后更新 " + DisplayNames.FormatTime(time);
        }

        // UX022 v0.14: the old footer update time now lives on the cards'
        // tooltip. Text-only, in place — no card rebuild, no focus / scroll
        // change.
        private void SyncUpdateTimeTooltip()
        {
            if (_view == null || _view.Data == null) return;
            string text = LastUpdate(_view.Data.FetchedAt);
            for (int i = 0; i < _content.Controls.Count; i++)
            {
                try { _tip.SetToolTip(_content.Controls[i], text); }
                catch (Exception) { }
            }
        }

        // UX022 v0.14: view-level status (stale / cache / identity-unknown)
        // must stay VISIBLE inside the card without a header / footer. One
        // persistent row exists per first card; refreshes only toggle its
        // text / visibility and a bounded height delta in place, so the
        // control tree stays stable (no create / dispose churn, no card
        // rebuild, no flicker) and focus / scroll survive. It carries the
        // "status" tag only while actually shown; hidden it is inert.
        private void SyncStatusInPlace()
        {
            if (_content.Controls.Count == 0) return;
            CardPanel card = _content.Controls[0] as CardPanel;
            if (card == null) return;
            Label st = FindStatusRow(card);
            if (st == null)
            {
                // Create the row once per card layout (Reflow's ClearCard
                // disposes it, so it is re-created there); keep it inert until
                // a note actually needs it.
                st = new Label();
                st.Name = "arkStatusRow";
                st.AutoSize = false;
                st.Visible = false;
                st.SetBounds(0, 0, 1, 1);
                card.Controls.Add(st);
            }
            bool want = !string.IsNullOrEmpty(_statusNote);
            bool active = (string)st.Tag == "status";
            if (want)
            {
                if (!active)
                {
                    st.Font = F(8.25f, false);
                    int oldH = card.Height;
                    st.SetBounds(card.PadX, Math.Max(0, oldH - S(8)),
                        Math.Max(1, card.Width - card.PadX * 2), S(16));
                    card.Height = oldH + S(22);
                    st.Tag = "status";   // marker for FindStatusLabelForTest
                    st.Visible = true;
                }
                st.Text = _statusNote;
                st.ForeColor = _statusStrong ? WarningColor : TextMuted;
            }
            else if (active)
            {
                st.Tag = null;
                st.Visible = false;
                st.Text = null;
                card.Height = Math.Max(1, card.Height - S(22));
            }
        }

        private static Label FindStatusRow(Control card)
        {
            foreach (Control c in card.Controls)
            {
                if (c is Label && c.Name == "arkStatusRow") return (Label)c;
            }
            return null;
        }

        // Length-delimited display semantics; deliberately excludes fetchedAt,
        // identity, server timestamps and raw used/total fields not shown by UX011.
        internal static string DisplayKey(PanelView v)
        {
            List<string> fields = new List<string>();
            if (v.Data == null)
            {
                fields.Add(string.IsNullOrEmpty(v.Message) ? "暂无数据" : v.Message);
                fields.Add(v.MessageStrong.ToString());
                fields.Add(v.AllowCopyLogin.ToString());
            }
            else
            {
                fields.Add(v.Data.Products.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (v.Data.Products.Count == 0)
                {
                    fields.Add(string.IsNullOrEmpty(v.Data.Message)
                        ? "当前身份下未发现已订阅的方舟套餐。" : v.Data.Message);
                    fields.Add((v.Data.Status == QuotaStatus.NotLoggedIn || v.Data.Status == QuotaStatus.CliMissing).ToString());
                }
                foreach (ProductQuota p in v.Data.Products)
                {
                    fields.Add(ProductTitle(p));
                    fields.Add(!p.SubscribedKnown ? "订阅状态未知" :
                        (!p.Subscribed && p.Error == null && !p.PeriodErrorPresent && !p.Malformed ? "未订阅" : ""));
                    fields.Add(p.Error ?? (p.Periods.Count == 0 ? "无周期数据。" : ""));
                    fields.Add(p.Periods.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (PeriodQuota q in p.Periods)
                    {
                        fields.Add(q.LabelDisplay); fields.Add(q.Error);
                        if (q.Error != null) continue;
                        EffectivePeriodQuota effective = QuotaDisplay.Effective(p, q);
                        fields.Add(effective.PercentKnown ? PercentFormat.RemainingForBar(effective.RemainingPercent) : "剩余未知");
                        fields.Add(effective.PercentKnown ? effective.RemainingPercent.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "");
                        fields.Add(effective.AmountKnown ? DisplayNames.Number(effective.RemainingAmount) : "");
                        fields.Add(q.HasReset ? DisplayNames.FormatTime(q.ResetLocal) : "");
                        fields.Add(q.UnknownNote != null && (q.Clamped || !effective.PercentKnown) ? q.UnknownNote : null);
                        fields.Add(q.UnknownNote != null && q.Clamped ? "warning" : "muted");
                    }
                    fields.Add("/product");
                }
            }
            System.Text.StringBuilder key = new System.Text.StringBuilder();
            foreach (string f in fields) key.Append(f == null ? -1 : f.Length).Append(':').Append(f);
            return key.ToString();
        }

        private void ReplaceContent(List<Control> next)
        {
            Control[] old = new Control[_content.Controls.Count];
            _content.Controls.CopyTo(old, 0);
            // Build first, attach once under suspended layout, then release old controls.
            _content.Controls.AddRange(next.ToArray());
            foreach (Control c in old) { _content.Controls.Remove(c); c.Dispose(); }
        }

        private static string TitleForState(PanelState s)
        {
            switch (s)
            {
                case PanelState.Loading: return "正在查询…";
                case PanelState.ConfirmingIdentity: return "确认身份…";
                case PanelState.RefreshingSameScope: return "刷新中…";
                case PanelState.StaleError: return "更新失败";
                case PanelState.CancelledStale: return "已取消";
                case PanelState.IdentityChanged: return "身份变化";
                case PanelState.Error: return "查询失败";
                default: return "方舟订阅额度";
            }
        }

        private void BuildActionButtons(PanelView v)
        {
            if (v.AllowRetry)
                _content.Controls.Add(BuildActionButton("重试", delegate { OnRefreshRequested(); }));
            if (v.AllowCopyLogin)
                _content.Controls.Add(BuildActionButton("登录方舟", delegate {
                    if (LoginRequested != null) LoginRequested(this, EventArgs.Empty); }));
            if (v.AllowOpenGuide)
                _content.Controls.Add(BuildActionButton("打开设置指南", delegate { OpenGuide(); }));
        }

        // ---- time labels (text-only; never rebuilds cards) ----

        private void TickTimeLabels()
        {
            if (!Visible || _view == null || _view.Data == null)
            {
                if (_clock.Enabled && !Visible) _clock.Stop();
                return;
            }
            try
            {
                SyncUpdateTimeTooltip();
            }
            catch (Exception) { }
        }

        // ---- rendering ----

        public void SetLoading()
        {
            ApplyModelView(_model.BeginQuery());
        }

        public void Render(QuotaSnapshot snap)
        {
            ApplyModelView(_model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
        }

        public void ApplyAuthResult(QueryOutcome o)
        {
            ApplyModelView(_model.OnAuthResult(
                o.AuthConfirmed, o.Identity, o.Snapshot.Status, o.Snapshot.Message));
        }

        // Auth-progress onset: hide old data and show "确认身份…" without
        // implying a terminal auth failure.
        public void BeginAuth()
        {
            ApplyModelView(_model.BeginQuery());
        }

        // Confirmed auth (logged_in=true) but before usage returns. The model
        // decides whether to restore same-scope data ("更新中") or keep loading.
        public void ApplyAuthProgress(QueryOutcome o)
        {
            ApplyModelView(_model.OnAuthResult(true, o.Identity, QuotaStatus.Ok, null));
        }

        public void SetIntro(string text)
        {
            // UX011 removes the first-run banner; keep this compatibility hook inert.
        }

        // Test hook: whether the first-run banner is actually rendered.
        public bool HasIntroCard { get { return _introCard != null; } }

        public void ApplyUsageResult(QueryOutcome o)
        {
            ApplyModelView(_model.OnUsageResult(o.Snapshot, o.Viewer, o.Verdict, null));
        }

        public void ApplyUsageFailure(QuotaStatus status, string message)
        {
            ApplyModelView(_model.OnUsageFailure(status, message, null));
        }

        public void ApplyCancelled()
        {
            ApplyModelView(_model.OnCancelled(null));
        }

        public bool IsQueryingUi
        {
            get { return _view != null && _view.AllowCancel; }
        }

        private void RebuildContent(QuotaSnapshot snap)
        {
            RenderCards(snap);
        }

        private void RenderCards(QuotaSnapshot snap)
        {
            Screen openScr = ActiveScreen();
            Rectangle wa = openScr.WorkingArea;
            // UX022: the initial card width is the clamped logical card width;
            // SizeToFit (after the cards exist) owns the final two-pass
            // measurement and any scrollbar adjustment.
            _contentWidth = _cardWidth =
                LayoutMath.ClampWidth(PanelPositioner.LogicalCardWidth, _scale, wa.Width);

            BuildCards(snap);
            SyncStatusInPlace();
            SizeToFit();
        }

        private void BuildCards(QuotaSnapshot snap)
        {
            if (_rebuilding) return;
            _rebuilding = true;
            try
            {
                List<Control> next = new List<Control>();

                _content.SuspendLayout();
                try
                {
                    if (snap == null)
                    {
                        next.Add(BuildMessageCard("暂无数据", TextMuted));
                    }
                    else
                    {
                        bool hasCards = false;
                        if (snap.Products != null)
                        {
                            for (int i = 0; i < snap.Products.Count; i++)
                            {
                                next.Add(BuildProductCard(snap.Products[i], null));
                                hasCards = true;
                            }
                        }
                        if (!hasCards)
                        {
                            // Empty products (NoSubscription / unknown status): show
                            // the explicit snapshot message rather than a blank panel.
                            string msg = !string.IsNullOrEmpty(snap.Message)
                                ? snap.Message : "当前身份下未发现已订阅的方舟套餐。";
                            Color c = (snap.Status == QuotaStatus.NotLoggedIn
                                || snap.Status == QuotaStatus.CliMissing) ? TextDark : TextMuted;
                            next.Add(BuildMessageCard(msg, c));
                        }
                    }
                    ReplaceContent(next);
                }
                finally
                {
                    _content.ResumeLayout();
                }
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private void ClearContent()
        {
            Control[] old = new Control[_content.Controls.Count];
            _content.Controls.CopyTo(old, 0);
            _content.Controls.Clear();
            for (int i = 0; i < old.Length; i++)
            {
                try { old[i].Dispose(); } catch (Exception) { }
            }
        }
    }
}
