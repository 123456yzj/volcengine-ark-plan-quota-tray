using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArkLeft
{
    // Overall query status. Distinguishes "no subscription" from failures so the
    // UI never renders a failure as a misleading 0.
    public enum QuotaStatus
    {
        Ok,
        NoSubscription,
        PartialError,
        NotLoggedIn,
        CliMissing,
        Timeout,
        Failed,
        FormatError,
        Cancelled,
        RuntimeMissing,
        RuntimeStartFailed,
        RuntimeIncompatible,
        LoginFailed,
        TokenRefreshFailed,
        Unauthorized,
        Forbidden,
        SignatureFailed,
        NetworkError,
        CredentialStorageFailed
    }

    public class QuotaSnapshot
    {
        public QuotaStatus Status;
        public string Message;
        public DateTime FetchedAt;
        public List<ProductQuota> Products = new List<ProductQuota>();

        public bool HasProducts
        {
            get { return Products != null && Products.Count > 0; }
        }
    }

    public class ProductQuota
    {
        public string Product;
        public string DisplayName;
        public string Edition;
        public string Tier;

        // Meaningful only when SubscribedKnown is true.
        public bool Subscribed;
        public bool SubscribedKnown;

        // Non-null when the item itself failed. Never treated as "unsubscribed".
        public string Error;

        // True when required fields were missing / malformed. Never faked.
        public bool Malformed;

        // True when at least one period bucket failed (item itself may still be usable).
        public bool PeriodErrorPresent;

        // Server-side data update time for the item (Coding Plan carries this at
        // the item level, not per period). May be absent.
        public bool HasUpdated;
        public DateTime UpdatedLocal;

        public List<PeriodQuota> Periods = new List<PeriodQuota>();
    }

    public class PeriodQuota
    {
        public string Label;
        public string LabelDisplay;

        // Non-null when this period bucket failed. Never treated as "unsubscribed".
        public string Error;

        public bool PercentKnown;
        public double RemainingPercent;
        public bool Clamped;

        public bool UsedKnown;
        public double Used;

        public bool TotalKnown;
        public double Total;

        public bool AmountKnown;
        public double RemainingAmount;

        public bool HasReset;
        public DateTime ResetLocal;

        public bool HasUpdated;
        public DateTime UpdatedLocal;

        // Human-readable reason why the percentage is unknown.
        public string UnknownNote;
    }

    // Pure display projection. Raw quota fields stay unchanged so parsing and
    // cache persistence retain the server's original values.
    public sealed class EffectivePeriodQuota
    {
        public bool PercentKnown;
        public double RemainingPercent;
        public bool AmountKnown;
        public double RemainingAmount;
    }

    public static class QuotaDisplay
    {
        public static EffectivePeriodQuota Effective(ProductQuota product, PeriodQuota period)
        {
            EffectivePeriodQuota result = new EffectivePeriodQuota();
            if (product == null || period == null || period.Error != null) return result;

            double minimum;
            bool hasMinimum = MinimumAmount(product, period, out minimum);
            double ownAmount;
            bool ownAmountValid = ValidAmount(period, out ownAmount);
            if (ownAmountValid && hasMinimum)
            {
                result.AmountKnown = true;
                result.RemainingAmount = minimum;
            }

            double total;
            if (ownAmountValid && hasMinimum && ValidTotal(period, out total))
            {
                double percent = minimum / total * 100.0;
                result.PercentKnown = true;
                result.RemainingPercent = percent > 100.0 ? 100.0 : percent;
            }
            else if (ValidPercent(period))
            {
                result.PercentKnown = true;
                result.RemainingPercent = period.RemainingPercent;
            }
            return result;
        }

        private static bool MinimumAmount(ProductQuota product, PeriodQuota target, out double minimum)
        {
            minimum = 0.0;
            double ownAmount;
            if (!ValidAmount(target, out ownAmount)) return false;
            minimum = ownAmount;

            if (product.Periods == null || target == null) return true;
            string label = target.Label;
            bool includeWeekly = label == "5h";
            bool includeMonthly = label == "5h" || label == "weekly";
            if (!includeWeekly && !includeMonthly) return true;

            for (int i = 0; i < product.Periods.Count; i++)
            {
                PeriodQuota candidate = product.Periods[i];
                if (candidate == null || candidate == target) continue;
                if (candidate.Label == "weekly" && !includeWeekly) continue;
                if (candidate.Label == "monthly" && !includeMonthly) continue;
                if (candidate.Label != "weekly" && candidate.Label != "monthly") continue;
                double amount;
                if (!ValidAmount(candidate, out amount)) continue;
                if (amount < minimum) minimum = amount;
            }
            return true;
        }

        private static bool ValidAmount(PeriodQuota period, out double amount)
        {
            amount = 0.0;
            if (period == null || period.Error != null || !period.AmountKnown) return false;
            amount = period.RemainingAmount;
            return !double.IsNaN(amount) && !double.IsInfinity(amount) && amount >= 0.0;
        }

        private static bool ValidTotal(PeriodQuota period, out double total)
        {
            total = period.Total;
            return period.TotalKnown && !double.IsNaN(total) && !double.IsInfinity(total)
                && total > 0.0;
        }

        private static bool ValidPercent(PeriodQuota period)
        {
            return period.PercentKnown && !double.IsNaN(period.RemainingPercent)
                && !double.IsInfinity(period.RemainingPercent)
                && period.RemainingPercent >= 0.0 && period.RemainingPercent <= 100.0;
        }
    }

    public static class DisplayNames
    {
        public static string Product(string p)
        {
            if (p == null) return "未知产品";
            switch (p)
            {
                case "agent-plan": return "Agent Plan";
                case "coding-plan": return "Coding Plan";
                case "agent-plan-team": return "Agent Plan 团队版";
                case "coding-plan-team": return "Coding Plan 团队版";
                default: return p.Length == 0 ? "未知产品" : p;
            }
        }

        public static bool IsTeam(string p)
        {
            return !string.IsNullOrEmpty(p) && p.EndsWith("-team", StringComparison.OrdinalIgnoreCase);
        }

        public static string Period(string label)
        {
            if (label == null) return "未知周期";
            switch (label)
            {
                case "5h": return "5 小时";
                case "daily": return "每日";
                case "weekly": return "每周";
                case "monthly": return "每月";
                case "session": return "会话";
                default: return label.Length == 0 ? "未知周期" : label;
            }
        }

        public static string Edition(string e)
        {
            if (string.IsNullOrEmpty(e)) return null;
            switch (e)
            {
                case "personal": return "个人版";
                case "team": return "团队版";
                default: return e;
            }
        }

        public static string Tier(string t)
        {
            if (string.IsNullOrEmpty(t)) return null;
            switch (t)
            {
                case "small": return "小型";
                case "medium": return "中型";
                case "large": return "大型";
                default: return t;
            }
        }

        public static string Number(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string FormatTime(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string FormatFetched(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
