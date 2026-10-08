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
        private static void FloatingAmountCases()
        {
            QuotaSnapshot snap = new QuotaSnapshot();
            ProductQuota product = new ProductQuota { Product = "agent-plan",
                SubscribedKnown = true, Subscribed = true };
            PeriodQuota period = EffectivePeriod("5h", 9703.75, 10000);
            product.Periods.Add(period);
            snap.Products.Add(product);
            PanelView view = new PanelView { Data = snap, State = PanelState.ShowingCurrent };
            Rectangle area = Screen.PrimaryScreen.WorkingArea;

            using (FloatingQuotaForm floating = new FloatingQuotaFormForData(snap))
            {
                FloatingCircleControl circle = floating.CircleForTest;
                circle.SetReduceMotion(true);
                circle.ShowAt(area, 1.0);
                Check("amount.normal.percent", circle.PercentTextForTest, "97%");
                Check("amount.normal.text", circle.AmountTextForTest, "9703.75 AFP");
                Check("amount.normal.fitted", circle.FittedAmountTextForTest, "9703.75 AFP");
                Check("amount.normal.tooltipProduct", circle.TooltipForTest.Contains("Agent Plan"), true);
                Check("amount.normal.tooltipPeriod", circle.TooltipForTest.Contains("5 小时"), true);

                period.RemainingAmount = 0;
                floating.ApplyModelView(view);
                Check("amount.zero.known", floating.SelectedEntryForTest.AmountKnown, true);
                Check("amount.zero.percent", circle.PercentTextForTest, "0%");
                Check("amount.zero.text", circle.AmountTextForTest, "0 AFP");

                period.AmountKnown = false;
                period.RemainingAmount = 9703.75;
                period.RemainingPercent = 97;
                floating.ApplyModelView(view);
                Check("amount.unknown.entry", floating.SelectedEntryForTest.AmountKnown, false);
                Check("amount.unknown.percentStillKnown", circle.PercentTextForTest, "97%");
                Check("amount.unknown.text", circle.AmountTextForTest, "AFP 未知");
                period.PercentKnown = false;
                floating.ApplyModelView(view);
                Check("amount.bothUnknown.percent", circle.PercentTextForTest, "剩余未知");
                Check("amount.bothUnknown.text", circle.AmountTextForTest, "AFP 未知");

                period.AmountKnown = true;
                period.TotalKnown = false;
                floating.ApplyModelView(view);
                Check("amount.percentUnknown.percent", circle.PercentTextForTest, "剩余未知");
                Check("amount.percentUnknown.text", circle.AmountTextForTest, "9703.75 AFP");
                period.TotalKnown = true;

                product.Periods.Add(EffectivePeriod("weekly", 9000, 20000));
                product.Periods.Add(EffectivePeriod("monthly", 1234.5, 30000));
                floating.ApplyModelView(view);
                FloatingEntry entry = floating.SelectedEntryForTest;
                Check("amount.effective.selected", entry.Label, "5h");
                CheckClose("amount.effective.value", entry.RemainingAmount, 1234.5);
                CheckClose("amount.effective.percent", entry.Percent, 12.345);
                Check("amount.effective.text", circle.AmountTextForTest, "1234.5 AFP");
                Check("amount.effective.percentText", circle.PercentTextForTest, "12%");
                CheckClose("amount.effective.rawUnchanged", period.RemainingAmount, 9703.75);

                FloatingSettings weekly = new FloatingSettings { Version = 1,
                    ProductKey = FloatingSettingsStore.ProductKey(product), PeriodLabel = "weekly" };
                using (FloatingQuotaForm selected = new FloatingQuotaFormForData(snap, weekly))
                {
                    Check("amount.selected.period", selected.SelectedEntryForTest.Label, "weekly");
                    Check("amount.selected.percent", selected.CircleForTest.PercentTextForTest, "6%");
                    Check("amount.selected.text", selected.CircleForTest.AmountTextForTest, "1234.5 AFP");
                    product.Periods[1].Error = "该周期查询失败。";
                    selected.ApplyModelView(view);
                    Check("amount.error.text", selected.CircleForTest.AmountTextForTest, "AFP 未知");
                    Check("amount.error.percent", selected.CircleForTest.PercentTextForTest, "剩余未知");
                }
                product.Periods.RemoveRange(1, 2);

                foreach (double amount in new double[] { 9703.75, 123456789012345.67, double.MaxValue })
                {
                    period.RemainingAmount = amount;
                    period.Total = amount / 0.97;
                    period.RemainingPercent = 97;
                    period.PercentKnown = true;
                    floating.ApplyModelView(view);
                    foreach (double scale in new double[] { 1.0, 1.5, 2.0 })
                    {
                        circle.ShowAt(area, scale);
                        string tag = "amount.fit." + amount + "." + scale;
                        string text = circle.FittedAmountTextForTest;
                        Rectangle bounds = circle.AmountBoundsForTest;
                        Check(tag + ".width", TextRenderer.MeasureText(text, circle.AmountFontForTest).Width
                            <= bounds.Width - 2, true);
                        Check(tag + ".height", circle.AmountFontForTest.Height <= bounds.Height, true);
                        Check(tag + ".topLeftInside", circle.Region.IsVisible(bounds.Left, bounds.Top), true);
                        Check(tag + ".bottomRightInside", circle.Region.IsVisible(bounds.Right, bounds.Bottom), true);
                        Check(tag + ".scientific", text.Contains("E+"), amount > 1e12);
                        Check(tag + ".unit", text.EndsWith(" AFP", StringComparison.Ordinal), true);
                        Check(tag + ".fullTooltip", circle.TooltipForTest.Contains(
                            DisplayNames.Number(amount) + " AFP"), true);
                        using (Bitmap bitmap = new Bitmap(circle.Width, circle.Height))
                        {
                            circle.DrawToBitmap(bitmap, circle.ClientRectangle);
                            bool amountInk = false, oldCaptionInk = false;
                            for (int y = bounds.Top; y < circle.Height; y++)
                                for (int x = 0; x < circle.Width; x++)
                                {
                                    Color pixel = bitmap.GetPixel(x, y);
                                    // Antialiasing blends glyph coverage with the water.
                                    if (pixel.A == 255
                                        && Math.Abs(pixel.R - UiStyle.CircleCaption.R) <= 30
                                        && Math.Abs(pixel.G - UiStyle.CircleCaption.G) <= 30
                                        && Math.Abs(pixel.B - UiStyle.CircleCaption.B) <= 30)
                                    {
                                        if (bounds.Contains(x, y)) amountInk = true;
                                        else oldCaptionInk = true;
                                    }
                                }
                            Check(tag + ".painted", amountInk, true);
                            Check(tag + ".noExtraCaption", oldCaptionInk, false);
                            if (scale == 1.0)
                                bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                    amount == 9703.75 ? "preview-floating-amount.png"
                                        : amount == double.MaxValue ? "preview-floating-amount-max.png"
                                        : "preview-floating-amount-long.png"));
                        }
                    }
                }
                floating.ApplyModelView(EmptyView());
                Check("amount.noData.cleared", circle.AmountTextForTest, "");
            }

            int queries = 0;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); }))
            {
                foreach (double amount in new double[] { 9703.75, 0, double.MaxValue })
                {
                    period.AmountKnown = true;
                    period.RemainingAmount = amount;
                    app.ApplyViewForTest(snap);
                    Check("amount.query.updated", app.CircleForTest.AmountTextForTest,
                        DisplayNames.Number(amount) + " AFP");
                }
                period.AmountKnown = false;
                app.ApplyViewForTest(snap);
                Check("amount.query.unknown", app.CircleForTest.AmountTextForTest, "AFP 未知");
                app.OpenEntryForTest("tray");
                using (Bitmap bitmap = new Bitmap(app.CircleForTest.Width, app.CircleForTest.Height))
                    app.CircleForTest.DrawToBitmap(bitmap, app.CircleForTest.ClientRectangle);
                Check("amount.query.zero", queries, 0);
            }
        }

        private static void EffectiveQuotaCases()
        {
            ProductQuota p = new ProductQuota();
            PeriodQuota five = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota week = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota month = EffectivePeriod("monthly", 8000, 16000);
            p.Periods.Add(five); p.Periods.Add(week); p.Periods.Add(month);
            EffectivePeriodQuota f = QuotaDisplay.Effective(p, five);
            EffectivePeriodQuota w = QuotaDisplay.Effective(p, week);
            EffectivePeriodQuota m = QuotaDisplay.Effective(p, month);
            CheckClose("effective.min.5h", f.RemainingAmount, 2000);
            CheckClose("effective.min.week", w.RemainingAmount, 2000);
            CheckClose("effective.min.month", m.RemainingAmount, 8000);
            CheckClose("effective.percent.5h", f.RemainingPercent, 20);
            CheckClose("effective.percent.week", w.RemainingPercent, 50);
            CheckClose("effective.percent.month", m.RemainingPercent, 50);
            CheckClose("effective.raw.unchanged", five.RemainingAmount, 5000);

            month.RemainingAmount = 1000;
            CheckClose("effective.lower.min", QuotaDisplay.Effective(p, five).RemainingAmount, 1000);
            CheckClose("effective.lower.percent", QuotaDisplay.Effective(p, five).RemainingPercent, 10);
            CheckClose("effective.lower.doesNotAffectWeekly",
                QuotaDisplay.Effective(p, week).RemainingAmount, 1000);
            CheckClose("effective.lower.doesNotAffectMonthly",
                QuotaDisplay.Effective(p, month).RemainingAmount, 1000);

            PeriodQuota error = EffectivePeriod("error", 1, 100);
            error.Error = "failed";
            p.Periods.Add(error);
            EffectivePeriodQuota failed = QuotaDisplay.Effective(p, error);
            Check("effective.error.amountUnknown", failed.AmountKnown, false);
            Check("effective.error.percentUnknown", failed.PercentKnown, false);
            CheckClose("effective.error.ignoredMinimum",
                QuotaDisplay.Effective(p, five).RemainingAmount, 1000);

            ProductQuota shuffled = new ProductQuota();
            PeriodQuota shuffledMonth = EffectivePeriod("monthly", 8000, 16000);
            PeriodQuota shuffledFive = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota shuffledWeek = EffectivePeriod("weekly", 2000, 4000);
            shuffled.Periods.Add(shuffledMonth); shuffled.Periods.Add(shuffledFive);
            shuffled.Periods.Add(shuffledWeek);
            CheckClose("effective.shuffled.5h", QuotaDisplay.Effective(
                shuffled, shuffledFive).RemainingAmount, 2000);
            CheckClose("effective.shuffled.week", QuotaDisplay.Effective(
                shuffled, shuffledWeek).RemainingAmount, 2000);
            CheckClose("effective.shuffled.month", QuotaDisplay.Effective(
                shuffled, shuffledMonth).RemainingAmount, 8000);

            ProductQuota hierarchy = new ProductQuota();
            PeriodQuota hFive = EffectivePeriod("5h", 100, 100);
            PeriodQuota hWeek = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota hMonth = EffectivePeriod("monthly", 8000, 16000);
            hierarchy.Periods.Add(hFive); hierarchy.Periods.Add(hWeek); hierarchy.Periods.Add(hMonth);
            CheckClose("effective.ownBelowUpper.5h", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 100);
            CheckClose("effective.ownBelowUpper.week", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            hFive.RemainingAmount = 5000;
            hWeek.RemainingAmount = 8000;
            hMonth.RemainingAmount = 2000;
            CheckClose("effective.upperLimits.5h", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 2000);
            CheckClose("effective.upperLimits.week", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            CheckClose("effective.upperLimits.month", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hFive.RemainingAmount = 0;
            CheckClose("effective.zero.lowerDoesNotAffectWeekly", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 2000);
            CheckClose("effective.zero.lowerDoesNotAffectMonthly", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hFive.RemainingAmount = 5000;
            hWeek.RemainingAmount = 0;
            CheckClose("effective.zero.weekLimitsFive", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 0);
            CheckClose("effective.zero.weekDoesNotAffectMonthly", QuotaDisplay.Effective(
                hierarchy, hMonth).RemainingAmount, 2000);

            hWeek.RemainingAmount = 8000;
            hMonth.RemainingAmount = 0;
            CheckClose("effective.zero.monthLimitsFive", QuotaDisplay.Effective(
                hierarchy, hFive).RemainingAmount, 0);
            CheckClose("effective.zero.monthLimitsWeekly", QuotaDisplay.Effective(
                hierarchy, hWeek).RemainingAmount, 0);

            ProductQuota unknownUpper = new ProductQuota();
            PeriodQuota uFive = EffectivePeriod("5h", 5000, 10000);
            PeriodQuota uWeek = EffectivePeriod("weekly", 2000, 4000);
            PeriodQuota uMonth = EffectivePeriod("monthly", 8000, 16000);
            uWeek.Error = "failed";
            unknownUpper.Periods.Add(uFive); unknownUpper.Periods.Add(uWeek); unknownUpper.Periods.Add(uMonth);
            CheckClose("effective.error.upperIgnored", QuotaDisplay.Effective(
                unknownUpper, uFive).RemainingAmount, 5000);
            uWeek.Error = null; uWeek.RemainingAmount = double.NaN;
            CheckClose("effective.invalid.upperIgnored", QuotaDisplay.Effective(
                unknownUpper, uFive).RemainingAmount, 5000);
            PeriodQuota unknownLabel = EffectivePeriod("other", 0, 100);
            unknownUpper.Periods.Add(unknownLabel);
            CheckClose("effective.unknownLabel.selfOnly", QuotaDisplay.Effective(
                unknownUpper, unknownLabel).RemainingAmount, 0);
            unknownLabel.AmountKnown = false;
            Check("effective.unknownLabel.ownUnknown", QuotaDisplay.Effective(
                unknownUpper, unknownLabel).AmountKnown, false);

            QuotaSnapshot cacheSnap = new QuotaSnapshot();
            ProductQuota cacheProduct = new ProductQuota();
            cacheProduct.Product = "agent-plan"; cacheProduct.SubscribedKnown = true;
            cacheProduct.Subscribed = true;
            cacheProduct.Periods.Add(five); cacheProduct.Periods.Add(week);
            cacheProduct.Periods.Add(month);
            cacheSnap.Products.Add(cacheProduct);
            string fingerprint = QueryScope.FromAuth(PopupForm.SampleIdentity()).Fingerprint;
            CachedSnapshot cache = PersistentStateStore.Decode(PersistentStateStore.Encode(
                PersistentStateStore.ToCache(cacheSnap, fingerprint)));
            QuotaSnapshot restored = PersistentStateStore.FromCache(cache);
            CheckClose("effective.cache.rawFive", restored.Products[0].Periods[0].RemainingAmount, 5000);
            CheckClose("effective.cache.rawMonth", restored.Products[0].Periods[2].RemainingAmount, 1000);
            CheckClose("effective.cache.display", QuotaDisplay.Effective(
                restored.Products[0], restored.Products[0].Periods[0]).RemainingAmount, 1000);

            foreach (double bad in new double[] { double.NaN, double.PositiveInfinity,
                double.NegativeInfinity, -1 })
            {
                PeriodQuota invalid = EffectivePeriod("bad", bad, 100);
                p.Periods.Add(invalid);
                Check("effective.invalid.amountUnknown." + bad,
                    QuotaDisplay.Effective(p, invalid).AmountKnown, false);
                CheckClose("effective.invalid.minimum." + bad,
                    QuotaDisplay.Effective(p, five).RemainingAmount, 1000);
            }

            QuotaSnapshot snap = new QuotaSnapshot();
            p.Product = "agent-plan"; p.DisplayName = "Agent Plan";
            p.SubscribedKnown = true; p.Subscribed = true;
            snap.Products.Add(p);
            PanelView view = new PanelView(); view.Data = snap;
            Check("effective.summary.minimum", QuotaSummary.Build(view).Contains(
                "5 小时：剩余 10%，可用剩余 1000 额度"), true);
            CheckClose("effective.circle.minimum", FloatingSelection.Build(snap)[0].Percent, 10);

            PeriodQuota zero = EffectivePeriod("zero", 0, 100);
            p.Periods.Add(zero);
            CheckClose("effective.zero", QuotaDisplay.Effective(p, zero).RemainingAmount, 0);
            CheckClose("effective.zero.unknownDoesNotAffectFive", QuotaDisplay.Effective(
                p, five).RemainingAmount, 1000);
            CheckClose("effective.zero.unknownDoesNotAffectWeekly", QuotaDisplay.Effective(
                p, week).RemainingPercent, 25);

            PeriodQuota unknown = new PeriodQuota();
            unknown.Label = "unknown";
            p.Periods.Add(unknown);
            EffectivePeriodQuota u = QuotaDisplay.Effective(p, unknown);
            Check("effective.unknown.amount", u.AmountKnown, false);
            Check("effective.unknown.percent", u.PercentKnown, false);

            ProductQuota other = new ProductQuota();
            PeriodQuota otherPeriod = EffectivePeriod("monthly", 9000, 10000);
            other.Periods.Add(otherPeriod);
            CheckClose("effective.product.isolated",
                QuotaDisplay.Effective(other, otherPeriod).RemainingAmount, 9000);

            PeriodQuota invalidTotal = EffectivePeriod("raw", 50, 100);
            invalidTotal.Total = 0; invalidTotal.TotalKnown = true;
            p.Periods.Add(invalidTotal);
            CheckClose("effective.invalidTotal.rawPercent",
                QuotaDisplay.Effective(p, invalidTotal).RemainingPercent, 50);

        }

    }
}
