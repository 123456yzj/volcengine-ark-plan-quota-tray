using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm
    {
        // Session-persistent first-run guidance card with a dismiss button.
        private Panel BuildBannerCard(string text)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(16);
            StyleCard(card, S(8));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Label lbl = new Label();
                lbl.Font = F(8.5f, false);
                lbl.ForeColor = TextDark;
                lbl.AutoSize = false;
                lbl.Text = text;
                int textH = MeasureWrappedHeight(text, lbl.Font, innerW);
                lbl.SetBounds(card.PadX, S(8), innerW, textH);
                card.Controls.Add(lbl);
                Button close = new ModernButton();
                close.Text = "知道了";
                close.AccessibleName = "关闭首次运行提示";
                close.TabStop = true;
                close.FlatStyle = FlatStyle.Flat;
                close.FlatAppearance.BorderSize = 0;
                close.BackColor = Color.White;
                close.ForeColor = UiStyle.Teal;
                close.FlatAppearance.MouseOverBackColor = UiStyle.TealLight;
                close.FlatAppearance.MouseDownBackColor = UiStyle.Selected;
                close.Font = F(8.25f, false);
                close.SetBounds(card.PadX, S(10) + textH, S(64), S(24));
                close.Click += delegate { DismissIntro(); };
                card.Controls.Add(close);
                card.Height = textH + S(42);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private void DismissIntro()
        {
            _introCard = null;
        }

        private Panel BuildActionButton(string text, EventHandler onClick)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(12);
            StyleCard(card, S(8));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Button b = new ModernButton();
                b.Text = text;
                b.AccessibleName = text;
                b.TabStop = true;
                UiStyle.StyleButton(b, false);
                b.MinimumSize = Size.Empty;
                b.Font = F(9f, false);
                b.SetBounds(card.PadX, S(8), innerW, S(30));
                b.Click += onClick;
                card.Controls.Add(b);
                card.Height = S(46);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private Panel BuildMessageCard(string text, Color color)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(12);
            StyleCard(card, S(12));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Label lbl = new Label();
                lbl.Font = F(9.5f, false);
                lbl.ForeColor = color;
                lbl.AutoSize = false;
                lbl.Text = text;
                int textH = MeasureWrappedHeight(text, lbl.Font, innerW);
                lbl.SetBounds(card.PadX, S(12), innerW, textH);
                card.Controls.Add(lbl);
                card.Height = textH + S(24);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private static void ClearCard(Control card)
        {
            Control[] old = new Control[card.Controls.Count];
            card.Controls.CopyTo(old, 0);
            card.Controls.Clear();
            for (int i = 0; i < old.Length; i++)
            {
                try { old[i].Dispose(); } catch (Exception) { }
            }
        }

        private static int MeasureWrappedHeight(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try
            {
                return TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(1, width), Int32.MaxValue),
                    TextFormatFlags.WordBreak).Height;
            }
            catch (Exception)
            {
                return 18;
            }
        }

        private int ScrollbarWidth()
        {
            try { return SystemInformation.VerticalScrollBarWidth; }
            catch (Exception) { return 17; }
        }

        // Applies the DPI-scaled card corner radius once, so the clipped Region
        // (CardPanel) and the drawn border (DrawCardBorder) are always equal.
        private static void StyleCard(CardPanel card, int scale)
        {
            card.RegionRadius = scale;
        }

        private Panel BuildProductCard(ProductQuota pq, RiskSummary risk)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(10);
            StyleCard(card, S(8));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };

            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                List<CardPanel.PeriodSurface> periodSurfaces =
                    new List<CardPanel.PeriodSurface>();
                int surfacePad = S(10);
                int surfaceWidth = card.ClientSize.Width - surfacePad * 2;
                int pad = surfacePad + S(10);
                innerW = Math.Max(1, surfaceWidth - S(10) - S(10));
                int y = S(12);

                Label title = new Label();
                title.Font = F(10f, true);
                title.ForeColor = TextDark;
                title.AutoSize = false;
                title.Text = ProductTitle(pq);
                int titleHeight = MeasureWrappedHeight(title.Text, title.Font, innerW);
                title.SetBounds(pad, y, innerW, titleHeight);
                card.Controls.Add(title);
                y += titleHeight + S(10);

                if (!pq.SubscribedKnown)
                    y = AddLine(card, "订阅状态未知", TextMuted, pad, y, innerW);
                else if (!pq.Subscribed && pq.Error == null && !pq.PeriodErrorPresent && !pq.Malformed)
                    y = AddLine(card, "未订阅", TextMuted, pad, y, innerW);

                if (pq.Error != null)
                    y = AddLine(card, pq.Error, ErrorColor, pad, y, innerW);
                else if (pq.Periods.Count == 0)
                    y = AddLine(card, "无周期数据。", TextMuted, pad, y, innerW);

                for (int i = 0; i < pq.Periods.Count; i++)
                {
                    y = BuildPeriodRows(card, pq, pq.Periods[i], pad, y, innerW,
                        false, periodSurfaces);
                    if (i < pq.Periods.Count - 1) y += S(8);
                }

                card.SetPeriodSurfaces(periodSurfaces);
                card.Height = y + S(10);
                card.Tag = pq.Periods.Count;
            };

            card.Width = _cardWidth > 0 ? _cardWidth : S(340);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private int AddLine(Control card, string text, Color color, int pad, int y, int innerW)
        {
            Label l = MakeLine(text, color, innerW);
            int height = MeasureWrappedHeight(text, l.Font, innerW);
            l.SetBounds(pad, y, innerW, height);
            card.Controls.Add(l);
            return y + height;
        }

        private int BuildPeriodRows(Control card, ProductQuota product, PeriodQuota p,
            int pad, int y, int innerW, bool isRisk,
            List<CardPanel.PeriodSurface> periodSurfaces)
        {
            EffectivePeriodQuota effective = QuotaDisplay.Effective(product, p);
            int blockTop = y;
            Color blockColor = PeriodSurfaceColor(p);
            int contentY = y + S(8);
            Label name = new Label();
            name.Font = F(9f, true);
            name.ForeColor = TextDark;
            name.AutoSize = false;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.Text = p.LabelDisplay;
            name.BackColor = blockColor;
            int nameWidth = S(48);
            int pctWidth = S(60);
            int gap = S(8);
            int barWidth = innerW - nameWidth - pctWidth - gap * 2;
            if (barWidth < S(20)) barWidth = S(20);
            int rowHeight = Math.Max(S(24), MeasureWrappedHeight(name.Text, name.Font, nameWidth));
            name.SetBounds(pad, contentY, nameWidth, rowHeight);
            card.Controls.Add(name);

            Label pct = new Label();
            pct.AutoSize = false;
            pct.TextAlign = ContentAlignment.MiddleRight;
            pct.BackColor = blockColor;
            if (p.Error != null)
            {
                pct.Font = F(10f, true);
                pct.ForeColor = ErrorColor;
                pct.Text = "获取失败";
            }
            else if (effective.PercentKnown)
            {
                pct.Font = F(11f, true);
                pct.ForeColor = TextDark;
                pct.Text = PercentFormat.Remaining(effective.RemainingPercent);
                _tip.SetToolTip(pct, PercentFormat.RemainingForBar(effective.RemainingPercent));
            }
            else
            {
                pct.Font = F(10.5f, true);
                pct.ForeColor = TextMuted;
                pct.Text = "剩余未知";
            }
            rowHeight = Math.Max(rowHeight, MeasureWrappedHeight(pct.Text, pct.Font, pctWidth));
            name.Height = rowHeight;
            pct.SetBounds(pad + innerW - pctWidth, contentY, pctWidth, rowHeight);
            card.Controls.Add(pct);

            QuotaBar bar = new QuotaBar();
            bar.Value = effective.PercentKnown ? effective.RemainingPercent : -1;
            bar.ReduceMotion = _reduceMotion;
            bar.SetBounds(pad + nameWidth + gap, contentY + (rowHeight - S(6)) / 2,
                barWidth, S(6));
            card.Controls.Add(bar);
            y = contentY + rowHeight + S(4);

            Label reset = new Label();
            reset.Font = F(8.5f, false);
            reset.ForeColor = TextMuted;
            reset.AutoSize = false;
            reset.TextAlign = ContentAlignment.MiddleLeft;
            reset.Text = p.HasReset ? DisplayNames.FormatTime(p.ResetLocal) : "--";
            reset.BackColor = blockColor;
            int dateWidth = TextRenderer.MeasureText(reset.Text, reset.Font).Width + S(2);
            int minAmountWidth = S(1);
            int availableWidth = Math.Max(2, innerW);
            int dateColumn = Math.Min(Math.Max(S(1), availableWidth - minAmountWidth),
                Math.Max(availableWidth / 2, dateWidth));
            int amountStart = pad + dateColumn;
            reset.SetBounds(pad, y, dateColumn, S(20));
            card.Controls.Add(reset);

            Label amount = new Label();
            amount.Font = F(8.5f, false);
            amount.ForeColor = TextMuted;
            amount.AutoSize = false;
            amount.TextAlign = ContentAlignment.MiddleRight;
            string fullAmount = effective.AmountKnown
                ? DisplayNames.Number(effective.RemainingAmount) + " 额度" : "--";
            int amountWidth = Math.Max(1, availableWidth - dateColumn);
            amount.Text = FitAmountText(fullAmount, effective.AmountKnown
                ? effective.RemainingAmount : double.NaN, amountWidth, amount.Font);
            amount.BackColor = blockColor;
            if (amount.Text != fullAmount)
                _tip.SetToolTip(amount, fullAmount);
            amount.SetBounds(amountStart, y, amountWidth, S(20));
            card.Controls.Add(amount);
            y += S(20);

            if (p.Error != null)
            {
                int first = card.Controls.Count;
                y = AddLine(card, p.Error, ErrorColor, pad, y + S(2), innerW);
                card.Controls[first].BackColor = blockColor;
            }

            if (p.Clamped && p.UnknownNote != null)
            {
                int first = card.Controls.Count;
                y = AddLine(card, p.UnknownNote, WarningColor, pad, y, innerW);
                card.Controls[first].BackColor = blockColor;
            }
            else if (!effective.PercentKnown && p.UnknownNote != null)
            {
                int first = card.Controls.Count;
                y = AddLine(card, p.UnknownNote, TextMuted, pad, y, innerW);
                card.Controls[first].BackColor = blockColor;
            }

            int blockBottom = y + S(8);
            periodSurfaces.Add(new CardPanel.PeriodSurface(
                new Rectangle(pad - S(10), blockTop, innerW + S(10) * 2,
                    blockBottom - blockTop), blockColor, S(8)));
            return blockBottom;
        }

        private Color PeriodSurfaceColor(PeriodQuota p)
        {
            return UiStyle.Secondary;
        }

        internal static string FitAmountText(string full, double value, int width, Font font,
            string unit = "额度")
        {
            if (TextRenderer.MeasureText(full, font).Width <= Math.Max(1, width - 2)) return full;
            if (double.IsNaN(value) || double.IsInfinity(value)) return full;
            string compact = value.ToString("0.###E+0",
                System.Globalization.CultureInfo.InvariantCulture) + " " + unit;
            return compact;
        }

        private static double ClampPercent(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        private Label MakeLine(string text, Color color, int width)
        {
            Label l = new Label();
            l.Font = F(8.25f, false);
            l.ForeColor = color;
            l.AutoSize = false;
            l.Text = text;
            return l;
        }

        private void DrawCardBorder(PaintEventArgs e, Control card)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Use the SAME DPI-scaled radius as the card's clipped Region so the
            // drawn border and the region agree at every DPI (a fixed 12px border
            // on a scaled region left a mismatched corner).
            int radius = card is CardPanel ? ((CardPanel)card).RegionRadius : S(12);
            using (Pen pen = new Pen(UiStyle.Border))
            using (GraphicsPath path = UiStyle.RoundedRectangle(
                new Rectangle(0, 0, card.Width - 1, card.Height - 1), radius))
                e.Graphics.DrawPath(pen, path);
        }

        private static string ProductTitle(ProductQuota pq)
        {
            // The product name already carries "团队版" when applicable; do not
            // repeat the edition, to avoid "团队版 · 团队版".
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(pq.DisplayName);
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
