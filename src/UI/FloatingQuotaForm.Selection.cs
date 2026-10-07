using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class FloatingQuotaForm
    {
        // ---- model binding (same PanelView as the details panel) ----

        public void ApplyModelView(PanelView v)
        {
            _view = v;
            QuotaSnapshot snap = v == null ? null : v.Data;
            List<FloatingEntry> built = FloatingSelection.Build(snap);
            string signature = ContentSignature(built);
            string identitySignature = IdentitySignature(v);
            _entries = built;
            _selected = ResolveSelection();
            UpdateCircleDisplay();
            // A same-semantics refresh (the routine 10s poll) must NOT rebuild
            // the menu or invalidate a selection the user is making. Only a real
            // candidate-set change or a real scope change (the PanelView's
            // ScopeFingerprint / IdentityUnknown, never the display IdentityHint)
            // bumps the generation and rebuilds; the circle display above is
            // always refreshed.
            if (signature == _contentSignature
                && identitySignature == _contentIdentitySignature) return;
            _contentSignature = signature;
            _contentIdentitySignature = identitySignature;
            _contentGeneration++;
            PopulateContentMenu(_contentItem);
            if (ContentChanged != null) ContentChanged(this, EventArgs.Empty);
        }

        // Stable signature of the SELECTABLE candidate set (key + label in
        // order). Values / counts may move every poll without changing which
        // periods a user can pick, so they are deliberately excluded.
        private static string ContentSignature(List<FloatingEntry> entries)
        {
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(entries);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                sb.Append(candidates[i].ProductKey).Append('\u0001')
                    .Append(candidates[i].Label).Append('\u0002');
            }
            return sb.ToString();
        }

        // T-057: the scope authority of the built menu is the fingerprint of the
        // identity scope the PanelView's Data belongs to - NOT the friendly
        // IdentityHint (a real owner change keeps the same hint when type /
        // region / profile name are unchanged). A null fingerprint means "no
        // confirmed scope for this Data", which is distinct from every real
        // fingerprint, so an unconfirmed result can never keep an old-scope
        // selection valid. The value is opaque and is never rendered or logged.
        private static string IdentitySignature(PanelView v)
        {
            if (v == null) return "";
            return (v.ScopeFingerprint ?? "") + "\u0003" + (v.IdentityUnknown ? "1" : "0");
        }

        private FloatingSettings ResolveSelection()
        {
            if (_stored != null) return _stored; // keep the user's choice verbatim
            FloatingEntry def = FloatingSelection.Default(_entries);
            if (def == null) return null;
            FloatingSettings s = new FloatingSettings();
            s.Version = FloatingSettingsStore.FormatVersion;
            s.ProductKey = def.ProductKey;
            s.PeriodLabel = def.Label;
            return s; // runtime default only; never persisted
        }

        private void UpdateCircleDisplay()
        {
            FloatingDisplay d = new FloatingDisplay();
            if (_entries.Count == 0)
            {
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.Tooltip = AppendHint("暂无数据");
                _circle.SetDisplay(d);
                return;
            }
            if (_selected == null)
            {
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.Tooltip = AppendHint("暂无数据");
                _circle.SetDisplay(d);
                return;
            }

            FloatingEntry e = FloatingSelection.Find(_entries, _selected);
            if (e == null)
            {
                // Chosen target no longer present: keep the choice, ask the user
                // to reselect. Never silently switch to another target.
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.Tooltip = AppendHint("已选目标暂无数据，请在设置中重新选择。");
                _circle.SetDisplay(d);
                return;
            }

            d.HasData = true;
            d.AmountKnown = e.Selectable && e.AmountKnown;
            d.RemainingAmount = e.RemainingAmount;
            if (!e.HasTrustedValue)
            {
                d.PercentKnown = false;
                d.PercentText = "剩余未知";
                d.Tooltip = BuildUnknownTooltip(e);
            }
            else
            {
                d.PercentKnown = true;
                d.Percent = e.Percent;
                d.PercentText = CirclePercent(e.Percent);
                d.Tooltip = BuildValueTooltip(e);
            }
            _circle.SetDisplay(d);
        }

        private static string AppendHint(string baseText)
        {
            if (string.IsNullOrEmpty(baseText)) return CircleHint;
            return baseText + "\n" + CircleHint;
        }

        private string BuildValueTooltip(FloatingEntry e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(e.ShortCaption).Append('\n');
            sb.Append("剩余 ").Append(PercentFormat.Remaining(e.Percent));
            AppendFreshness(sb, false);
            sb.Append('\n').Append(CircleHint);
            return sb.ToString();
        }

        private string BuildUnknownTooltip(FloatingEntry e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(e.ShortCaption).Append('\n');
            sb.Append(e.StatusText);
            AppendFreshness(sb, e.PeriodError || e.ProductError);
            sb.Append('\n').Append(CircleHint);
            return sb.ToString();
        }

        private void AppendFreshness(StringBuilder sb, bool failed)
        {
            if (_view == null) return;
            if (_view.Data != null && _view.Data.FetchedAt != DateTime.MinValue)
            {
                sb.Append('\n');
                sb.Append(_view.FromCache ? "上次数据 " : "最后更新 ");
                sb.Append(DisplayNames.FormatTime(_view.Data.FetchedAt));
            }
            // Never let a limited / cached / failed result look freshly
            // confirmed: surface the PanelView's own safe note (no process state,
            // no raw identity). Fall back to a short line only if the model has
            // no note of its own.
            if (!string.IsNullOrEmpty(_view.Note))
            {
                sb.Append('\n').Append(_view.Note);
            }
            else if (failed || _view.State == PanelState.StaleError
                || _view.State == PanelState.CancelledStale)
            {
                sb.Append('\n').Append("更新失败，显示上次数据。");
            }
            if (_view.IdentityUnknown) sb.Append('\n').Append("身份未完全确认");
        }

        // 0 -> "0%", 100 -> "100%"; never rounds a real value into a false 100.
        public static string CirclePercent(double v)
        {
            if (v <= 0) return "0%";
            if (v >= 100) return "100%";
            if (v < 1) return "<1%";
            int r = (int)Math.Round(v);
            if (r >= 100) r = 99;
            if (r < 1) return "<1%";
            return r.ToString(CultureInfo.InvariantCulture) + "%";
        }

    }
}
