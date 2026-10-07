using System;
using System.Text;

namespace ArkLeft
{
    // UX019 (v0.11): pure formatter for the user-initiated "复制摘要" action.
    // It converts the currently displayed PanelView into a plain-text summary
    // that mirrors the on-screen rendering semantics (DisplayNames /
    // PercentFormat / product-title conventions) without reading the control
    // tree. Honest-data rules: a missing field is rendered as "未知", never a
    // fabricated 0; failed items / periods show FIXED failure wording; identity,
    // credentials, scope fingerprints, the panel Message and every upstream
    // Error / UnknownNote string are never copied verbatim (the formatter is an
    // independent trust boundary even if the parser normally sanitises); no
    // predicted or inferred values are added.
    public static class QuotaSummary
    {
        // Fixed safe wording. Never derived from upstream text.
        private const string NotSubscribed = "当前身份下未发现已订阅的方舟套餐。";
        private const string FetchFailed = "额度获取失败，请稍后重试。";
        private const string ItemUnknown = "订阅状态未知。";
        private const string UnknownPercent = "剩余未知";
        private const string UnknownAmount = "可用剩余未知";
        private const string UnknownReset = "重置时间未知";
        private const string PeriodFailed = "该周期获取失败。";
        private const string ClampNote = "原始百分比越界，已按 0-100 修正。";
        private const string TotalZeroNote = "总量为 0，额度未知。";
        private const string MissingNote = "缺少可用的百分比数据。";

        // Returns null when there is no usable snapshot (the copy action must
        // stay disabled and must not write the clipboard).
        public static string Build(PanelView v)
        {
            if (v == null || v.Data == null) return null;
            QuotaSnapshot d = v.Data;
            StringBuilder sb = new StringBuilder();
            sb.Append("方舟订阅额度摘要\r\n");
            sb.Append(d.FetchedAt == DateTime.MinValue
                ? "最后更新时间未知"
                : "最后更新 " + DisplayNames.FormatTime(d.FetchedAt));
            string note = StateNote(v);
            if (note.Length > 0) sb.Append("\r\n状态：").Append(note);
            if (d.Products == null || d.Products.Count == 0)
            {
                // The empty-products line is chosen from the status, never the
                // upstream Message.
                sb.Append("\r\n").Append(EmptyMessage(d.Status));
                return sb.ToString();
            }
            for (int i = 0; i < d.Products.Count; i++)
            {
                ProductQuota p = d.Products[i];
                if (p == null) continue;   // defensive: never throw on null
                sb.Append("\r\n\r\n");
                AppendProduct(sb, p);
            }
            return sb.ToString();
        }

        // Status-derived, fixed safe wording for an empty product list.
        private static string EmptyMessage(QuotaStatus status)
        {
            if (status == QuotaStatus.NotLoggedIn) return "当前未登录方舟账号。";
            if (status == QuotaStatus.CliMissing) return "未找到 ArkCLI，请先安装或配置。";
            if (status == QuotaStatus.Timeout) return "查询超时，请稍后重试。";
            if (status == QuotaStatus.Failed) return FetchFailed;
            if (status == QuotaStatus.FormatError) return "额度数据格式不正确。";
            return NotSubscribed;
        }

        // Same wording as the panel footer note (ApplyModelView semantics) so
        // cached / failed-retained states stay clearly marked.
        private static string StateNote(PanelView v)
        {
            if (v.State == PanelState.StaleError || v.State == PanelState.CancelledStale)
                return "未能更新，显示上次数据。";
            if (v.IdentityUnknown) return "身份未完全确认";
            if (v.FromCache) return "上次数据";
            return "";
        }

        private static void AppendProduct(StringBuilder sb, ProductQuota p)
        {
            sb.Append(ProductTitle(p));
            if (!p.SubscribedKnown) sb.Append("：").Append(ItemUnknown);
            else if (p.Error != null) sb.Append("：").Append(FetchFailed);
            else if (!p.Subscribed && !p.PeriodErrorPresent && !p.Malformed)
                sb.Append("：未订阅");
            if (p.Periods == null) return;
            for (int i = 0; i < p.Periods.Count; i++)
            {
                PeriodQuota q = p.Periods[i];
                if (q == null) continue;   // defensive: never throw on null
                sb.Append("\r\n");
                 AppendPeriod(sb, p, q);
            }
        }

        private static void AppendPeriod(StringBuilder sb, ProductQuota product, PeriodQuota q)
        {
            EffectivePeriodQuota effective = QuotaDisplay.Effective(product, q);
            sb.Append("- ").Append(string.IsNullOrEmpty(q.LabelDisplay)
                ? DisplayNames.Period(q.Label) : q.LabelDisplay).Append("：");
            if (q.Error != null) { sb.Append(PeriodFailed); return; }
            sb.Append(effective.PercentKnown
                ? PercentFormat.RemainingForBar(effective.RemainingPercent) : UnknownPercent);
            sb.Append(effective.AmountKnown
                ? "，可用剩余 " + DisplayNames.Number(effective.RemainingAmount) + " 额度"
                : "，" + UnknownAmount);
            sb.Append(q.HasReset
                ? "，" + DisplayNames.FormatTime(q.ResetLocal) + " 重置"
                : "，" + UnknownReset);
            string note = SafeUnknownNote(q, effective);
            if (note != null) sb.Append("（").Append(note).Append("）");
        }

        // Fixes the note from SAFE model flags only; the upstream UnknownNote
        // text is never echoed (a secret sentinel must not survive).
        private static string SafeUnknownNote(PeriodQuota q, EffectivePeriodQuota effective)
        {
            if (string.IsNullOrEmpty(q.UnknownNote)) return null;
            if (q.Clamped) return ClampNote;
            if (!effective.PercentKnown)
                return (q.TotalKnown && q.Total == 0.0) ? TotalZeroNote : MissingNote;
            return null;
        }

        // Mirrors PopupForm.ProductTitle: the product name already carries
        // "团队版" when applicable; do not repeat the edition.
        private static string ProductTitle(ProductQuota pq)
        {
            string name = string.IsNullOrEmpty(pq.DisplayName)
                ? DisplayNames.Product(pq.Product) : pq.DisplayName;
            StringBuilder sb = new StringBuilder();
            sb.Append(name);
            string ed = DisplayNames.Edition(pq.Edition);
            string tier = DisplayNames.Tier(pq.Tier);
            bool teamName = name.IndexOf("团队", StringComparison.Ordinal) >= 0;
            if (ed != null && !(teamName && ed == "团队版")) sb.Append(" · ").Append(ed);
            if (tier != null) sb.Append(" · ").Append(tier);
            return sb.ToString();
        }
    }
}
