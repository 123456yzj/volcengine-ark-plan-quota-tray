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
        // ---- v0.14 UX022: card-only popup (T052) ----

        // Extra coverage beyond the migrated chrome / copy / shortcut cases:
        // multi-card stacking without a tail gap, real two-pass overflow with
        // a reachable last card, the re-anchor consume semantics after a
        // visible size change, the in-place view-level status row inside the
        // first card, focus landing on the first card, and the rounded Region
        // only for the single non-scrolling card shape. All synthetic; zero
        // query, zero clipboard.
        private static void CompactUiCases()
        {
            ModernBlueVisualCases();
            QuotaBar disposedBar = null;
            using (PopupForm compact = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.BuildLarge();
                sample.Products.RemoveRange(1, sample.Products.Count - 1);
                compact.ForceRender(sample);
                compact.ShowPanel();
                compact.SetScaleForTest(1.0);
                Application.DoEvents();
                Check("compact.widthReadable", compact.Width >= 325 && compact.Width <= 344, true);
                Check("compact.heightReadable", compact.Height > 185 && compact.Height <= 340, true);
                Check("compact.noScroll", compact.ContentForTest.VerticalScroll.Visible, false);
                Check("compact.cardBg", compact.BackColor, Color.FromArgb(248, 251, 255));
                CheckCardLayout(compact, "compact.fit");
                CheckPeriodGeometry(compact, "compact.100", 1.0);
                SaveSpacingPreview(compact, "preview-spacing.png");
                compact.SetScaleForTest(1.25);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.125", 1.25);
                compact.SetScaleForTest(1.5);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.150", 1.5);
                compact.SetScaleForTest(2.0);
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.200", 2.0);
                SaveSpacingPreview(compact, "preview-spacing-200.png");
                compact.SetScaleForTest(1.0);
                Application.DoEvents();
                CheckQuotaBarMotion(compact);
                foreach (Control card in compact.ContentControls)
                {
                    Check("compact.cardTopSpace", card.Controls[0].Top >= 10, true);
                    foreach (Control child in card.Controls)
                    {
                        Check("compact.childInside", card.ClientRectangle.Contains(child.Bounds), true);
                        Label label = child as Label;
                        if (label != null)
                        {
                            using (Graphics g = label.CreateGraphics())
                            {
                                SizeF measured = g.MeasureString(label.Text, label.Font, Math.Max(1, label.Width));
                                Check("compact.textHeight", measured.Height <= label.Height + 1, true);
                            }
                        }
                    }
                    Check("compact.cardBottomSpace", card.Height - card.Controls[card.Controls.Count - 1].Bottom
                        >= 10, true);
                }
                using (Bitmap bitmap = new Bitmap(compact.Width, compact.Height))
                {
                    compact.DrawToBitmap(bitmap, compact.ClientRectangle);
                    Check("compact.oceanFrame", bitmap.GetPixel(0, compact.Height / 2).ToArgb(),
                        Color.FromArgb(216, 227, 240).ToArgb());
                }
                Console.WriteLine("compact fixture: " + compact.Width + "x" + compact.Height);
                disposedBar = FindQuotaBar(compact);
                CardPanel narrowCard = compact.ContentControls[0] as CardPanel;
                narrowCard.Width = 300;
                narrowCard.InvalidateLayout();
                narrowCard.ForceLayout();
                Application.DoEvents();
                CheckPeriodGeometry(compact, "compact.narrow300", 1.0);
                compact.HidePanel();
            }
            Check("compact.motion.dispose", disposedBar != null && !disposedBar.MotionRunningForTest, true);

            using (PopupForm longNotes = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.BuildLarge();
                sample.Products.RemoveRange(1, sample.Products.Count - 1);
                longNotes.ForceRender(sample);
                longNotes.ShowPanel();
                Application.DoEvents();
                CardPanel before = longNotes.ContentControls[0] as CardPanel;
                int normalHeight = before.PeriodSurfacesForTest[0].Bounds.Height;
                sample.Products[0].Periods[0].Error =
                    "这是一个很长的周期错误说明，用于确认错误文字会完整换行并推动当前周期背景。";
                sample.Products[0].Periods[1].PercentKnown = false;
                sample.Products[0].Periods[1].UnknownNote =
                    "这是一个很长的未知原因说明，用于确认未知文字会完整换行并推动后续周期。";
                longNotes.ForceRender(sample);
                Application.DoEvents();
                CardPanel after = longNotes.ContentControls[0] as CardPanel;
                CheckPeriodGeometry(longNotes, "compact.longNotes", 1.0);
                Check("compact.longErrorGrows", after.PeriodSurfacesForTest[0].Bounds.Height
                    > normalHeight, true);
                Check("compact.longNoteGap",
                    after.PeriodSurfacesForTest[1].Bounds.Top
                    - after.PeriodSurfacesForTest[0].Bounds.Bottom, 8);
                bool foundLongText = false;
                foreach (Control child in after.Controls)
                {
                    Label label = child as Label;
                    if (label == null || (label.Text.IndexOf("很长", StringComparison.Ordinal) < 0)) continue;
                    foundLongText = true;
                    Check("compact.longTextHeight", TextRenderer.MeasureText(label.Text, label.Font,
                        new Size(label.Width, 300), TextFormatFlags.WordBreak).Height <= label.Height, true);
                    Check("compact.longTextInside", after.PeriodSurfacesForTest[0].Bounds.Contains(label.Bounds)
                        || after.PeriodSurfacesForTest[1].Bounds.Contains(label.Bounds), true);
                }
                Check("compact.longTextFound", foundLongText, true);
                longNotes.HidePanel();
            }

            using (PopupForm longAmount = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.Build();
                if (sample.Products.Count > 1)
                    sample.Products.RemoveRange(1, sample.Products.Count - 1);
                for (int i = 0; i < sample.Products[0].Periods.Count; i++)
                {
                    PeriodQuota period = sample.Products[0].Periods[i];
                    period.AmountKnown = true;
                    period.RemainingAmount = 1e100;
                    period.TotalKnown = true;
                    period.Total = 1e101;
                    period.PercentKnown = true;
                    period.RemainingPercent = 10;
                }
                longAmount.ForceRender(sample);
                longAmount.ShowPanel();
                longAmount.SetScaleForTest(1.0);
                Application.DoEvents();
                CheckLongAmount(longAmount, "compact.long.100");
                longAmount.SetScaleForTest(2.0);
                Application.DoEvents();
                CheckLongAmount(longAmount, "compact.long.200");
                longAmount.HidePanel();
            }

            using (PopupForm unknown = new PopupForm())
            {
                QuotaSnapshot sample = SyntheticSample.Build();
                sample.Products[0].Periods[0].Error = "failed";
                sample.Products[0].Periods[0].AmountKnown = false;
                sample.Products[0].Periods[0].PercentKnown = false;
                unknown.ForceRender(sample);
                unknown.ShowPanel();
                Application.DoEvents();
                CheckPeriodGeometry(unknown, "compact.error", 1.0);
                Check("compact.error.noBarMotion", FindQuotaBar(unknown).MotionRunningForTest, false);
                unknown.HidePanel();
            }
        }

        private sealed class VisualButton : ModernButton
        {
            internal void Hover() { OnMouseEnter(EventArgs.Empty); }
            internal void Press() { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)); }
            internal void Release() { OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)); }
            internal void LeavePointer() { OnMouseLeave(EventArgs.Empty); }
        }

        private sealed class VisualCombo : ModernComboBox
        {
            internal void PaintItem(Graphics g, bool selected, bool closed = false)
            {
                OnDrawItem(new DrawItemEventArgs(g, Font, new Rectangle(0, 0, 180, 26), 0,
                    (selected ? DrawItemState.Selected : DrawItemState.None)
                    | (closed ? DrawItemState.ComboBoxEdit : DrawItemState.None)));
            }
        }

        private static void CheckButtonPixel(VisualButton button, Color expected, string tag)
        {
            using (Bitmap bmp = new Bitmap(button.Width, button.Height))
            {
                button.DrawToBitmap(bmp, button.ClientRectangle);
                Check(tag, bmp.GetPixel(12, 12).ToArgb(), expected.ToArgb());
                Check(tag + ".roundCorner", bmp.GetPixel(0, 0).ToArgb(), Color.White.ToArgb());
            }
        }

        private static void CheckMenuTheme(ToolStripDropDown menu, string tag)
        {
            Check(tag + ".white", menu.BackColor, Color.White);
            Check(tag + ".renderer", menu.Renderer.GetType().Name, "ModernMenuRenderer");
            foreach (ToolStripItem item in menu.Items)
            {
                Check(tag + ".ink", item.ForeColor, UiStyle.Navy);
                ToolStripMenuItem child = item as ToolStripMenuItem;
                if (child == null) continue;
                using (Bitmap bmp = new Bitmap(Math.Max(10, item.Width), Math.Max(10, item.Height)))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    menu.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, item));
                    Check(tag + ".checkedSurface", bmp.GetPixel(5, 5).ToArgb(),
                        (child.Checked ? UiStyle.Selected : Color.White).ToArgb());
                }
                if (child.HasDropDownItems) CheckMenuTheme(child.DropDown, tag + ".child");
            }
        }

        private static void ModernBlueVisualCases()
        {
            Check("modern.primary", UiStyle.Primary.ToArgb(), Color.FromArgb(58, 131, 247).ToArgb());
            Check("modern.text", UiStyle.Navy.ToArgb(), Color.FromArgb(36, 65, 92).ToArgb());
            using (VisualButton b = new VisualButton())
            {
                b.Size = new Size(84, 34);
                UiStyle.StyleButton(b, true);
                CheckButtonPixel(b, UiStyle.Primary, "modern.button.default");
                b.Hover(); CheckButtonPixel(b, UiStyle.PrimaryHover, "modern.button.hover");
                b.Press(); CheckButtonPixel(b, UiStyle.PrimaryPressed, "modern.button.pressed");
                b.Release(); CheckButtonPixel(b, UiStyle.PrimaryHover, "modern.button.release");
                b.Enabled = false; CheckButtonPixel(b, UiStyle.Track, "modern.button.disabled");
                b.Enabled = true; b.LeavePointer(); UiStyle.StyleButton(b, false);
                CheckButtonPixel(b, Color.White, "modern.secondary.default");
                b.Hover(); CheckButtonPixel(b, UiStyle.Secondary, "modern.secondary.hover");
                b.Press(); CheckButtonPixel(b, UiStyle.Selected, "modern.secondary.pressed");
            }
            using (VisualCombo combo = new VisualCombo())
            using (Bitmap bmp = new Bitmap(180, 26))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                combo.Items.Add("Agent Plan · 5 小时");
                combo.PaintItem(g, false);
                Check("modern.selector.default", bmp.GetPixel(170, 12).ToArgb(), Color.White.ToArgb());
                combo.PaintItem(g, true);
                Check("modern.selector.selected", bmp.GetPixel(170, 12).ToArgb(), UiStyle.Selected.ToArgb());
                combo.PaintItem(g, true, true);
                Check("modern.selector.closedSelected", bmp.GetPixel(170, 12).ToArgb(), Color.White.ToArgb());
                bool navyInk = false;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < 160; x++)
                        if (bmp.GetPixel(x, y).ToArgb() == UiStyle.Navy.ToArgb()) navyInk = true;
                Check("modern.selector.closedInk", navyInk, true);
            }
            using (ContextMenuStrip menu = new ContextMenuStrip())
            using (Bitmap bmp = new Bitmap(100, 30))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ToolStripMenuItem item = new ToolStripMenuItem("设置");
                menu.Items.Add(item); UiStyle.StyleMenu(menu);
                item.Select();
                menu.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, item));
                Check("modern.menu.hover", bmp.GetPixel(5, 5).ToArgb(), UiStyle.TealLight.ToArgb());
            }
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));
            using (FloatingSettingsForm settings = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; }))
            {
                Check("modern.settings.white", settings.BackColor, Color.White);
                Check("modern.settings.header", settings.HeaderForTest.BackColor, Color.White);
                Check("modern.settings.button", settings.SaveButtonForTest is ModernButton, true);
                Check("modern.settings.selector", settings.ComboForTest is ModernComboBox, true);
                Check("modern.settings.rounded", settings.Region != null, true);
            }
            using (FloatingQuotaForm floating = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; }, delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; }, delegate(FloatingPreferences p) { return true; }))
            {
                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                floating.ApplyModelView(model.OnUsageResult(SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null));
                ContextMenuStrip menu = floating.CircleForTest.ContextMenuStrip;
                UiStyle.StyleMenu(menu);
                CheckMenuTheme(menu, "modern.menu");
                ToolStripMenuItem settings = (ToolStripMenuItem)menu.Items[0];
                ToolStripMenuItem content = (ToolStripMenuItem)settings.DropDownItems[0];
                ToolStripItem old = content.DropDownItems[0];
                floating.PopulateContentMenu(content);
                Check("modern.menu.rebuilt", ReferenceEquals(old, content.DropDownItems[0]), false);
                CheckMenuTheme(menu, "modern.menu.rebuilt");
            }
            using (QuotaBar bar = new QuotaBar())
            using (Bitmap bmp = new Bitmap(100, 6))
            {
                bar.Size = bmp.Size; bar.Value = 50;
                bar.DrawToBitmap(bmp, bar.ClientRectangle);
                Check("modern.bar.fill", bmp.GetPixel(20, 3).ToArgb(), UiStyle.Primary.ToArgb());
                Check("modern.bar.track", bmp.GetPixel(80, 3).ToArgb(), UiStyle.Track.ToArgb());
            }
        }

        private static void CheckLongAmount(PopupForm form, string tag)
        {
            bool found = false;
            string expected = DisplayNames.Number(1e100) + " 额度";
            foreach (Control card in form.ContentControls)
                for (int i = 0; i + 2 < card.Controls.Count; i++)
                {
                    QuotaBar bar = card.Controls[i] as QuotaBar;
                    Label amount = card.Controls[i + 2] as Label;
                    if (bar == null || amount == null) continue;
                    found = true;
                    Check(tag + ".fullTooWide", TextRenderer.MeasureText(expected, amount.Font).Width
                        > amount.Width, true);
                    Check(tag + ".scientific", amount.Text.IndexOf("E+", StringComparison.Ordinal) >= 0, true);
                    Check(tag + ".measured", TextRenderer.MeasureText(amount.Text, amount.Font).Width
                        <= amount.Width, true);
                    Check(tag + ".tooltip", form.ToolTipForTest.GetToolTip(amount), expected);
                }
            Check(tag + ".found", found, true);
        }

        private static QuotaBar FindQuotaBar(PopupForm form)
        {
            foreach (Control card in form.ContentControls)
                foreach (Control child in card.Controls)
                    if (child is QuotaBar) return (QuotaBar)child;
            return null;
        }

        private static void SaveSpacingPreview(PopupForm form, string name)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(path);
            }
            Console.WriteLine("spacing preview: " + path);
        }

        private static int ScaleLogical(int logical, double scale)
        {
            return (int)Math.Round(logical * scale);
        }

        private static void CheckPeriodGeometry(PopupForm form, string tag)
        {
            CheckPeriodGeometry(form, tag, 1.0);
        }

        private static void CheckPeriodGeometry(PopupForm form, string tag, double scale)
        {
            foreach (Control card in form.ContentControls)
            {
                CardPanel panel = card as CardPanel;
                Check(tag + ".surfaceCount", panel == null ? 0 : panel.PeriodSurfacesForTest.Count,
                    panel == null ? 0 : Convert.ToInt32(card.Tag));
                if (panel == null) continue;
                int edge = ScaleLogical(10, scale);
                int innerPad = ScaleLogical(10, scale);
                int topPad = ScaleLogical(8, scale);
                int rowGap = ScaleLogical(4, scale);
                int radius = ScaleLogical(8, scale);
                int expectedWidth = card.ClientSize.Width - edge * 2;
                int firstBarLeft = -1;
                int firstBarRight = -1;
                for (int s = 0; s < panel.PeriodSurfacesForTest.Count; s++)
                {
                    CardPanel.PeriodSurface surface = panel.PeriodSurfacesForTest[s];
                    Check(tag + ".surfaceFill" + s, surface.Fill, Color.FromArgb(242, 247, 255));
                    Check(tag + ".surfaceRadius" + s, surface.Radius, radius);
                    Check(tag + ".surfaceLeft" + s, surface.Bounds.Left, edge);
                    Check(tag + ".surfaceRight" + s, card.ClientSize.Width - surface.Bounds.Right, edge);
                    Check(tag + ".surfaceWidth" + s, surface.Bounds.Width, expectedWidth);
                    if (s == panel.PeriodSurfacesForTest.Count - 1)
                        Check(tag + ".surfaceBottomPad" + s,
                            card.ClientSize.Height - surface.Bounds.Bottom, edge);
                    Check(tag + ".surfaceTopPad" + s, surface.Bounds.Top >= 0, true);
                    if (s == 0 && card.Controls.Count > 0)
                        Check(tag + ".titleGap", surface.Bounds.Top - card.Controls[0].Bottom,
                            ScaleLogical(10, scale));
                    if (s > 0)
                        Check(tag + ".surfaceGap" + s,
                            surface.Bounds.Top - panel.PeriodSurfacesForTest[s - 1].Bounds.Bottom,
                            ScaleLogical(8, scale));
                }
                for (int i = 0; i < card.Controls.Count; i++)
                {
                    QuotaBar bar = card.Controls[i] as QuotaBar;
                    if (bar == null || i < 2 || i + 2 >= card.Controls.Count) continue;
                    Label type = card.Controls[i - 2] as Label;
                    Label pct = card.Controls[i - 1] as Label;
                    Label date = card.Controls[i + 1] as Label;
                    Label amount = card.Controls[i + 2] as Label;
                    if (type == null || pct == null || date == null || amount == null) continue;
                    int surfaceIndex = 0;
                    for (int j = 0; j < i; j++)
                        if (card.Controls[j] is QuotaBar) surfaceIndex++;
                    CardPanel.PeriodSurface surface = panel.PeriodSurfacesForTest[surfaceIndex];
                    Check(tag + ".contentLeft", type.Left, surface.Bounds.Left + innerPad);
                    Check(tag + ".contentRight", pct.Right, surface.Bounds.Right - innerPad);
                    Check(tag + ".contentTop", type.Top, surface.Bounds.Top + topPad);
                    Check(tag + ".auxGap", date.Top - type.Bottom, rowGap);
                    if (firstBarLeft < 0)
                    {
                        firstBarLeft = bar.Left;
                        firstBarRight = bar.Right;
                    }
                    Check(tag + ".barStart", bar.Left, firstBarLeft);
                    Check(tag + ".barEnd", bar.Right, firstBarRight);
                    Check(tag + ".surfaceContainsRow", surface.Bounds.Contains(type.Bounds)
                        && surface.Bounds.Contains(pct.Bounds)
                        && surface.Bounds.Contains(date.Bounds)
                        && surface.Bounds.Contains(amount.Bounds), true);
                    Check(tag + ".typeBar", type.Bounds.IntersectsWith(bar.Bounds), false);
                    Check(tag + ".barPct", bar.Bounds.IntersectsWith(pct.Bounds), false);
                    Check(tag + ".barLargest", bar.Width > type.Width && bar.Width > pct.Width, true);
                    Check(tag + ".firstCenter", Math.Abs(type.Bounds.Top + type.Height / 2
                        - (bar.Top + bar.Height / 2)) <= 1, true);
                    Check(tag + ".pctCenter", Math.Abs(pct.Bounds.Top + pct.Height / 2
                        - (bar.Top + bar.Height / 2)) <= 1, true);
                    Check(tag + ".dateNoLabel", date.Text.IndexOf("重置", StringComparison.Ordinal) < 0, true);
                    Check(tag + ".dateLeft", date.Left, type.Left);
                    Check(tag + ".amountRight", amount.Right, pct.Right);
                    Check(tag + ".secondNoOverlap", date.Bounds.IntersectsWith(amount.Bounds), false);
                    Check(tag + ".barHeight", bar.Height, ScaleLogical(6, scale));
                    Check(tag + ".dateMeasured", TextRenderer.MeasureText(date.Text, date.Font).Width
                        <= date.Width, true);
                    Check(tag + ".amountMeasured", TextRenderer.MeasureText(amount.Text, amount.Font).Width
                        <= amount.Width, true);
                    Check(tag + ".dateHeight", TextRenderer.MeasureText(date.Text, date.Font,
                        new Size(date.Width, 100), TextFormatFlags.WordBreak).Height <= date.Height, true);
                    Check(tag + ".amountHeight", TextRenderer.MeasureText(amount.Text, amount.Font,
                        new Size(amount.Width, 100), TextFormatFlags.WordBreak).Height <= amount.Height, true);
                    Check(tag + ".inside", card.ClientRectangle.Contains(type.Bounds)
                        && card.ClientRectangle.Contains(bar.Bounds)
                        && card.ClientRectangle.Contains(pct.Bounds)
                        && card.ClientRectangle.Contains(date.Bounds)
                        && card.ClientRectangle.Contains(amount.Bounds), true);
                    if (amount.Text.IndexOf("E+", StringComparison.Ordinal) >= 0)
                        Check(tag + ".longTooltip", form.ToolTipForTest.GetToolTip(amount).Contains("额度"), true);
                }
            }
        }

        private static void CheckQuotaBarMotion(PopupForm form)
        {
            QuotaBar bar = FindQuotaBar(form);
            Check("compact.motion.exists", bar != null, true);
            if (bar == null) return;
            Check("compact.motion.static", bar.MotionRunningForTest, false);
            int phase = bar.PhaseForTest;
            double value = bar.Value;
            bar.TickForTest();
            Check("compact.motion.valueStable", bar.Value, value);
            Check("compact.motion.noTick", bar.PhaseForTest, phase);
            form.HidePanel();
            Check("compact.motion.hidden", bar.MotionRunningForTest, false);
            form.ShowPanel();
            Check("compact.motion.reopenStatic", bar.MotionRunningForTest, false);
            form.SetReduceMotion(true);
            Check("compact.motion.reduced", bar.MotionRunningForTest, false);
            form.HidePanel();
            form.ShowPanel();
            Check("compact.motion.reducedAcrossHide", bar.MotionRunningForTest, false);
            form.SetReduceMotion(false);
            Check("compact.motion.restoredStatic", bar.MotionRunningForTest, false);
            bar.Value = 0;
            Check("compact.motion.zero", bar.MotionRunningForTest, false);
            bar.Value = -1;
            Check("compact.motion.unknown", bar.MotionRunningForTest, false);
        }

    }
}
