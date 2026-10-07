using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArkLeft
{
    internal class QuotaBar : Control
    {
        private double _value = -1;
        private bool _reduceMotion;
        private bool _motionAllowed = true;
        private int _phase;
        private System.Windows.Forms.Timer _motion;

        public QuotaBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        // negative => unknown (bar drawn empty, never implying zero)
        public double Value
        {
            get { return _value; }
            set { _value = value; UpdateMotion(); Invalidate(); }
        }

        public bool ReduceMotion
        {
            get { return _reduceMotion; }
            set { if (_reduceMotion == value) return; _reduceMotion = value; UpdateMotion(); Invalidate(); }
        }

        private bool AnimatedValue { get { return _value > 0 && _value <= 100; } }

        private void UpdateMotion()
        {
            // The bar is intentionally static; there is no animated pixel to update.
            if (_motion != null) _motion.Stop();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateMotion();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_motion != null) _motion.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateMotion();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _motion != null)
            {
                _motion.Stop();
                _motion.Dispose();
                _motion = null;
            }
            base.Dispose(disposing);
        }

        internal bool MotionRunningForTest
        {
            get { return _motion != null && _motion.Enabled; }
        }

        internal int PhaseForTest { get { return _phase; } }

        internal void SetMotionAllowed(bool allowed)
        {
            _motionAllowed = allowed;
            UpdateMotion();
        }

        internal void TickForTest()
        {
            if (_motion != null && _motion.Enabled)
            {
                _phase = (_phase + 2) % 1000;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Only draw when there is a real pixel to paint; a 0 / negative
            // width or height is a legal no-op (never throws, never implies 0).
            if (Width <= 1 || Height <= 1) return;
            int radius = Math.Max(2, (Height - 1) / 2);
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath tp = RoundRect(track, radius))
                using (SolidBrush tb = new SolidBrush(UiStyle.Track))
                g.FillPath(tb, tp);

            if (_value >= 0)
            {
                double v = _value;
                if (v > 100) v = 100;
                // Clamp the fill to the real track width so a narrow bar can
                // never overflow past its own bounds (a fill narrower than the
                // corner diameter used to round up past the track edge).
                int trackW = Width - 1;
                int fillW = (int)Math.Floor(trackW * (v / 100.0));
                if (fillW > trackW) fillW = trackW;
                if (fillW > 0)
                {
                    Rectangle fill = new Rectangle(0, 0, fillW, Height - 1);
                    // A fill narrower than the corner diameter must not bulge
                    // outward: shrink the effective radius to the fill extent.
                    int fr = Math.Min(radius, fillW / 2);
                    if (fr < 1) fr = 1;
                    using (GraphicsPath fp = RoundRect(fill, fr))
                    {
                        using (SolidBrush fb = new SolidBrush(UiStyle.Sky))
                            g.FillPath(fb, fp);
                    }
                }
            }
        }

        private int SmoothingRadius() { return Math.Max(2, Height / 2); }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
