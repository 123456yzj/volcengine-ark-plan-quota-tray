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
    // One selectable (product, period) target, mapped from the snapshot in the
    // API's original order. Flags keep unknown / error / unsubscribed states
    // honest; a caller never turns them into a trusted percentage.
    public class FloatingEntry
    {
        public string ProductKey;
        public string Product;
        public string Edition;
        public string Tier;
        public string Label;
        public string ProductTitle;
        public string PeriodTitle;
        public bool PercentKnown;
        public double Percent;            // remaining percent, 0..100
        public bool AmountKnown;
        public double RemainingAmount;
        public bool ProductSubscribedKnown;
        public bool ProductSubscribed;
        public bool ProductError;
        public bool ProductMalformed;
        public bool PeriodError;

        // A period is selectable to be shown by the circle: a known, active
        // subscription without a product/period error. Percent may still be
        // unknown (then the circle shows "剩余未知").
        public bool Selectable
        {
            get
            {
                return ProductSubscribedKnown && ProductSubscribed
                    && !ProductError && !ProductMalformed && !PeriodError;
            }
        }

        public bool HasTrustedValue
        {
            get
            {
                return Selectable && PercentKnown
                    && !double.IsNaN(Percent) && !double.IsInfinity(Percent)
                    && Percent >= 0 && Percent <= 100;
            }
        }

        public string Key
        {
            get { return (ProductKey ?? "") + "\u0001" + (Label ?? ""); }
        }

        public string StatusText
        {
            get
            {
                if (PeriodError) return "获取失败";
                if (!ProductSubscribedKnown) return "订阅状态未知";
                if (!ProductSubscribed) return "未订阅";
                if (ProductError || ProductMalformed) return "获取失败";
                if (!PercentKnown || double.IsNaN(Percent) || double.IsInfinity(Percent)
                    || Percent < 0 || Percent > 100) return "剩余未知";
                return "剩余 " + PercentFormat.Remaining(Percent);
            }
        }

        public string ComboText
        {
            get { return ProductTitle + " · " + PeriodTitle + " — " + StatusText; }
        }

        public string ShortCaption
        {
            get { return ProductTitle + " · " + PeriodTitle; }
        }
    }

    // Pure ordering / default-selection rules (Q-006 / UX012 B). No UI, disk or
    // network. Default = first trusted known percentage IN API ORDER; if none,
    // the first selectable entry (shown as unknown). Never the global minimum,
    // never a sum / average.
    public static class FloatingSelection
    {
        public static List<FloatingEntry> Build(QuotaSnapshot snap)
        {
            List<FloatingEntry> list = new List<FloatingEntry>();
            if (snap == null || snap.Products == null) return list;
            for (int i = 0; i < snap.Products.Count; i++)
            {
                ProductQuota pq = snap.Products[i];
                if (pq == null) continue;
                string productKey = FloatingSettingsStore.ProductKey(pq);
                string productTitle = ProductTitle(pq);
                if (pq.Periods == null) continue;
                for (int j = 0; j < pq.Periods.Count; j++)
                {
                    PeriodQuota p = pq.Periods[j];
                    if (p == null) continue;
                    FloatingEntry e = new FloatingEntry();
                    e.ProductKey = productKey;
                    e.Product = pq.Product;
                    e.Edition = pq.Edition;
                    e.Tier = pq.Tier;
                    e.Label = p.Label;
                    e.ProductTitle = productTitle;
                    e.PeriodTitle = p.LabelDisplay != null ? p.LabelDisplay
                        : DisplayNames.Period(p.Label);
                    EffectivePeriodQuota effective = QuotaDisplay.Effective(pq, p);
                    e.PercentKnown = effective.PercentKnown;
                    e.Percent = effective.RemainingPercent;
                    e.AmountKnown = effective.AmountKnown;
                    e.RemainingAmount = effective.RemainingAmount;
                    e.ProductSubscribedKnown = pq.SubscribedKnown;
                    e.ProductSubscribed = pq.Subscribed;
                    e.ProductError = pq.Error != null;
                    e.ProductMalformed = pq.Malformed;
                    e.PeriodError = p.Error != null;
                    list.Add(e);
                }
            }
            return list;
        }

        // Entries offered by the settings ComboBox: only products with a known,
        // active subscription are listed. Period-error entries stay listed with
        // an explicit "获取失败" state so nothing is fabricated, but Save refuses
        // to select them. Non-subscribed / unknown-subscription products are not
        // listed at all.
        public static List<FloatingEntry> SelectableCandidates(List<FloatingEntry> all)
        {
            List<FloatingEntry> list = new List<FloatingEntry>();
            if (all == null) return list;
            for (int i = 0; i < all.Count; i++)
            {
                FloatingEntry e = all[i];
                if (e != null && e.ProductSubscribedKnown && e.ProductSubscribed)
                    list.Add(e);
            }
            return list;
        }

        public static FloatingEntry Default(List<FloatingEntry> all)
        {
            if (all == null || all.Count == 0) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].HasTrustedValue) return all[i];
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Selectable) return all[i];
            return null;
        }

        public static FloatingEntry Find(List<FloatingEntry> all, FloatingSettings s)
        {
            if (all == null || s == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                FloatingEntry e = all[i];
                if (e != null
                    && string.Equals(e.ProductKey, s.ProductKey, StringComparison.Ordinal)
                    && string.Equals(e.Label, s.PeriodLabel, StringComparison.Ordinal))
                    return e;
            }
            return null;
        }

        private static string ProductTitle(ProductQuota pq)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(DisplayNames.Product(pq.Product));
            string ed = DisplayNames.Edition(pq.Edition);
            string tier = DisplayNames.Tier(pq.Tier);
            bool teamName = pq.DisplayName != null
                && pq.DisplayName.IndexOf("团队", StringComparison.Ordinal) >= 0;
            if (ed != null && !(teamName && ed == "团队版")) sb.Append(" · ").Append(ed);
            if (tier != null) sb.Append(" · ").Append(tier);
            return sb.ToString();
        }
    }

}
