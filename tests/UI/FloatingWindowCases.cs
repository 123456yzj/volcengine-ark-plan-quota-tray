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
        private static bool ContainsText(System.Windows.Forms.Control root, string text)
        {
            if (root.Text.Contains(text)) return true;
            foreach (System.Windows.Forms.Control c in root.Controls) if (ContainsText(c, text)) return true;
            return false;
        }

        private static FloatingSettingsForm FindOpenSettings()
        {
            foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
                if (f is FloatingSettingsForm) return (FloatingSettingsForm)f;
            return null;
        }

        private static bool ContainsItem(FloatingSettingsForm dlg, string text)
        {
            return IndexOfItem(dlg, text) >= 0;
        }

        private static int IndexOfItem(FloatingSettingsForm dlg, string text)
        {
            for (int i = 0; i < dlg.CandidateCountForTest; i++)
            {
                string s = dlg.CandidateTextForTest(i);
                if (s != null && s.Contains(text)) return i;
            }
            return -1;
        }

        // UX022: feedback is a recorded text cleared by the one-shot timer.
        private static void WaitForCopyRestore(PopupForm form,
            System.Windows.Forms.Timer timer)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (form.CopyFeedbackTextForTest != null && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
        }

        private static System.Windows.Forms.Control FindStatusLabelForTest(
            System.Windows.Forms.Control card)
        {
            foreach (Control c in card.Controls)
                if (c is System.Windows.Forms.Label
                    && (string)c.Tag == "status") return c;
            return null;
        }

        private static void LayoutMathBounds()
        {
            // Small content stays within min/max.
            Check("layout.small", LayoutMath.DesiredHeight(76, 64, 90, 1000), 76 + 90);
            // Large content is capped at 70% of the work area.
            Check("layout.largeCap", LayoutMath.DesiredHeight(76, 5000, 90, 1000), (int)(1000 * 0.7));
            // Never exceeds the work area, even with absurd minimums.
            Check("layout.neverExceeds", LayoutMath.DesiredHeight(900, 900, 900, 800), 800);
            // Degenerate work area still returns at least 1.
            Check("layout.degenerate", LayoutMath.DesiredHeight(0, 0, 0, 0) >= 1, true);
        }

        private static void HideControllerCases()
        {
            // A pure trace: Toggle / ConsumeToggle / Evt / Tick with explicit times.

            // Focus loss queues a pending hide (no immediate decision).
            HideController h = new HideController();
            h.State(true, 1000);
            h.Evt(1000, true);
            Check("hide.evtPending", h.Pending, true);
            Check("hide.beforeDeadline", h.Tick(1100, true), false); // < delay
            Check("hide.atDeadline", h.Tick(1220, true), true);      // now+delay

            // Deactivate-before-mousedown: the focus-loss queues a hide, then the
            // press cancels it and captures the actual (visible) state. The click
            // consumes the capture once.
            HideController d = new HideController();
            d.State(true, 4000);
            d.Evt(4000, true);
            d.Toggle(4005, true);
            Check("hide.deactivateThenToggleClearsPending", d.Pending, false);
            Check("hide.deactivateThenConsumeVisible", d.ConsumeToggle(true), true);
            Check("hide.deactivateThenConsumeReset", d.HasToggle, false);

            // Long press: focus can be lost before mouse-up. While a capture is
            // outstanding Evt must NOT queue a hide, so the panel stays visible.
            HideController lp = new HideController();
            lp.State(true, 5000);
            lp.Toggle(5000, true);
            lp.Evt(5080, true); // focus lost mid-hold
            Check("hide.longPressNoPending", lp.Pending, false);
            Check("hide.longPressTickNoHide", lp.Tick(5300, true), false);
            Check("hide.longPressConsumeStaysVisible", lp.ConsumeToggle(true), true);

            // No mouse-down before the click: ConsumeToggle must use the current
            // visibility, NOT a stale capture from an earlier press.
            HideController nd = new HideController();
            nd.State(true, 6000);
            nd.Toggle(6000, true);          // mouse-down while visible
            nd.ConsumeToggle(true);         // click hides it
            nd.State(false, 6010);          // real hide resets the capture
            Check("hide.noDownHasNoCapture", nd.HasToggle, false);
            // Next click with no mouse-down: live visibility (hidden) => open.
            Check("hide.noDownNextClickUsesVisible", nd.ConsumeToggle(false), false);
            Check("hide.noDownConsumeResets", nd.HasToggle, false);

            // Suppression window: the click's own focus loss within the window is
            // suppressed, and the deferred hide still fires later once the timer
            // is allowed to evaluate (no permanent stuck).
            HideController s = new HideController();
            s.State(true, 7000);
            s.Toggle(7000, true);
            Check("hide.suppressConsume", s.ConsumeToggle(true), true);
            s.Evt(7010, true); // own focus loss after click
            Check("hide.suppressedWithinWindow", s.Tick(7020, true), false);
            Check("hide.deferredAfterSuppress", s.Tick(7300, true), true);

            // Cancel clears pending; a later focus loss still hides (no stuck).
            HideController cn = new HideController();
            cn.State(true, 8000);
            cn.Evt(8000, true);
            cn.Cancel();
            Check("hide.cancelClears", cn.Pending, false);
            cn.Evt(8100, true);
            Check("hide.afterCancelStillWorks", cn.Tick(8400, true), true);

            // Already hidden: no pending, no hide.
            HideController e = new HideController();
            e.State(false, 9000);
            e.Evt(9000, false);
            Check("hide.alreadyHiddenNoPending", e.Pending, false);
        }

        private static void PanelPositionerCases()
        {
            Rectangle wa = new Rectangle(0, 0, 1920, 1040);
            Rectangle b = PanelPositioner.Bounds(wa, 1.0);
            Check("pos.fits", PanelPositioner.FitsInside(b, wa), true);
            Rectangle b2 = PanelPositioner.Bounds(wa, 1.0);
            Check("pos.stable", b, b2);
            Check("pos.bottomRight", b.Right <= wa.Right && b.Bottom <= wa.Bottom, true);

            // Thin work area: must still fit inside.
            Rectangle thin = new Rectangle(0, 0, 300, 260);
            Rectangle bt = PanelPositioner.Bounds(thin, 1.0);
            Check("pos.thinFits", PanelPositioner.FitsInside(bt, thin), true);

            // Loading and result use the same bounds (fixed size).
            Check("pos.widthRange", LayoutMath.ClampWidth(440, 1.0, 1920), 440);
            Check("pos.widthClampSmall", LayoutMath.ClampWidth(440, 1.0, 200), 200);
        }

        // ---- v0.4 floating circle / settings cases ----

        private static void FloatingSelectionCases()
        {
            // Agent Plan amounts 750/300/100 share a 1000 total, so all display 10%.
            QuotaSnapshot snap = SyntheticSample.BuildLarge();
            List<FloatingEntry> entries = FloatingSelection.Build(snap);
            Check("selection.count", entries.Count, 8);
            Check("selection.orderFirst", entries[0].Label, "5h");
            FloatingEntry def = FloatingSelection.Default(entries);
            Check("selection.defaultFirst", def.Label, "5h");
            Check("selection.defaultKnown", def.HasTrustedValue, true);
            CheckClose("selection.defaultPercent", def.Percent, 10.0);

            // Unknown before a known one: default skips to the first trusted value,
            // never the global minimum.
            QuotaSnapshot mixed = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true,
                    Period("weekly", null, null, null, null) + "," + Period("5h", "80", null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", "99", null, null, null))));
            List<FloatingEntry> mixedEntries = FloatingSelection.Build(mixed);
            FloatingEntry mixedDef = FloatingSelection.Default(mixedEntries);
            Check("selection.mixedSkipsUnknown", mixedDef.Label, "5h");
            CheckClose("selection.mixedNotMinimum", mixedDef.Percent, 20.0);

            // All unknown -> first selectable, shown unknown (no fake 0).
            QuotaSnapshot allUnknown = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("weekly", null, null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", null, null, null, null))));
            FloatingEntry unknownDef = FloatingSelection.Default(FloatingSelection.Build(allUnknown));
            Check("selection.allUnknownFirst", unknownDef.Label, "weekly");
            Check("selection.allUnknownNoValue", unknownDef.HasTrustedValue, false);
            Check("selection.allUnknownSelectable", unknownDef.Selectable, true);

            // No products -> no default.
            Check("selection.emptyDefault", FloatingSelection.Default(new List<FloatingEntry>()), null);

            // Unsubscribed / unknown subscription are never candidates.
            QuotaSnapshot unsub = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", false, Period("5h", "10", null, null, null)),
                Item("coding-plan", "personal", null, true, Period("monthly", "40", null, null, null))));
            List<FloatingEntry> unsubEntries = FloatingSelection.Build(unsub);
            List<FloatingEntry> cand = FloatingSelection.SelectableCandidates(unsubEntries);
            Check("selection.nonSubscribedExcluded", cand.Count, 1);
            Check("selection.nonSubscribedLabel", cand[0].Label, "monthly");

            // Period error keeps the entry listed but not selectable.
            QuotaSnapshot errSnap = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("5h", "10", null, null, null))));
            errSnap.Products[0].Periods[0].Error = "该周期查询失败。";
            FloatingEntry errEntry = FloatingSelection.Build(errSnap)[0];
            Check("selection.periodErrorStatus", errEntry.StatusText, "获取失败");
            Check("selection.periodErrorNotSelectable", errEntry.Selectable, false);
            Check("selection.periodErrorNotTrusted", errEntry.HasTrustedValue, false);

            // Product key distinguishes edition / tier (no cross-version collision).
            QuotaSnapshot a = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "small", true, Period("5h", "10", null, null, null))));
            QuotaSnapshot b = QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "large", true, Period("5h", "10", null, null, null))));
            string keyA = FloatingSettingsStore.ProductKey(a.Products[0]);
            string keyB = FloatingSettingsStore.ProductKey(b.Products[0]);
            Check("selection.productKeyDiffersByTier", keyA == keyB, false);
            Check("selection.productKeyContent", keyA, "agent-plan|personal|small");

            // Settings ComboBox shows honest per-item state text.
            Check("selection.comboKnown", entries[0].ComboText.Contains("剩余 10%"), true);
            FloatingEntry unk = FloatingSelection.Build(allUnknown)[0];
            Check("selection.comboUnknown", unk.ComboText.Contains("剩余未知"), true);
            Check("selection.comboError", errEntry.ComboText.Contains("获取失败"), true);

            // Find honours both product key and label.
            FloatingSettings sel = new FloatingSettings {
                Version = 1, ProductKey = entries[0].ProductKey, PeriodLabel = "5h" };
            Check("selection.findSelected", FloatingSelection.Find(entries, sel).Label, "5h");
            sel.PeriodLabel = "weekly";
            Check("selection.findOtherPeriod", FloatingSelection.Find(entries, sel).Label, "weekly");
            sel.PeriodLabel = "missing";
            Check("selection.findMissing", FloatingSelection.Find(entries, sel), null);
        }

        private static void FloatingLayoutCases()
        {
            // Normal right-bottom circle: details go to the LEFT, never covering.
            System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1080);
            System.Drawing.Rectangle circle = new System.Drawing.Rectangle(1768, 928, 136, 136);
            System.Drawing.Rectangle details = new System.Drawing.Rectangle(0, 0, 440, 560);
            FloatingLayout.Choose(circle, details, work);
            Check("layout.normalLeft", FloatingLayout.Choose(circle, details, work), DetailsPlacement.Left);
            System.Drawing.Rectangle L = FloatingLayout.Bounds(DetailsPlacement.Left, circle, details, work);
            Check("layout.normalNoOverlap", L.Right <= circle.Left, true);
            Check("layout.normalInside", CircleWithin(L, work), true);

            // Left edge circle: left does not fit -> fall back to the right.
            System.Drawing.Rectangle leftCircle = new System.Drawing.Rectangle(0, 400, 136, 136);
            Check("layout.leftFallbackRight",
                FloatingLayout.Choose(leftCircle, details, work), DetailsPlacement.Right);

            // Only room above (circle at the bottom, no left/right space).
            System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(200, 0, 460, 1080);
            System.Drawing.Rectangle bottomCircle = new System.Drawing.Rectangle(310, 930, 136, 136);
            Check("layout.aboveFallback",
                FloatingLayout.Choose(bottomCircle, details, narrow), DetailsPlacement.Above);

            // No side fits at all -> temporarily hide the circle.
            System.Drawing.Rectangle tiny = new System.Drawing.Rectangle(0, 0, 300, 300);
            System.Drawing.Rectangle midCircle = new System.Drawing.Rectangle(82, 82, 136, 136);
            Check("layout.noSpaceHide",
                FloatingLayout.Choose(midCircle, details, tiny), DetailsPlacement.HideCircle);

            // Negative-coordinate multi-monitor work area still resolves.
            System.Drawing.Rectangle negWork = new System.Drawing.Rectangle(-1920, 0, 1920, 1080);
            System.Drawing.Rectangle negCircle = new System.Drawing.Rectangle(-288, 928, 136, 136);
            Check("layout.negativeLeft",
                FloatingLayout.Choose(negCircle, details, negWork), DetailsPlacement.Left);
            System.Drawing.Rectangle nl = FloatingLayout.Bounds(DetailsPlacement.Left, negCircle, details, negWork);
            Check("layout.negativeInside", CircleWithin(nl, negWork), true);
        }

        private static bool CircleWithin(System.Drawing.Rectangle b, System.Drawing.Rectangle wa)
        {
            return b.Left >= wa.Left && b.Top >= wa.Top
                && b.Right <= wa.Right && b.Bottom <= wa.Bottom;
        }

        // Exercises the REAL PrepareDetails consumption (not just the
        // FloatingLayout enum): every branch, including HideCircle, must leave
        // PendingDetailsBounds non-zero, containing the details size, inside the
        // work area, and it must survive RestoreAfterDetails only for auto-hide.
        private static void FloatingPrepareDetailsCases()
        {
            System.Drawing.Size details = new System.Drawing.Size(440, 560);

            // Normal case on a wide work area -> Left, non-zero, size kept.
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                form.ShowCircleAtForTest(work, 1.0);
                DetailsPlacement p = form.PrepareDetails(details, work);
                System.Drawing.Rectangle b = form.PendingDetailsBounds;
                Check("prepare.positionLeft", p, DetailsPlacement.Left);
                Check("prepare.nonEmpty", b.Width > 0 && b.Height > 0, true);
                Check("prepare.sizeKept", b.Size, details);
                Check("prepare.inside", CircleWithin(b, work), true);
            }

            // Work area large enough to CONTAIN the details but positioned so
            // every side intersects the circle -> HideCircle, yet the pending
            // bounds must STILL be finite, non-zero and contain the size (this is
            // the original bug: an empty rectangle rendered the panel invisible).
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 500, 700);
                form.ShowCircleAtForTest(narrow, 1.0);
                DetailsPlacement p = form.PrepareDetails(details, narrow);
                System.Drawing.Rectangle b = form.PendingDetailsBounds;
                Check("prepare.narrowHide", p, DetailsPlacement.HideCircle);
                Check("prepare.hideNonEmpty", b.Width > 0 && b.Height > 0, true);
                Check("prepare.hideSizeKept", b.Size, details);
                Check("prepare.hideInside", CircleWithin(b, narrow), true);

                // HideCircle auto-hid the circle; closing details restores it.
                Check("prepare.circleAutoHidden", form.CircleVisible, false);
                form.RestoreAfterDetails();
                Check("prepare.autoRestored", form.CircleVisible, true);
            }

            // Explicit user hide must NOT be restored by a details close.
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 300, 300);
                form.ShowCircleAtForTest(narrow, 1.0);
                form.HideCircle();              // explicit hide first
                form.RestoreAfterDetails();     // details close
                Check("prepare.explicitHideNotRestored", form.CircleVisible, false);

                // After an explicit show, a later auto-hide is still restorable.
                form.ShowCircle();
                Check("prepare.explicitShowVisible", form.CircleVisible, true);
            }

            // ShowCircle cancels the auto-hidden flag (no stale restore).
            using (FloatingQuotaForm form = new FloatingQuotaFormForData(
                SyntheticSample.BuildLarge()))
            {
                System.Drawing.Rectangle narrow = new System.Drawing.Rectangle(0, 0, 300, 300);
                form.ShowCircleAtForTest(narrow, 1.0);
                form.PrepareDetails(details, narrow);
                Check("prepare.autoHiddenBeforeShow", form.CircleVisible, false);
                form.ShowCircle();              // user explicitly shows again
                form.RestoreAfterDetails();     // must not change anything
                Check("prepare.showCancelsAutoFlag", form.CircleVisible, true);
            }
        }

        private static void FloatingDisplayCases()
        {
            // 0 -> "0%", 100 -> "100%", <1 -> "<1%", 99.6 does not fake 100.
            Check("circle.zero", FloatingQuotaForm.CirclePercent(0), "0%");
            Check("circle.full", FloatingQuotaForm.CirclePercent(100), "100%");
            Check("circle.sub1", FloatingQuotaForm.CirclePercent(0.4), "<1%");
            Check("circle.half", FloatingQuotaForm.CirclePercent(50), "50%");
            Check("circle.noFakeFull", FloatingQuotaForm.CirclePercent(99.6), "99%");
            FloatingAmountCases();
        }

        private static void FloatingWindowCases()
        {
            // Injection avoids any real preference IO.
            using (FloatingQuotaForm floating = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; }))
            {
                IntPtr h = floating.Handle; GC.KeepAlive(h);
                Check("floating.noEntryNoData", floating.EntryCountForTest, 0);
                FloatingDisplay empty = new FloatingDisplay();
                empty.PercentText = "暂无数据";
                empty.HasData = false;
                floating.ApplyModelView(EmptyView());
                Check("floating.emptyNoData", floating.EntryCountForTest, 0);

                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                PanelView view = model.OnUsageResult(
                    SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
                floating.ApplyModelView(view);
                Check("floating.entryCount", floating.EntryCountForTest, 8);
                FloatingEntry selected = floating.SelectedEntryForTest;
                Check("floating.selectedFirst", selected.Label, "5h");
                Check("floating.selectedPercentText", floating.CircleForTest.PercentTextForTest, "10%");
                Check("floating.selectedTrusted", floating.CircleForTest.PercentKnownForTest, true);
                Check("floating.defaultNotStored", floating.StoredSettingsForTest, null);
                Check("floating.amountCaption", floating.CircleForTest.AmountTextForTest, "100 AFP");
                Check("floating.tooltipIncludesProduct",
                    floating.CircleForTest.TooltipForTest.Contains("Agent Plan"), true);
                Check("floating.tooltipIncludesPeriod",
                    floating.CircleForTest.TooltipForTest.Contains("5 小时"), true);

                // Tooltip surfaces the PanelView's own safe freshness status and
                // never fabricates a confirmed update for a limited result.
                PanelView restricted = new PanelView();
                restricted.Data = SyntheticSample.BuildLarge();
                restricted.Note = "身份未完全确认";
                restricted.IdentityUnknown = true;
                restricted.State = PanelState.ShowingCurrent;
                restricted.FromCache = false;
                floating.ApplyModelView(restricted);
                Check("floating.tooltipNote",
                    floating.CircleForTest.TooltipForTest.Contains("身份未完全确认"), true);
                PanelView cached = new PanelView();
                cached.Data = SyntheticSample.BuildLarge();
                cached.FromCache = true;
                cached.Note = "上次更新数据";
                cached.State = PanelState.ShowingCurrent;
                floating.ApplyModelView(cached);
                Check("floating.tooltipCache",
                    floating.CircleForTest.TooltipForTest.Contains("上次更新数据"), true);
                // No raw identity / credential text can appear.
                Check("floating.tooltipNoId",
                    floating.CircleForTest.TooltipForTest.Contains("owner_trn"), false);

                // Unknown percent -> neutral, wave stopped, no fake 0.
                FloatingQuotaForm unknownForm = new FloatingQuotaFormForData(AllUnknownSnapshot());
                Check("floating.unknownText", unknownForm.CircleForTest.PercentTextForTest, "剩余未知");
                Check("floating.unknownKnownFlag", unknownForm.CircleForTest.PercentKnownForTest, false);
                Check("floating.unknownWaveStopped", unknownForm.CircleForTest.WaveRunningForTest, false);
                unknownForm.Dispose();

                // A real drag (production mouse handlers) over the threshold must
                // NOT open details; a click must. The drag clamp targets the real
                // screen work area (Screen.FromPoint), so use that as the bound.
                System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                floating.ShowCircleAtForTest(area, 1.0);
                FloatingCircleControl circle = floating.CircleForTest;
                int opened = 0;
                circle.DetailsRequested += delegate { opened++; };
                Check("floating.circleInitialInside", CircleWithin(circle.Bounds, area), true);
                System.Drawing.Point start = circle.Bounds.Location;
                System.Drawing.Point p0 = new System.Drawing.Point(start.X + 20, start.Y + 20);
                // Small move (< 4dp threshold): treated as a click.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 2, p0.Y + 2));
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 2, p0.Y + 2));
                Check("floating.clickOpens", opened, 1);
                Check("floating.clickBoundsUnchanged", circle.Bounds.Location, start);

                // Large move (> threshold): drag, no details, still inside the area.
                circle.SimulateMouseDownForTest(p0);
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("floating.dragMoved", circle.Bounds.Location != start, true);
                Check("floating.dragInside", CircleWithin(circle.Bounds, area), true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(p0.X + 60, p0.Y + 40));
                Check("floating.dragNoDetails", opened, 1);

                // Drag beyond the right/bottom edge clamps inside the work area.
                circle.SimulateMouseDownForTest(new System.Drawing.Point(
                    circle.Bounds.Left + 10, circle.Bounds.Top + 10));
                circle.SimulateMouseMoveForTest(new System.Drawing.Point(5000, 5000));
                Check("floating.dragClamped", CircleWithin(circle.Bounds, area), true);
                circle.SimulateMouseUpForTest(new System.Drawing.Point(5000, 5000));

                // Selection missing -> "暂无数据", stored choice retained.
                FloatingQuotaForm missing = new FloatingQuotaFormForData(
                    SyntheticSample.BuildLarge(), new FloatingSettings {
                        Version = 1, ProductKey = "ghost|none|none", PeriodLabel = "5h" });
                Check("floating.missingNoData",
                    missing.CircleForTest.PercentTextForTest, "暂无数据");
                Check("floating.missingKeepsChoice",
                    missing.StoredSettingsForTest.ProductKey, "ghost|none|none");
                missing.Dispose();

                // Drifted position is preserved on re-show within the session.
                floating.ShowCircle();
                System.Drawing.Rectangle moved = new System.Drawing.Rectangle(
                    floating.CircleForTest.Bounds.Left + 30,
                    floating.CircleForTest.Bounds.Top + 30,
                    floating.CircleForTest.Width, floating.CircleForTest.Height);
                floating.CircleForTest.Bounds = moved;
                floating.HideCircleForTest();
                floating.ShowCircle();
                Check("floating.dragPositionKept",
                    floating.CircleForTest.Bounds.Location, moved.Location);

                // Identity clear (data evicted) syncs the circle to "暂无数据".
                PanelModel cleared = new PanelModel();
                cleared.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
                floating.ApplyModelView(cleared.CurrentView);
                Check("floating.identityClearNoData",
                    floating.CircleForTest.PercentTextForTest, "暂无数据");
                Check("floating.identityClearEntries", floating.EntryCountForTest, 0);

                // Partial error: a period-error entry is listed but not trusted;
                // the default skips it for the first valid known percentage.
                QuotaSnapshot partial = QuotaParser.Parse(Wrap(
                    Item("agent-plan", "personal", "medium", true,
                        Period("5h", "50", null, null, null))));
                partial.Products[0].Periods[0].Error = "该周期查询失败。";
                partial.Products.Add(QuotaParser.Parse(Wrap(
                    Item("coding-plan", "personal", null, true,
                        Period("monthly", "30", null, null, null)))).Products[0]);
                partial.Status = QuotaStatus.PartialError;
                List<FloatingEntry> pe = FloatingSelection.Build(partial);
                FloatingEntry peDef = FloatingSelection.Default(pe);
                Check("floating.partialDefaultSkipsError", peDef.Label, "monthly");
                Check("floating.partialErrorListedWithState", pe[0].StatusText, "获取失败");
                FloatingQuotaForm partialForm = new FloatingQuotaFormForData(partial);
                Check("floating.partialShowsKnown",
                    partialForm.CircleForTest.PercentTextForTest, "70%");
                partialForm.Dispose();

                // 0% and 100% are exact values but the wave animation only runs
                // for 0 < p < 100 (empty / full are static).
                FloatingQuotaForm z = new FloatingQuotaFormForData(PercentSnapshot(100));
                Check("floating.zeroExact", z.CircleForTest.PercentTextForTest, "0%");
                Check("floating.zeroWaveStopped", z.CircleForTest.WaveRunningForTest, false);
                z.HideCircleForTest();
                z.Dispose();
                FloatingQuotaForm full = new FloatingQuotaFormForData(PercentSnapshot(0));
                Check("floating.fullExact", full.CircleForTest.PercentTextForTest, "100%");
                Check("floating.fullWaveStopped", full.CircleForTest.WaveRunningForTest, false);
                full.HideCircleForTest();
                full.Dispose();

                // NaN / Infinity are never rendered as a trusted value.
                QuotaSnapshot nan = PercentSnapshot(50);
                nan.Products[0].Periods[0].RemainingPercent = double.NaN;
                Check("floating.nanUntrusted",
                    FloatingSelection.Build(nan)[0].HasTrustedValue, false);
                QuotaSnapshot inf = PercentSnapshot(50);
                inf.Products[0].Periods[0].RemainingPercent = double.PositiveInfinity;
                Check("floating.infUntrusted",
                    FloatingSelection.Build(inf)[0].HasTrustedValue, false);
            }
        }

    }
}
