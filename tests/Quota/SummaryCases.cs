using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private static void DisplayNameMapping()
        {
            Check("name.product", DisplayNames.Product("coding-plan-team"), "Coding Plan 团队版");
            Check("name.period", DisplayNames.Period("weekly"), "每周");
            Check("name.teamFlag", DisplayNames.IsTeam("agent-plan-team"), true);
            Check("name.teamFlagFalse", DisplayNames.IsTeam("agent-plan"), false);
        }

        private static void PercentFormatCases()
        {
            Check("pct.zero", PercentFormat.Remaining(0), "已用尽");
            Check("pct.sub1", PercentFormat.Remaining(0.4), "<1%");
            Check("pct.oneDecimal", PercentFormat.Remaining(75.54), "75.5%");
            Check("pct.integer", PercentFormat.Remaining(75.0), "75%");
            Check("pct.barZero", PercentFormat.RemainingForBar(0), "已用尽");
            Check("pct.barSub1", PercentFormat.RemainingForBar(0.2), "剩余 <1%");
            Check("pct.bar", PercentFormat.RemainingForBar(12.34), "剩余 12.3%");
        }

        private static void RelativeFormatCases()
        {
            DateTime now = new DateTime(2026, 10, 3, 12, 0, 0);
            Check("rel.freshNow", RelativeFormat.Freshness(now.AddSeconds(-10), now), "刚刚查询");
            Check("rel.freshMin", RelativeFormat.Freshness(now.AddMinutes(-5), now), "约 5 分钟前查询");
            // Reset in the past must not imply quota recovered.
            Check("rel.past", RelativeFormat.Countdown(now.AddMinutes(-1), now),
                "已到重置时间，请刷新");
            Check("rel.hours", RelativeFormat.Countdown(now.AddHours(3).AddMinutes(10), now),
                "3 小时 10 分后重置");
            Check("rel.days", RelativeFormat.Countdown(now.AddDays(2).AddHours(1), now),
                "2 天 1 小时后重置");
        }

        private static void RiskSummaryCases()
        {
            // Two products, no summing. agent-plan lowest = 20 (25 -> 75, 80 -> 20);
            // coding-plan lowest = 40. Low flag only when <= 20.
            QuotaSnapshot s = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true,
                    Period("5h", "25", null, null, null) + "," + Period("weekly", "80", null, null, null)),
                Item("coding-plan", null, null, true, Period("monthly", "60", null, null, null))));
            System.Collections.Generic.List<RiskSummary> r = RiskSummaryBuilder.Build(s);
            Check("risk.count", r.Count, 2);
            CheckClose("risk.agentLowest", r[0].LowestPercent, 20.0);
            Check("risk.agentLow", r[0].Low, true);
            CheckClose("risk.codingLowest", r[1].LowestPercent, 40.0);
            Check("risk.codingNotLow", r[1].Low, false);

            // Unknown/error periods are ignored, never counted as 0.
            QuotaSnapshot s2 = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true,
                    Period("5h", null, null, null, null) + ",{\"label\":\"weekly\",\"error\":\"x\"}")));
            System.Collections.Generic.List<RiskSummary> r2 = RiskSummaryBuilder.Build(s2);
            Check("risk.ignore.none", r2[0].HasValue, false);
        }

        // UX019 (v0.11): pure summary formatter semantics. Exact text match on
        // a fully specified snapshot, "未知" placeholders for missing fields,
        // 0 / 100 / <1% percent semantics, explicit failure retention, cached
        // markers and a hard no-identity guarantee.
        private static void QuotaSummaryCases()
        {
            Check("summary.nullView", QuotaSummary.Build(null) == null, true);
            PanelView empty = new PanelView();
            empty.State = PanelState.NoData;
            Check("summary.noDataNull", QuotaSummary.Build(empty) == null, true);

            QuotaSnapshot snap = new QuotaSnapshot();
            snap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            ProductQuota p1 = new ProductQuota();
            p1.Product = "coding-plan"; p1.DisplayName = "Coding Plan"; p1.Edition = "personal";
            p1.Subscribed = true; p1.SubscribedKnown = true;
            PeriodQuota q1 = new PeriodQuota();
            q1.Label = "monthly";
            q1.PercentKnown = true; q1.RemainingPercent = 25;
            q1.AmountKnown = true; q1.RemainingAmount = 750;
            q1.HasReset = true; q1.ResetLocal = new DateTime(2026, 10, 28, 23, 59, 59);
            p1.Periods.Add(q1);
            PeriodQuota q2 = new PeriodQuota();
            q2.Label = "session"; q2.LabelDisplay = DisplayNames.Period("session");
            p1.Periods.Add(q2);
            snap.Products.Add(p1);

            PanelView v = new PanelView();
            v.State = PanelState.ShowingCurrent; v.Data = snap;
            Check("summary.exact", QuotaSummary.Build(v),
                "方舟订阅额度摘要\r\n" +
                "最后更新 2026-10-04 12:30\r\n" +
                "\r\n" +
                "Coding Plan · 个人版\r\n" +
                "- 每月：剩余 25%，可用剩余 750 额度，2026-10-28 23:59 重置\r\n" +
                "- 会话：剩余未知，可用剩余未知，重置时间未知");

            // Missing fields stay unknown; 0 / 100 / <1% keep the bar wording.
            q2.PercentKnown = true; q2.RemainingPercent = 100;
            Check("summary.percent100", QuotaSummary.Build(v).Contains("- 会话：剩余 100%"), true);
            q2.RemainingPercent = 0;
            Check("summary.percent0", QuotaSummary.Build(v).Contains("- 会话：已用尽"), true);
            q2.RemainingPercent = 0.5;
            Check("summary.percentLt1", QuotaSummary.Build(v).Contains("- 会话：剩余 <1%"), true);
            q2.PercentKnown = false; q2.UnknownNote = "字段缺失";
            Check("summary.unknownNote", QuotaSummary.Build(v).Contains(
                "- 会话：剩余未知，可用剩余未知，重置时间未知（缺少可用的百分比数据。）"), true);
            q2.UnknownNote = null;
            q2.AmountKnown = true; q2.RemainingAmount = 12.5;
            Check("summary.amount", QuotaSummary.Build(v).Contains("可用剩余 12.5"), true);
            q2.AmountKnown = false;
            q2.Error = "该周期获取失败";
            Check("summary.periodError", QuotaSummary.Build(v).Contains("- 会话：该周期获取失败"), true);
            q2.Error = null;

            // A failed product keeps its explicit failure wording.
            ProductQuota p2 = new ProductQuota();
            p2.Product = "agent-plan"; p2.DisplayName = "Agent Plan"; p2.Edition = "personal";
            p2.Tier = "medium"; p2.SubscribedKnown = true; p2.Subscribed = true;
            p2.Error = "套餐获取失败";
            snap.Products.Add(p2);
            Check("summary.productError", QuotaSummary.Build(v).Contains(
                "Agent Plan · 个人版 · 中型：额度获取失败，请稍后重试。"), true);
            snap.Products.Remove(p2);

            // Cached / stale / unknown-identity markers mirror the panel note.
            v.State = PanelState.StaleError;
            Check("summary.stale", QuotaSummary.Build(v).Contains("状态：未能更新，显示上次数据。"), true);
            v.State = PanelState.ShowingCurrent; v.FromCache = true;
            Check("summary.cached", QuotaSummary.Build(v).Contains("状态：上次数据"), true);
            v.FromCache = false; v.IdentityUnknown = true;
            Check("summary.identityUnknown", QuotaSummary.Build(v).Contains("状态：身份未完全确认"), true);
            v.IdentityUnknown = false;

            // Unknown times never become a fabricated date.
            snap.FetchedAt = DateTime.MinValue;
            Check("summary.unknownTime", QuotaSummary.Build(v).Contains("最后更新时间未知"), true);
            snap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);

            // Identity material never leaks into the summary.
            v.IdentityHint = "trn:iam::000000000000:user/sample";
            string s2 = QuotaSummary.Build(v);
            Check("summary.noIdentity", s2.Contains("trn:") || s2.Contains("sample")
                || s2.Contains("profile") || s2.Contains("account"), false);

            // Empty snapshot: honest message, no fabricated products.
            QuotaSnapshot none = new QuotaSnapshot();
            none.Message = "当前身份下未发现已订阅的方舟套餐。";
            PanelView ev = new PanelView();
            ev.State = PanelState.ShowingCurrent; ev.Data = none;
            Check("summary.emptyProducts", QuotaSummary.Build(ev).Contains(
                "当前身份下未发现已订阅的方舟套餐。"), true);

            // UX019 trust boundary: a sentinel placed in the upstream Message /
            // Error / UnknownNote must never survive into the summary. The
            // formatter uses fixed safe wording, independent of the parser's own
            // sanitising, so even an abnormal channel cannot leak raw text.
            const string secret = "SECRET-TOKEN-9f3a";
            none.Message = secret;
            Check("summary.emptyNoSecret", QuotaSummary.Build(ev).Contains(secret), false);
            Check("summary.emptySafeMsg", QuotaSummary.Build(ev).Contains(
                "当前身份下未发现已订阅的方舟套餐。"), true);

            ProductQuota evil = new ProductQuota();
            evil.Product = "agent-plan"; evil.DisplayName = "Agent Plan";
            evil.SubscribedKnown = true; evil.Subscribed = true; evil.Error = secret;
            PeriodQuota eq = new PeriodQuota();
            eq.Label = "5h"; eq.LabelDisplay = DisplayNames.Period("5h");
            eq.PercentKnown = false; eq.UnknownNote = secret;
            evil.Periods.Add(eq);
            QuotaSnapshot evilSnap = new QuotaSnapshot();
            evilSnap.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            evilSnap.Message = secret;
            evilSnap.Products.Add(evil);
            PanelView evv = new PanelView();
            evv.State = PanelState.ShowingCurrent; evv.Data = evilSnap;
            string evilText = QuotaSummary.Build(evv);
            Check("summary.noErrorSecret", evilText.Contains(secret), false);
            Check("summary.safeError", evilText.Contains("额度获取失败，请稍后重试。"), true);
            Check("summary.safeNote", evilText.Contains("缺少可用的百分比数据。"), true);

            // Defensive: a null product / null period must not throw; the
            // summary stays usable and honest.
            QuotaSnapshot nulls = new QuotaSnapshot();
            nulls.FetchedAt = new DateTime(2026, 10, 4, 12, 30, 0);
            nulls.Products.Add(null);
            ProductQuota np = new ProductQuota();
            np.Product = "coding-plan"; np.DisplayName = "Coding Plan";
            np.SubscribedKnown = true; np.Subscribed = true;
            np.Periods.Add(null);
            nulls.Products.Add(np);
            PanelView nv = new PanelView();
            nv.State = PanelState.ShowingCurrent; nv.Data = nulls;
            string nullText = QuotaSummary.Build(nv);
            Check("summary.nullSafe", nullText != null && nullText.Contains("Coding Plan"), true);
        }

    }
}
