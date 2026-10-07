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
        private static void PercentWins()
        {
            string json = Wrap(Item("agent-plan", "personal", "medium", true,
                Period("5h", "25", null, null, "2026-10-03T14:43:59+08:00")));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("percent25.known", p.PercentKnown, true);
            CheckClose("percent25.remaining=75", p.RemainingPercent, 75.0);
            Check("percent25.amountUnknown", p.AmountKnown, false);
            Check("percent25.statusOk", s.Status, QuotaStatus.Ok);
        }

        private static void ZeroIsValid()
        {
            string json = Wrap(Item("agent-plan", null, null, true,
                Period("monthly", "0", "0", "1000", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("zero.usedKnown", p.UsedKnown, true);
            CheckClose("zero.used=0", p.Used, 0.0);
            Check("zero.percentKnown", p.PercentKnown, true);
            CheckClose("zero.remaining=100", p.RemainingPercent, 100.0);
            Check("zero.amountKnown", p.AmountKnown, true);
            CheckClose("zero.amount=1000", p.RemainingAmount, 1000.0);
        }

        private static void MissingIsUnknown()
        {
            string json = Wrap(Item("coding-plan", "personal", null, true,
                Period("monthly", null, null, null, null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("missing.percentUnknown", p.PercentKnown, false);
            Check("missing.usedUnknown", p.UsedKnown, false);
            Check("missing.totalUnknown", p.TotalKnown, false);
            Check("missing.amountUnknown", p.AmountKnown, false);
        }

        private static void UsedTotalFallback()
        {
            string json = Wrap(Item("agent-plan", null, null, true,
                Period("weekly", null, "250", "1000", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("fallback.known", p.PercentKnown, true);
            CheckClose("fallback.remaining=75", p.RemainingPercent, 75.0);
            CheckClose("fallback.amount=750", p.RemainingAmount, 750.0);
        }

        private static void TotalZero()
        {
            string json = Wrap(Item("coding-plan", null, null, true,
                Period("monthly", null, "50", "0", null)));
            QuotaSnapshot s = Parse(json);
            PeriodQuota p = s.Products[0].Periods[0];
            Check("total0.percentUnknown", p.PercentKnown, false);
            Check("total0.amountUnknown", p.AmountKnown, false);

            // total 0 but a valid percent: trust percent.
            string json2 = Wrap(Item("coding-plan", null, null, true,
                Period("monthly", "30", null, "0", null)));
            QuotaSnapshot s2 = Parse(json2);
            PeriodQuota p2 = s2.Products[0].Periods[0];
            Check("total0percent.known", p2.PercentKnown, true);
            CheckClose("total0percent.remaining=70", p2.RemainingPercent, 70.0);
        }

        private static void OutOfBoundsClamp()
        {
            string high = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "130", null, null, null)));
            PeriodQuota ph = Parse(high).Products[0].Periods[0];
            Check("clamp.high=0", ph.PercentKnown, true);
            CheckClose("clamp.high.remaining=0", ph.RemainingPercent, 0.0);
            Check("clamp.high.flag", ph.Clamped, true);

            string low = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "-3", null, null, null)));
            PeriodQuota pl = Parse(low).Products[0].Periods[0];
            CheckClose("clamp.low.remaining=100", pl.RemainingPercent, 100.0);
            Check("clamp.low.flag", pl.Clamped, true);
        }

        private static void MalformedAndShape()
        {
            Check("malformed.notjson", QuotaParser.Parse("not-json{").Status, QuotaStatus.FormatError);
            Check("malformed.rootArray", QuotaParser.Parse("[1,2,3]").Status, QuotaStatus.FormatError);
            Check("malformed.noItems", QuotaParser.Parse("{\"viewer\":{}}").Status, QuotaStatus.FormatError);
            Check("malformed.itemsType", QuotaParser.Parse("{\"items\":{\"a\":1}}").Status, QuotaStatus.FormatError);
            Check("malformed.empty", QuotaParser.Parse("").Status, QuotaStatus.FormatError);
        }

        private static void PartialErrors()
        {
            string errItem = "{\"product\":\"coding-plan\",\"subscribed\":true,\"error\":\"boom\"}";
            string okItem = Item("agent-plan", null, null, true, Period("5h", "10", null, null, null));
            string json = Wrap(okItem, errItem);
            QuotaSnapshot s = Parse(json);
            Check("partial.status", s.Status, QuotaStatus.PartialError);
            Check("partial.count=2", s.Products.Count, 2);
            bool foundErr = false;
            for (int i = 0; i < s.Products.Count; i++)
                if (s.Products[i].Error != null) foundErr = true;
            Check("partial.errorPresent", foundErr, true);
        }

        private static void UnsubscribedAndEmpty()
        {
            string json = Wrap(Item("agent-plan", null, null, false,
                Period("5h", "10", null, null, null)));
            QuotaSnapshot s = Parse(json);
            Check("unsub.statusNoSub", s.Status, QuotaStatus.NoSubscription);
            Check("unsub.hidden", s.Products.Count, 0);

            QuotaSnapshot e = QuotaParser.Parse("{\"items\":[]}");
            Check("empty.statusNoSub", e.Status, QuotaStatus.NoSubscription);
            Check("empty.count", e.Products.Count, 0);
        }

        private static void ErrorBucketNotUnsubscribed()
        {
            // An item-level error with subscribed=false must still be shown as an error,
            // never silently hidden as "unsubscribed".
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":false,\"error\":\"nope\"}]}";
            QuotaSnapshot s = Parse(json);
            Check("errBucket.count=1", s.Products.Count, 1);
            Check("errBucket.status", s.Status, QuotaStatus.PartialError);
            Check("errBucket.hasError", s.Products[0].Error != null, true);
        }

        private static void MissingSubscribedIsMalformed()
        {
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"periods\":[]}]}";
            QuotaSnapshot s = Parse(json);
            Check("missingSub.count=1", s.Products.Count, 1);
            Check("missingSub.malformed", s.Products[0].Malformed, true);
            Check("missingSub.notNoSub", s.Status != QuotaStatus.NoSubscription, true);
        }

        private static void NullItemAndNullPeriod()
        {
            // A null item must be an error, not silently skipped (which would
            // look like "no subscription").
            QuotaSnapshot s = QuotaParser.Parse("{\"items\":[null]}");
            Check("nullItem.count=1", s.Products.Count, 1);
            Check("nullItem.malformed", s.Products[0].Malformed, true);
            Check("nullItem.notNoSub", s.Status != QuotaStatus.NoSubscription, true);
            Check("nullItem.partial", s.Status, QuotaStatus.PartialError);

            // A null period bucket must be surfaced as a period error too.
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true,\"periods\":[null]}]}";
            QuotaSnapshot s2 = Parse(json);
            Check("nullPeriod.count=1", s2.Products[0].Periods.Count, 1);
            Check("nullPeriod.error", s2.Products[0].Periods[0].Error != null, true);
            Check("nullPeriod.flag", s2.Products[0].PeriodErrorPresent, true);
            Check("nullPeriod.partial", s2.Status, QuotaStatus.PartialError);
        }

        private static void PeriodErrorPartial()
        {
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true,\"periods\":[" +
                          "{\"label\":\"5h\",\"percent\":10}," +
                          "{\"label\":\"weekly\",\"error\":\"upstream\"}]}]}";
            QuotaSnapshot s = Parse(json);
            Check("periodErr.status", s.Status, QuotaStatus.PartialError);
            Check("periodErr.flag", s.Products[0].PeriodErrorPresent, true);
            Check("periodErr.good", s.Products[0].Periods[0].PercentKnown, true);
            Check("periodErr.bad", s.Products[0].Periods[1].Error != null, true);
        }

        private static void ItemUpdatedAt()
        {
            string json = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                          "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                          "\"updated_at\":\"2026-10-03T10:00:00+08:00\"}]}";
            QuotaSnapshot s = Parse(json);
            Check("updatedAt.has", s.Products[0].HasUpdated, true);

            // Numeric epoch ms must parse; absurd values must not crash.
            string json2 = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                           "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                           "\"updated_at\":1790998371000}]}";
            QuotaSnapshot s2 = Parse(json2);
            Check("updatedAt.epoch", s2.Products[0].HasUpdated, true);

            string json3 = "{\"items\":[{\"product\":\"coding-plan\",\"subscribed\":true," +
                           "\"periods\":[{\"label\":\"monthly\",\"percent\":40}]," +
                           "\"updated_at\":99999999999999999999}]}";
            QuotaSnapshot s3 = Parse(json3);
            Check("updatedAt.hugeNoCrash", s3.Status, QuotaStatus.Ok);
            Check("updatedAt.hugeUnknown", s3.Products[0].HasUpdated, false);
        }

        private static void ErrorTextNotLeaked()
        {
            // Raw upstream error text must never be surfaced to the user.
            string json = "{\"items\":[{\"product\":\"agent-plan\",\"subscribed\":true," +
                          "\"error\":\"token=secret123 bearer abcdef\"}]}";
            QuotaSnapshot s = Parse(json);
            string err = s.Products[0].Error;
            Check("errText.notNull", err != null, true);
            Check("errText.noSecret", err.IndexOf("secret123", StringComparison.Ordinal) < 0, true);
            Check("errText.noBearer", err.IndexOf("bearer", StringComparison.OrdinalIgnoreCase) < 0, true);
        }

    }
}
