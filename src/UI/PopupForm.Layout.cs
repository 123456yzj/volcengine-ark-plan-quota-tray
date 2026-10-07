using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm
    {
        private int S(int logical)
        {
            return (int)Math.Round(logical * _scale);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_chromeReady)
            {
                _chromeReady = true;
                double s = DeviceDpi > 0 ? DeviceDpi / 96.0 : _scale;
                if (Math.Abs(s - _scale) > 0.001)
                {
                    // UX022: a real DPI at handle creation differs from the
                    // guess — re-derive the fitted size from the actual scale.
                    _scale = s;
                    RelayoutContent();
                }
            }
        }

        private Screen ActiveScreen()
        {
            return _openScreen ?? Screen.FromPoint(Cursor.Position);
        }

        // UX022 v0.14: the window hugs the cards. Height fitting uses the REAL
        // layout in at most two bounded passes: pass 1 lays every card out at
        // the full clamped width and measures the actual content bottom; only
        // when that overflows the capped height does pass 2 subtract the REAL
        // scrollbar width and re-flow the cards. No infinite resize loop and
        // no guessing from the period count.
        // fullPass: re-evaluate BOTH the width decision and the height
        // (rendered data changed). When false (same-semantics refresh, e.g.
        // only the status row / update time moved), the CURRENT width decision
        // is kept — otherwise the two passes would flip the width (full ↔
        // scrollbar) and REBUILD every card on each refresh, destroying the
        // same-card / same-children guarantees.
        private void SizeToFit()
        {
            SizeToFit(true);
        }

        private void SizeToFit(bool fullPass)
        {
            Rectangle wa = ActiveScreen().WorkingArea;
            int w = LayoutMath.ClampWidth(PanelPositioner.LogicalCardWidth, _scale, wa.Width);
            int maxH = Math.Min(S(340), wa.Height - S(16));
            if (maxH < 1) maxH = 1;

            if (!fullPass && _scrolling)
            {
                // Keep the scrollbar width; only re-measure for the hug.
                ApplyCardWidths(Math.Max(1, w - 2 - ScrollbarWidth()));
                int h2 = MeasureCardsHeight();
                Size target2 = new Size(w, Math.Max(3, Math.Min(maxH, h2 + 2)));
                if (ClientSize != target2) { ClientSize = target2; if (Visible) _pendingReposition = true; }
                ApplyWindowRegion();
                return;
            }

            // Pass 1: full width, no scrollbar.
            _scrolling = false;
            _content.AutoScroll = false;
            ApplyCardWidths(Math.Max(1, w - 2));
            int h = MeasureCardsHeight() + 2;

            // Pass 2 (bounded): real overflow — subtract the REAL scrollbar.
            if (h > maxH)
            {
                _scrolling = true;
                _content.AutoScroll = true;
                ApplyCardWidths(Math.Max(1, w - 2 - ScrollbarWidth()));
                h = maxH;
            }

            Size target = new Size(w, Math.Max(1, h));
            bool resized = ClientSize != target;
            if (resized) ClientSize = target;
            ApplyWindowRegion();
            if (resized && Visible) _pendingReposition = true;
        }

        // Lays every card at the given width; CardPanel reflows from its OWN
        // width. The 10dp gap exists ONLY between cards — never after the
        // last one (no tail gap).
        private void ApplyCardWidths(int w)
        {
            _contentWidth = w;
            _cardWidth = w;
            _content.SuspendLayout();
            try
            {
                int count = _content.Controls.Count;
                for (int i = 0; i < count; i++)
                {
                    Control c = _content.Controls[i];
                    c.Margin = i < count - 1 ? new Padding(0, 0, 0, S(3)) : new Padding(0);
                    c.Width = w;
                }
            }
            finally
            {
                _content.ResumeLayout(true);
            }
        }

        private int MeasureCardsHeight()
        {
            // UX022: sum each card's own measured height plus its vertical
            // margins instead of reading Control.Bottom. Bottom is only valid
            // once the FlowLayoutPanel has positioned its children, but this
            // runs right after ApplyCardWidths (which reflows every card
            // synchronously and sets its Height) and BEFORE the panel's own
            // layout moves them into place. Reading Bottom there uses stale /
            // zero positions, which both under- and over-measured the real
            // content and then flipped the width in a later pass, rebuilding
            // the cards on same-semantics refreshes. TopDown flow advances by
            // Height + Margin.Top + Margin.Bottom (Padding is 0).
            int total = 0;
            for (int i = 0; i < _content.Controls.Count; i++)
            {
                Control c = _content.Controls[i];
                total += c.Height + c.Margin.Top + c.Margin.Bottom;
            }
            return total;
        }

        // Keep the outer frame rectangular; individual cards retain their corners.
        private void ApplyWindowRegion()
        {
            if (Region != null) { Region.Dispose(); Region = null; }
        }

        // UX022: first selectable content — the card itself (CardPanel is
        // explicitly selectable) or a focusable control inside it (retry /
        // login / guide action buttons).
        // A bare container focus (the FlowLayoutPanel) is NOT card focus: the
        // form can report ContainsFocus while only that panel is active, and
        // then the card must still take focus.
        private bool CardHasFocus()
        {
            Control active = ActiveControl;
            if (active == null || active == _content) return false;
            for (Control p = active; p != null; p = p.Parent)
            {
                if (p == _content) return true;
            }
            return false;
        }

        private void FocusFirstContent()
        {
            foreach (Control c in _content.Controls)
            {
                if (c.CanSelect) { FocusQuietly(c); return; }
                foreach (Control k in c.Controls)
                {
                    if (k.CanSelect) { FocusQuietly(k); return; }
                }
            }
        }

        private static void FocusQuietly(Control c)
        {
            try { c.Focus(); } catch (Exception) { }
        }

        private void InvalidateCardLayouts()
        {
            foreach (Control c in _content.Controls)
            {
                CardPanel card = c as CardPanel;
                if (card != null) card.InvalidateLayout();
            }
        }

        // UX022: re-derive the rendered cards from the current view (a DPI /
        // scale change must rebuild fonts / paddings / radius / height) and
        // re-fit the window. Zero query; the same snapshot is kept.
        private void RelayoutContent()
        {
            if (_view != null)
            {
                _displayKey = null;   // force a real rebuild
                ApplyModelView(_view);
            }
            else
            {
                SizeToFit();
            }
        }
    }
}
