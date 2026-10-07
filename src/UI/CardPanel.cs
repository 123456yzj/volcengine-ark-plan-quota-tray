using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArkLeft
{
    // A card that explicitly re-lays-out its children from its OWN actual width.
    internal class CardPanel : Panel
    {
        internal sealed class PeriodSurface
        {
            public Rectangle Bounds;
            public Color Fill;
            public int Radius;

            public PeriodSurface(Rectangle bounds, Color fill, int radius)
            {
                Bounds = bounds;
                Fill = fill;
                Radius = radius;
            }
        }

        public CardPanel()
        {
            // UX022 v0.14: the card itself is the focusable detail surface.
            // A plain Panel is not selectable by default; with Selectable +
            // TabStop the details can hand keyboard focus to the real card
            // instead of relying on (now removed) header buttons.
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public Action<int> Reflow;   // arg = inner width (card width - 2*padX)
        // UX022: raised after every real reflow so form-level in-place
        // decorations (the compact status line) survive ClearCard rebuilds.
        public Action AfterLayout;
        public int PadX;
        private int _laidOutWidth = -1;
        private Region _cardRegion;
        private List<PeriodSurface> _periodSurfaces = new List<PeriodSurface>();
        // Already DPI-scaled corner radius. DrawCardBorder uses the SAME value
        // so the clipped region and the drawn border never disagree per DPI.
        private int _regionRadius = 12;

        public int RegionRadius
        {
            get { return _regionRadius; }
            set
            {
                int r = value < 1 ? 1 : value;
                if (r == _regionRadius) return;
                _regionRadius = r;
                ApplyCardRegion();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCardRegion();
            RunLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCardRegion();
            RunLayout();
        }

        private void ApplyCardRegion()
        {
            if (Width < 1 || Height < 1) return;
            try
            {
                Region old = _cardRegion;
                using (GraphicsPath path = UiStyle.RoundedRectangle(
                    new Rectangle(0, 0, Width - 1, Height - 1), _regionRadius))
                    _cardRegion = new Region(path);
                Region = _cardRegion;
                if (old != null) old.Dispose();
            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _cardRegion != null)
            {
                _cardRegion.Dispose();
                _cardRegion = null;
            }
            base.Dispose(disposing);
        }

        public void RunLayout()
        {
            int inner = Width - PadX * 2;
            if (inner < 1 || Reflow == null || inner == _laidOutWidth) return;
            _laidOutWidth = inner;
            try { Reflow(inner); } catch (Exception) { }
            if (AfterLayout != null) { try { AfterLayout(); } catch (Exception) { } }
        }

        public void ForceLayout()
        {
            RunLayout();
        }

        // UX022: a same-width / different-DPI reopen must reflow even though
        // the pixel width did not change; the cache is invalidated explicitly.
        public void InvalidateLayout()
        {
            _laidOutWidth = -1;
        }

        public void SetPeriodSurfaces(List<PeriodSurface> surfaces)
        {
            _periodSurfaces = surfaces ?? new List<PeriodSurface>();
            Invalidate();
        }

        internal List<PeriodSurface> PeriodSurfacesForTest
        {
            get { return _periodSurfaces; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < _periodSurfaces.Count; i++)
            {
                PeriodSurface surface = _periodSurfaces[i];
                if (surface.Bounds.Width < 2 || surface.Bounds.Height < 2) continue;
                using (GraphicsPath path = UiStyle.RoundedRectangle(surface.Bounds, surface.Radius))
                using (SolidBrush brush = new SolidBrush(surface.Fill))
                    e.Graphics.FillPath(brush, path);
            }
            base.OnPaint(e);
        }
    }
}
