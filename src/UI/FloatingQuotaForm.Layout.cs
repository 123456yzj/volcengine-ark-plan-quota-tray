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
    internal partial class FloatingQuotaForm
    {
        public bool AllowClose { set { _circle.AllowClose = value; } }
        public bool CircleVisible { get { return _circle.CircleVisible; } }

        public void ShowCircle()
        {
            // A user-requested show cancels any auto-hide-for-details flag so a
            // later details close cannot mis-restore a user-hidden circle.
            _autoHiddenForDetails = false;
            // Position once per session; a user drag is preserved on later
            // re-opens. Position is never persisted across restarts.
            if (!_positioned)
            {
                Screen scr = Screen.FromPoint(Cursor.Position);
                _positioned = true;
                _circle.ShowAt(scr.WorkingArea, DpiUtil.GetScale(scr));
            }
            else if (!_circle.Visible)
            {
                _circle.Show();
            }
        }

        public void HideCircle()
        {
            // Explicit hide cancels any layout auto-hide, so a subsequent
            // details-close cannot pull the circle back.
            _autoHiddenForDetails = false;
            if (_circle.Visible) _circle.Hide();
        }

        public void ToggleCircle()
        {
            if (_circle.Visible) HideCircle();
            else ShowCircle();
        }

        // v0.12 UX020: explicit "归位". Moves the circle to the bottom-right
        // of the work area of the screen the circle is currently on (the
        // primary screen when it was never positioned / has no usable
        // bounds), using that screen's DPI scale (136 dp logical diameter).
        // Works while the position is locked; cancels any in-progress drag;
        // shows the circle; never persists coordinates, never queries, never
        // opens details and never touches lock / reduce-motion / selection.
        public void RepositionCircleHome()
        {
            Rectangle wa;
            double scale;
            try
            {
                // v0.12 UX020 fix: a NEVER-positioned circle must go to the
                // PRIMARY screen. A fresh control already has non-zero
                // default bounds at (0,0), which may sit on a secondary
                // monitor, so Screen-from-bounds is not trusted before the
                // first real placement; afterwards follow the circle's own
                // screen (a drag may have left it on a secondary monitor).
                Screen scr = _positioned
                    ? CircleScreen()
                    : (Screen.PrimaryScreen ?? Screen.AllScreens[0]);
                wa = scr.WorkingArea;
                scale = DpiUtil.GetScale(scr);
            }
            catch (Exception)
            {
                Screen p = Screen.PrimaryScreen ?? Screen.AllScreens[0];
                wa = p.WorkingArea;
                scale = 1.0;
            }
            ApplyHome(wa, scale);
        }

        // Test entry with an injected work area: same core as the production
        // path so offline tests never depend on real Screen geometry.
        internal void RepositionCircleHomeForTest(Rectangle workArea, double scale)
        {
            ApplyHome(workArea, scale);
        }

        private void ApplyHome(Rectangle workArea, double scale)
        {
            _positioned = true; // the circle now owns a deterministic position
            // A user-requested re-home cancels any auto-hide-for-details flag
            // so a later details close cannot resurrect the old position
            // (same contract as ShowCircle / HideCircle).
            _autoHiddenForDetails = false;
            _circle.MoveHome(workArea, scale);
        }

        public void TrayDown()
        {
            _circle.NotifyTrayMouseDown();
        }

        public bool WantsHideOnTrayClick()
        {
            return _circle.WantsHideOnTrayClick();
        }

        // ---- details layout (never cover the circle) ----

        // Chooses the details position; if nothing fits it hides ONLY an
        // auto-hidden circle (never a user-hidden one) and reports that the
        // details may be shown. Uses the CIRCLE's own screen (after a drag the
        // circle may be on a secondary monitor), not the primary screen.
        // Returns the chosen placement (for tests).
        public DetailsPlacement PrepareDetails(Size details)
        {
            return PrepareDetails(details, CircleWorkingArea());
        }

        // Working area of the monitor that currently contains the circle. Falls
        // back to the primary screen only when the circle has no visible bounds.
        internal Rectangle CircleWorkingArea()
        {
            try
            {
                if (_circle.Width > 0 && _circle.Height > 0)
                    return Screen.FromRectangle(_circle.Bounds).WorkingArea;
            }
            catch (Exception) { }
            return Screen.PrimaryScreen.WorkingArea;
        }

        public Screen CircleScreen()
        {
            try
            {
                if (_circle.Width > 0 && _circle.Height > 0)
                    return Screen.FromRectangle(_circle.Bounds);
            }
            catch (Exception) { }
            return Screen.PrimaryScreen;
        }

        internal DetailsPlacement PrepareDetails(Size details, Rectangle work)
        {
            if (details.Width <= 0 || details.Height <= 0)
                details = new Size(Math.Max(1, details.Width), Math.Max(1, details.Height));
            Rectangle circle = _circle.Bounds;
            Rectangle box = new Rectangle(circle.Left, circle.Top, details.Width, details.Height);
            // Guarantee a finite, non-zero fallback BEFORE any branch so the
            // details bounds are never an empty rectangle (which would render
            // the panel invisible when a HideCircle branch ran first).
            _pendingDetailsBounds = FloatingLayout.Bounds(
                DetailsPlacement.Left, circle, box, work);
            DetailsPlacement p = FloatingLayout.Choose(circle, box, work);
            if (p == DetailsPlacement.HideCircle)
            {
                if (_circle.Visible)
                {
                    _autoHiddenForDetails = true;
                    _circle.Hide();
                }
                // Keep the deterministic fallback (still a valid, non-zero,
                // work-area-clamped rectangle containing the details size).
            }
            else
            {
                Rectangle target = FloatingLayout.Bounds(p, circle, box, work);
                _pendingDetailsBounds = target;
            }
            return p;
        }

        public Rectangle PendingDetailsBounds { get { return _pendingDetailsBounds; } }

        // Called when the details close; restores ONLY an auto-hidden circle.
        public void RestoreAfterDetails()
        {
            if (_autoHiddenForDetails)
            {
                _autoHiddenForDetails = false;
                // Never touch a circle that was already disposed during exit.
                if (_circle != null && !_circle.IsDisposed) _circle.Show();
            }
        }

    }
}
