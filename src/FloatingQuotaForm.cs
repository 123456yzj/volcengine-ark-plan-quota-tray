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
    // Persisted display-target choice for the floating circle. This is NOT the
    // quota cache (quota-cache.dat / format 1 is untouched): it only remembers
    // which product + period the user chose to show. It stores no identity,
    // credentials or quota values. Corrupt / wrong-version files are treated as
    // "not configured" and never fabricate a selection.
    public class FloatingSettings
    {
        public int Version;
        public string ProductKey;   // Product|Edition|Tier (stable across versions)
        public string PeriodLabel;  // period label as returned by the API
    }

    public static class FloatingSettingsStore
    {
        public const int FormatVersion = 1;
        public const string FileName = "floating-settings.json";
        public const int MaxProductKeyLength = 128;
        public const int MaxPeriodLabelLength = 64;
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Marker.StateDir(), FileName); }
        }

        // Stable, identity-free product key. Edition / tier are included so two
        // different plans that share a product id never collide.
        public static string ProductKey(ProductQuota pq)
        {
            if (pq == null) return null;
            string p = pq.Product == null ? "" : pq.Product;
            string e = pq.Edition == null ? "" : pq.Edition;
            string t = pq.Tier == null ? "" : pq.Tier;
            return p + "|" + e + "|" + t;
        }

        public static bool Valid(FloatingSettings s)
        {
            if (s == null) return false;
            if (s.Version != FormatVersion) return false;
            if (string.IsNullOrEmpty(s.ProductKey)
                || s.ProductKey.Length > MaxProductKeyLength) return false;
            if (string.IsNullOrEmpty(s.PeriodLabel)
                || s.PeriodLabel.Length > MaxPeriodLabelLength) return false;
            return true;
        }

        // Strict decode. Null (=> treat as not configured) on any of: missing
        // file, malformed JSON, wrong version, missing/wrong-typed/empty fields,
        // over-length fields. Never returns a partially trusted object.
        public static FloatingSettings Load()
        {
            try
            {
                lock (Gate)
                {
                    string path = FilePath;
                    if (!File.Exists(path)) return null;
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d =
                        ser.DeserializeObject(json) as Dictionary<string, object>;
                    if (d == null) return null;
                    // Exactly the three known fields: any extra / unknown field
                    // is treated as a corrupt file, never silently ignored.
                    if (d.Count != 3) return null;

                    object v, pk, pl;
                    if (!d.TryGetValue("Version", out v) || !(v is int)) return null;
                    if ((int)v != FormatVersion) return null;
                    if (!d.TryGetValue("ProductKey", out pk) || !(pk is string)) return null;
                    if (!d.TryGetValue("PeriodLabel", out pl) || !(pl is string)) return null;

                    FloatingSettings s = new FloatingSettings();
                    s.Version = (int)v;
                    s.ProductKey = (string)pk;
                    s.PeriodLabel = (string)pl;
                    return Valid(s) ? s : null;
                }
            }
            catch (Exception) { return null; }
        }

        // Atomic write: temp file then File.Replace (Move when no old file).
        // Failure never degrades an existing valid file; returns false so the
        // caller can tell the user the choice was not remembered.
        public static bool Save(FloatingSettings s)
        {
            if (!Valid(s)) return false;
            lock (Gate)
            {
                string tmp = null;
                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    byte[] data = Encoding.UTF8.GetBytes(ser.Serialize(s));
                    string path = FilePath;
                    tmp = path + ".tmp";
                    string bak = path + ".bak";
                    Directory.CreateDirectory(Marker.StateDir());
                    DeleteQuiet(tmp);
                    File.WriteAllBytes(tmp, data);

                    if (File.Exists(path))
                    {
                        try
                        {
                            DeleteQuiet(bak);
                            File.Replace(tmp, path, bak, true);
                        }
                        catch (Exception)
                        {
                            DeleteQuiet(tmp);
                            return false;
                        }
                    }
                    else
                    {
                        File.Move(tmp, path);
                    }
                }
                catch (Exception)
                {
                    DeleteQuiet(tmp);
                    return false;
                }
            }
            DeleteQuiet(FilePath + ".bak");
            return true;
        }

        public static void Clear()
        {
            lock (Gate)
            {
                DeleteQuiet(FilePath);
                DeleteQuiet(FilePath + ".tmp");
                DeleteQuiet(FilePath + ".bak");
            }
        }

        private static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }

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
        public string ProductShort;   // short product name for the circle line
        public string PeriodTitle;
        public bool PercentKnown;
        public double Percent;            // remaining percent, 0..100
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
                    e.ProductShort = DisplayNames.Product(pq.Product);
                    e.PeriodTitle = p.LabelDisplay != null ? p.LabelDisplay
                        : DisplayNames.Period(p.Label);
                    e.PercentKnown = p.PercentKnown;
                    e.Percent = p.RemainingPercent;
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

    // Placement of the details panel relative to the always-visible circle.
    // Prefers the left; falls back right / above / below; if nothing fits the
    // circle is temporarily hidden so the details never cover it.
    public enum DetailsPlacement { Left, Right, Above, Below, HideCircle }

    // Pure geometry helper so the fallback rule is unit-testable without a
    // screen. Order: Left, Right, Above, Below; HideCircle when none fits.
    public static class FloatingLayout
    {
        public const int Gap = 8;

        public static DetailsPlacement Choose(Rectangle circle, Rectangle details,
            Rectangle work)
        {
            if (Fits(DetailsPlacement.Left, circle, details, work)) return DetailsPlacement.Left;
            if (Fits(DetailsPlacement.Right, circle, details, work)) return DetailsPlacement.Right;
            if (Fits(DetailsPlacement.Above, circle, details, work)) return DetailsPlacement.Above;
            if (Fits(DetailsPlacement.Below, circle, details, work)) return DetailsPlacement.Below;
            return DetailsPlacement.HideCircle;
        }

        public static Rectangle Bounds(DetailsPlacement p, Rectangle circle, Rectangle details,
            Rectangle work)
        {
            return Clamp(RawBounds(p, circle, details), work);
        }

        private static Rectangle RawBounds(DetailsPlacement p, Rectangle circle, Rectangle details)
        {
            int x, y;
            switch (p)
            {
                case DetailsPlacement.Right: x = circle.Right + Gap; y = circle.Top; break;
                case DetailsPlacement.Above: x = circle.Left; y = circle.Top - details.Height - Gap; break;
                case DetailsPlacement.Below: x = circle.Left; y = circle.Bottom + Gap; break;
                case DetailsPlacement.HideCircle:
                case DetailsPlacement.Left:
                default: x = circle.Left - details.Width - Gap; y = circle.Top; break;
            }
            return new Rectangle(x, y, details.Width, details.Height);
        }

        // A side is usable when its work-area-clamped rectangle stays fully
        // inside the work area (so it does not spill onto another screen) and
        // does NOT intersect the circle. Vertical clamping is expected: a circle
        // near the bottom still allows a LEFT/ABOVE placement that is pulled up.
        private static bool Fits(DetailsPlacement p, Rectangle circle, Rectangle details,
            Rectangle work)
        {
            Rectangle b = Clamp(RawBounds(p, circle, details), work);
            return b.Left >= work.Left && b.Top >= work.Top
                && b.Right <= work.Right && b.Bottom <= work.Bottom
                && !b.IntersectsWith(circle);
        }

        private static Rectangle Clamp(Rectangle b, Rectangle work)
        {
            int x = b.X, y = b.Y;
            if (x + b.Width > work.Right) x = work.Right - b.Width;
            if (y + b.Height > work.Bottom) y = work.Bottom - b.Height;
            if (x < work.Left) x = work.Left;
            if (y < work.Top) y = work.Top;
            return new Rectangle(x, y, b.Width, b.Height);
        }
    }

    // Immutable render instruction for the circle control. HasData=false means
    // "暂无数据"; PercentKnown=false (with HasData) means "剩余未知" and no wave.
    internal class FloatingDisplay
    {
        public bool HasData;
        public bool PercentKnown;
        public double Percent;
        public string PercentText;
        public string Caption;        // combined, newline-separated (tests / tooltip)
        public string ProductCaption; // short product name line
        public string PeriodCaption;  // period line (always kept separate)
        public string Tooltip;
    }

    // The self-painting circle. Top-most, border-less, no taskbar item, with an
    // elliptical Region so clicks outside the ellipse really pass through. Owns
    // only a light repaint timer (never a query). All GDI objects / fonts / the
    // region and timer are released in Dispose.
    internal class FloatingCircleControl : Form
    {
        public event EventHandler DetailsRequested;
        // Raised when the circle is hidden by an explicit user action (Esc /
        // WM_CLOSE). The owner clears its layout auto-hide flag so a later
        // details-close cannot pull the circle back.
        public event EventHandler ExplicitHideRequested;

        private const int LogicalDiameter = 136;
        private const int LogicalMargin = 16;
        private const int DragThresholdLogical = 4;

        private readonly Timer _wave = new Timer();
        private Font _percentFont;
        private Font _captionFont;
        private readonly ToolTip _tip = new ToolTip();
        private Region _region;
        private double _scale = 1.0;
        private float _phase;
        private bool _allowClose;
        private bool _hovered;

        private bool _hasData;
        private bool _percentKnown;
        private double _percent;
        private string _percentText = "暂无数据";
        private string _productCaption = "";
        private string _periodCaption = "";
        private string _tooltip = "暂无数据";

        private bool _down;
        private bool _dragging;
        private Point _downScreen;
        private Point _downLocation;
        private bool _hasTrayDown;
        private bool _trayDownVisible;
        // v0.8 UX016: when locked, mouse gestures never move the circle; the
        // drag threshold is still detected so a release after a
        // threshold-crossing gesture is swallowed instead of opening details.
        private bool _positionLocked;
        // v0.9 UX017: when set, the wave animation timer stays stopped and the
        // surface paints a static wave; display values are never changed.
        private bool _reduceMotion;
        // v0.9 UX017: set in Dispose; the wave timer must never be restarted
        // after the control is disposed.
        private bool _waveDead;

        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        // v0.8 UX016: the lock only blocks dragging. Left click / Enter /
        // Space (details), the right-click menu and show / hide keep working.
        // Toggling mid-gesture releases capture and resets the gesture so a
        // stale drag can never continue under the new state (and cannot
        // mis-open details on release).
        public bool PositionLocked
        {
            get { return _positionLocked; }
            set
            {
                if (_positionLocked == value) return;
                _positionLocked = value;
                if (_down)
                {
                    _down = false;
                    _dragging = false;
                    Capture = false;
                }
            }
        }

        // v0.9 UX017: reduce motion freezes the wave animation. The timer is
        // stopped and the surface is repainted once as a static wave; the
        // shown ratio / percent (0 / 100 / unknown) is untouched. State is
        // owned by the handler (FloatingQuotaForm), mirroring the lock.
        public bool ReduceMotion { get { return _reduceMotion; } }

        public void SetReduceMotion(bool on)
        {
            if (_reduceMotion == on) return;
            _reduceMotion = on;
            UpdateWaveState();
            if (Visible) Invalidate(); // immediate static repaint, no timer
        }

        private void RaiseExplicitHide()
        {
            if (ExplicitHideRequested != null) ExplicitHideRequested(this, EventArgs.Empty);
        }

        public FloatingCircleControl()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            BackColor = UiStyle.Navy;
            DoubleBuffered = true;
            Text = "方舟剩余额度";
            AccessibleName = "方舟剩余额度悬浮圆圈";
            BuildFonts(1.0);
            _wave.Interval = 60;
            _wave.Tick += delegate { _phase += 0.22f; if (Visible) Invalidate(); };
            MouseDown += OnCircleMouseDown;
            MouseMove += OnCircleMouseMove;
            MouseUp += OnCircleMouseUp;
            MouseEnter += delegate { _hovered = true; Invalidate(); };
            MouseLeave += delegate { _hovered = false; Invalidate(); };
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        // ---- placement ----

        public static Rectangle InitialBounds(Rectangle workArea, double scale)
        {
            if (scale <= 0) scale = 1.0;
            int d = (int)Math.Round(LogicalDiameter * scale);
            int m = (int)Math.Round(LogicalMargin * scale);
            int x = workArea.Right - d - m;
            int y = workArea.Bottom - d - m;
            return ClampTo(new Rectangle(x, y, d, d), workArea);
        }

        public static Rectangle ClampTo(Rectangle b, Rectangle workArea)
        {
            int x = b.X, y = b.Y;
            if (x < workArea.Left) x = workArea.Left;
            if (y < workArea.Top) y = workArea.Top;
            if (b.Width <= workArea.Width && x + b.Width > workArea.Right)
                x = workArea.Right - b.Width;
            if (b.Height <= workArea.Height && y + b.Height > workArea.Bottom)
                y = workArea.Bottom - b.Height;
            return new Rectangle(x, y, b.Width, b.Height);
        }

        public void ShowAt(Rectangle workArea, double scale)
        {
            _scale = scale <= 0 ? 1.0 : scale;
            BuildFonts(_scale);
            Bounds = InitialBounds(workArea, _scale);
            if (!Visible) Show();
            ApplyRegion();
            UpdateWaveState();
            Invalidate();
        }

        // v0.12 UX020: explicit "归位" (re-home). Unlike the once-per-session
        // first placement inside FloatingQuotaForm.ShowCircle, this ALWAYS
        // applies the given work area / scale (never ignores them), cancels
        // any in-progress drag first (releasing capture so a stale mouse move
        // can neither drag the circle back nor mis-open details) and shows
        // the circle. The position lock does not block it; nothing persists.
        public void MoveHome(Rectangle workArea, double scale)
        {
            ResetDragGesture();
            ShowAt(workArea, scale);
        }

        // v0.12 UX020: same gesture reset as the mid-gesture lock toggle —
        // no capture, no pending down / drag.
        internal void ResetDragGesture()
        {
            _down = false;
            _dragging = false;
            try { Capture = false; } catch (Exception) { }
        }

        // Fonts scale with DPI so the text keeps its relative size; rebuilt on
        // ShowAt (per open screen) and released in Dispose.
        private void BuildFonts(double scale)
        {
            Font pf = null, cf = null;
            try
            {
                pf = new Font("Microsoft YaHei UI", (float)(24.0 * scale), FontStyle.Bold,
                    GraphicsUnit.Point);
                cf = new Font("Microsoft YaHei UI", (float)(8.5 * scale), FontStyle.Regular,
                    GraphicsUnit.Point);
            }
            catch (Exception) { }
            Font oldP = _percentFont, oldC = _captionFont;
            // Only dispose an old font once its replacement was created; a
            // failed creation must not leave a disposed font in use.
            if (pf != null)
            {
                _percentFont = pf;
                if (oldP != null) { try { oldP.Dispose(); } catch (Exception) { } }
            }
            if (cf != null)
            {
                _captionFont = cf;
                if (oldC != null) { try { oldC.Dispose(); } catch (Exception) { } }
            }
        }

        public bool CircleVisible { get { return Visible; } }

        // Tray toggle support: the mouse-down captures the circle's actual
        // visibility (the circle never hides on focus loss, so no timer / race
        // handling is needed); the click consumes the capture exactly once.
        public void NotifyTrayMouseDown()
        {
            _hasTrayDown = true;
            _trayDownVisible = Visible;
        }

        public bool WantsHideOnTrayClick()
        {
            bool v = _hasTrayDown ? _trayDownVisible : Visible;
            _hasTrayDown = false;
            return v;
        }

        // ---- display ----

        public void SetDisplay(FloatingDisplay d)
        {
            _hasData = d != null && d.HasData;
            _percentKnown = d != null && d.PercentKnown;
            _percent = d == null ? 0 : d.Percent;
            _percentText = d == null || string.IsNullOrEmpty(d.PercentText)
                ? "暂无数据" : d.PercentText;
            _productCaption = d == null ? "" : (d.ProductCaption ?? "");
            _periodCaption = d == null ? "" : (d.PeriodCaption ?? "");
            _tooltip = d == null ? "暂无数据" : (d.Tooltip ?? "");
            try { _tip.SetToolTip(this, _tooltip); } catch (Exception) { }
            UpdateWaveState();
            if (Visible) Invalidate();
        }

        private void UpdateWaveState()
        {
            // v0.9 UX017: a disposed circle never restarts its timer, and
            // reduce motion keeps the wave static. Everything else unchanged:
            // the wave only runs while visible with known 0 < percent < 100.
            if (_waveDead) return;
            bool run = !_reduceMotion && Visible && _hasData && _percentKnown
                && _percent > 0 && _percent < 100;
            if (run)
            {
                if (!_wave.Enabled) _wave.Start();
            }
            else if (_wave.Enabled)
            {
                _wave.Stop();
            }
        }

        // v0.8 UX016: a short readable status (never a raw exception) surfaced
        // through the existing tooltip. Safe when the handle is not created
        // (offline tests only observe state); failures are swallowed.
        public void ShowStatusHint(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { _tip.Show(text, this, Width / 2, Height, 2500); }
            catch (Exception) { }
        }

        // ---- drag / click ----

        private void OnCircleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // The shared ContextMenuStrip is shown by the default WM_CONTEXTMENU
                // handling; TrayApp owns that menu (correct show/hide text).
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            _down = true;
            _dragging = false;
            _downScreen = PointToScreen(e.Location);
            _downLocation = Location;
            Capture = true;
        }

        private void OnCircleMouseMove(object sender, MouseEventArgs e)
        {
            if (!_down) return;
            Point cur = PointToScreen(e.Location);
            int dx = cur.X - _downScreen.X;
            int dy = cur.Y - _downScreen.Y;
            int threshold = (int)Math.Round(DragThresholdLogical * _scale);
            if (!_dragging && (Math.Abs(dx) > threshold || Math.Abs(dy) > threshold))
                _dragging = true;
            if (!_dragging) return;
            if (_positionLocked) return; // locked: never move (release still swallowed)
            Rectangle b = new Rectangle(_downLocation.X + dx, _downLocation.Y + dy,
                Width, Height);
            Screen scr = Screen.FromPoint(new Point(
                b.Left + b.Width / 2, b.Top + b.Height / 2));
            Bounds = ClampTo(b, scr.WorkingArea);
        }

        private void OnCircleMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_down) return;
            _down = false;
            Capture = false;
            if (_dragging) { _dragging = false; return; } // drag never opens details
            if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
        }

        // ---- keys / lifecycle ----

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                RaiseExplicitHide();
                Hide();
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                RaiseExplicitHide();
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateWaveState();
            if (Visible) ApplyRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        private void ApplyRegion()
        {
            try
            {
                using (GraphicsPath path = EllipsePath())
                {
                    Region old = _region;
                    _region = new Region(path);
                    Region = _region;
                    if (old != null) old.Dispose();
                }
            }
            catch (Exception) { }
        }

        private GraphicsPath EllipsePath()
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            return p;
        }

        // ---- painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(UiStyle.Navy);

            float w = Width - 1, h = Height - 1;
            // 0 = empty, 100 = fully filled (solid water, no sine notch);
            // intermediate values draw layered waves.
            if (_hasData && _percentKnown && _percent >= 100)
            {
                using (SolidBrush full = new SolidBrush(Color.FromArgb(132, 24, 166, 145)))
                    g.FillRectangle(full, 0, 0, Width, Height);
            }
            else if (_hasData && _percentKnown && _percent > 0)
            {
                float level = (float)(_percent / 100.0);
                if (level > 1f) level = 1f;
                DrawWaves(g, w, h, level);
            }

            using (Pen ring = new Pen(_hovered || Focused
                ? Color.FromArgb(230, 103, 219, 185) : Color.FromArgb(150, 130, 174, 190),
                Math.Max(1.5f, w * (_hovered || Focused ? 0.022f : 0.012f))))
                g.DrawEllipse(ring, 0.5f, 0.5f, w, h);

            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            center.LineAlignment = StringAlignment.Center;
            using (SolidBrush white = new SolidBrush(Color.FromArgb(250, 253, 255)))
                g.DrawString(_percentText, _percentFont, white,
                    new RectangleF(0, 0, w, h * 0.64f), center);
            if (_productCaption.Length > 0)
            {
                using (SolidBrush soft = new SolidBrush(Color.FromArgb(198, 222, 238)))
                {
                    // Product line: long names ellipsize, but the period line is
                    // always drawn separately below so it is never truncated away.
                    g.DrawString(Truncate(_productCaption, 14), _captionFont, soft,
                        new RectangleF(w * 0.08f, h * 0.65f, w * 0.84f, h * 0.12f), center);
                    if (_periodCaption.Length > 0)
                        g.DrawString(_periodCaption, _captionFont, soft,
                            new RectangleF(w * 0.08f, h * 0.77f, w * 0.84f, h * 0.12f), center);
                }
            }
            center.Dispose();
        }

        private void DrawWaves(Graphics g, float w, float h, float level)
        {
            float top = h * (1f - level);
            // Near the extremes the available span on one side of the waterline
            // is tiny; limit the amplitude so 1% never paints a large volume.
            float maxAmp = Math.Min(top, h - top) * 0.5f;
            if (maxAmp < 0f) maxAmp = 0f;
            for (int layer = 0; layer < 2; layer++)
            {
                float amp = h * (layer == 0 ? 0.028f : 0.020f);
                if (amp > maxAmp) amp = maxAmp;
                float baseTop = top + (layer == 0 ? amp * 0.4f : -amp * 0.3f);
                float phaseX = _phase * (layer == 0 ? 1f : -1.15f) + layer * 1.7f;
                float step = Math.Max(2f, w / 40f);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(0, h, 0, WaveY(baseTop, amp, phaseX, 0, w));
                    for (float x = 0; x < w; x += step)
                    {
                        float x2 = Math.Min(w, x + step);
                        path.AddLine(x, WaveY(baseTop, amp, phaseX, x, w),
                            x2, WaveY(baseTop, amp, phaseX, x2, w));
                    }
                    path.AddLine(w, WaveY(baseTop, amp, phaseX, w, w), w, h);
                    path.CloseFigure();
                    Color c = layer == 0
                        ? Color.FromArgb(80, 150, 196, 224)
                        : Color.FromArgb(140, 40, 186, 214);
                    using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
                }
            }
        }

        private static float WaveY(float baseTop, float amp, float phase, float x, float w)
        {
            if (w <= 0) return baseTop;
            double t = (x / w) * Math.PI * 3.0 + phase;
            return baseTop + (float)Math.Sin(t) * amp;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waveDead = true; // v0.9 UX017: never restart after dispose
                try { _wave.Stop(); _wave.Dispose(); } catch (Exception) { }
                try { if (_region != null) { _region.Dispose(); _region = null; } }
                catch (Exception) { }
                try { _tip.Dispose(); } catch (Exception) { }
                try { _percentFont.Dispose(); } catch (Exception) { }
                try { _captionFont.Dispose(); } catch (Exception) { }
            }
            base.Dispose(disposing);
        }

        // ---- test hooks ----

        internal bool WaveRunningForTest { get { return _wave.Enabled; } }
        internal string PercentTextForTest { get { return _percentText; } }
        internal bool PercentKnownForTest { get { return _percentKnown; } }
        internal string CaptionForTest { get { return _productCaption + " " + _periodCaption; } }
        internal string ProductCaptionForTest { get { return _productCaption; } }
        internal string PeriodCaptionForTest { get { return _periodCaption; } }
        internal string TooltipForTest { get { return _tooltip; } }
        internal bool DraggingForTest { get { return _dragging; } }

        // Dispatch through the PRODUCTION mouse handlers so the real drag
        // threshold / bounds-clamp logic is exercised (screen coords only; never
        // moves the OS cursor).
        internal void SimulateMouseDownForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseDown(this, e);
        }

        internal void SimulateMouseMoveForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 0,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseMove(this, e);
        }

        internal void SimulateMouseUpForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseUp(this, e);
        }
    }

    // Settings dialog: a ComboBox of every subscribed product/period with its
    // honest state, plus Save / Cancel. Save persists through the injected
    // callback; a write failure keeps the dialog open with a short error and
    // never pretends the choice was remembered.
    internal class FloatingSettingsForm : Form
    {
        private sealed class ComboItem
        {
            public FloatingEntry Entry;
            public string Text;
            public override string ToString() { return Text; }
        }

        private Func<FloatingSettings, bool> _save;
        private ComboBox _combo;
        private Label _error;
        private double _scale;
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private Panel _headerPanel;
        // UX015: owner work area / DPI seam, fixed layout parts and tooltip.
        private Rectangle _workArea;
        private ToolTip _tip = new ToolTip();
        private Label _heading;
        private Button _saveBtn;
        private Button _cancelBtn;
        private Button _closeBtn;
        private Panel _contentHost;
        private Label _hint;
        private bool _dragging;
        private Point _dragStart;
        private Point _formStart;

        public FloatingSettings ResultSettings;

        public FloatingSettingsForm(List<FloatingEntry> candidates,
            FloatingSettings current, Func<FloatingSettings, bool> save)
        {
            // Legacy primary-screen construction (offline tests / smoke build
            // the dialog directly). The production open path passes the OWNER
            // screen's scale and work area via the explicit overload below.
            Screen scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            Init(candidates, current, save, DpiUtil.GetScale(scr), scr.WorkingArea);
        }

        // UX015 test / multi-screen seam: explicit DPI scale and work area so
        // 100 / 150 / 200% layouts and narrow / short work areas can be checked
        // by injection instead of claimed human vision.
        internal FloatingSettingsForm(List<FloatingEntry> candidates,
            FloatingSettings current, Func<FloatingSettings, bool> save,
            double scale, Rectangle workArea)
        {
            Init(candidates, current, save, scale, workArea);
        }

        private void Init(List<FloatingEntry> candidates,
            FloatingSettings current, Func<FloatingSettings, bool> save,
            double scale, Rectangle workArea)
        {
            _save = save;
            _scale = scale;
            _workArea = workArea;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            MaximizeBox = false;
            MinimizeBox = false;
            // UX015: manual placement centred in the OWNER screen's work area
            // (never blind CenterScreen on the primary display).
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            // v0.15 UX023: the period-selection concept is named "悬浮内容"
            // consistently with the shared menu's submenu label.
            Text = "悬浮内容";
            AccessibleName = "悬浮内容";
            BackColor = UiStyle.Canvas;
            KeyPreview = true;

            // UX015/T042: physical margins first (scale-independent), then an
            // EFFECTIVE LAYOUT SCALE. The desired 430x208 logical layout keeps
            // the owner-screen DPI as long as a usable minimum layout (240x132
            // logical: header + chooser + reachable content + two 84dp
            // buttons) fits the work area; on tiny work areas the WHOLE layout
            // — fonts included — scales down (bounded below at 50%) instead of
            // overlapping, and the middle content scrolls instead of hiding
            // the save / cancel row. An extremely small work area can never
            // demand infinite pixels.
            int marginX = Math.Max(4, Math.Min(32, _workArea.Width / 20));
            int marginY = Math.Max(4, Math.Min(32, _workArea.Height / 20));
            int availW = Math.Max(60, _workArea.Width - marginX * 2);
            int availH = Math.Max(60, _workArea.Height - marginY * 2);
            double les = Math.Min(_scale, Math.Min(availW / 240.0, availH / 132.0));
            _scale = Math.Max(0.5, les);
            ClientSize = new Size(Math.Min(S(430), availW),
                Math.Min(S(208), availH));

            Panel header = new Panel();
            header.BackColor = UiStyle.Navy;
            _headerPanel = header;
            // The border-less header is the drag handle (production mouse drag,
            // not just a static title). The close button is a child and its own
            // clicks do not bubble as a header MouseDown on it.
            header.MouseDown += HeaderMouseDown;
            header.MouseMove += HeaderMouseMove;
            header.MouseUp += HeaderMouseUp;
            Label heading = new Label();
            heading.Text = "悬浮内容";
            heading.Font = F(10f, true);
            heading.ForeColor = Color.White;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.MouseDown += HeaderMouseDown;
            heading.MouseMove += HeaderMouseMove;
            heading.MouseUp += HeaderMouseUp;
            _heading = heading;
            Button closeBtn = new Button();
            closeBtn.Text = "×";
            closeBtn.AccessibleName = "关闭设置";
            closeBtn.Font = F(14f, false);
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.BackColor = UiStyle.Navy;
            closeBtn.ForeColor = Color.White;
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 79, 98);
            closeBtn.Click += delegate { Close(); };
            _closeBtn = closeBtn;
            header.Controls.Add(heading);
            header.Controls.Add(closeBtn);
            Controls.Add(header);

            // UX015: scrollable middle host — the header and the save / cancel
            // row stay fixed so a short work area can never hide the buttons.
            Panel host = new Panel();
            host.BackColor = UiStyle.Canvas;
            host.AutoScroll = true;
            _contentHost = host;
            Controls.Add(host);

            Label hint = new Label();
            hint.Text = "选择悬浮内容：";
            hint.Font = F(9f, false);
            hint.AutoSize = false;
            hint.ForeColor = UiStyle.Navy;
            _hint = hint;
            host.Controls.Add(hint);

            _combo = new ComboBox();
            _combo.DropDownStyle = ComboBoxStyle.DropDownList;
            _combo.AccessibleName = "显示目标";
            _combo.Font = F(9f, false);
            // Long product (edition / tier) + period + state strings must remain
            // readable in the dropped list; the dropdown width is MEASURED from
            // the widest item below and clamped to the owner work area. Native
            // DropDownList keeps it keyboard selectable, and the closed box
            // shows a full-text tooltip that follows the selection.
            ComboItem preselect = null;
            List<FloatingEntry> list = candidates == null
                ? new List<FloatingEntry>() : candidates;
            for (int i = 0; i < list.Count; i++)
            {
                FloatingEntry e = list[i];
                if (e == null) continue;
                ComboItem item = new ComboItem();
                item.Entry = e;
                item.Text = e.ComboText;
                _combo.Items.Add(item);
                if (current != null
                    && string.Equals(e.ProductKey, current.ProductKey, StringComparison.Ordinal)
                    && string.Equals(e.Label, current.PeriodLabel, StringComparison.Ordinal))
                    preselect = item;
            }
            if (_combo.Items.Count == 0)
            {
                ComboItem none = new ComboItem();
                none.Entry = null;
                none.Text = "暂无数据";
                _combo.Items.Add(none);
            }
            _combo.SelectedItem = preselect ?? _combo.Items[0];
            _combo.SelectedIndexChanged += delegate { UpdateSelectionTooltip(); };
            host.Controls.Add(_combo);

            _error = new Label();
            _error.AutoSize = false;
            _error.ForeColor = Color.FromArgb(217, 72, 15);
            _error.Font = F(8.25f, false);
            host.Controls.Add(_error);

            Button saveBtn = new Button();
            saveBtn.Text = "保存";
            saveBtn.AccessibleName = "保存显示目标";
            saveBtn.Font = F(9f, false);
            UiStyle.StyleButton(saveBtn, true);
            saveBtn.Click += OnSaveClick;
            _saveBtn = saveBtn;
            Controls.Add(saveBtn);

            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.AccessibleName = "取消设置";
            cancelBtn.Font = F(9f, false);
            UiStyle.StyleButton(cancelBtn, false);
            cancelBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _cancelBtn = cancelBtn;
            Controls.Add(cancelBtn);

            CancelButton = cancelBtn;
            AcceptButton = saveBtn;
            if (_combo.Items.Count == 1 && ((ComboItem)_combo.Items[0]).Entry == null)
                saveBtn.Enabled = false;

            // UX015/T042 deterministic keyboard order ACROSS CONTAINERS (tab
            // order is per-container): the content host (with the target
            // chooser) first, then save / cancel, and the header — carrying
            // the close button — LAST, so the header close can never precede
            // the chooser. Esc / the CancelButton cancel. The hint label is
            // not a tab stop.
            _contentHost.TabIndex = 0;
            _combo.TabIndex = 0;
            saveBtn.TabIndex = 1;
            cancelBtn.TabIndex = 2;
            header.TabIndex = 3;
            closeBtn.TabIndex = 0; // first (only) tab stop inside the header
            _combo.TabStop = true;
            ActiveControl = _combo;

            UpdateSelectionTooltip();

            // UX015: measure the widest candidate, then clamp the dropdown to
            // the owner work area (a wider dropdown would truncate anyway).
            int maxText = 0;
            for (int i = 0; i < _combo.Items.Count; i++)
            {
                int w = TextRenderer.MeasureText(_combo.Items[i].ToString(),
                    _combo.Font).Width;
                if (w > maxText) maxText = w;
            }
            int needed = maxText + SystemInformation.VerticalScrollBarWidth + S(24);
            _combo.DropDownWidth = Math.Min(Math.Max(needed, S(200)),
                Math.Max(S(140), availW));

            LayoutControls();
            int cx = _workArea.Left + Math.Max(0, (_workArea.Width - ClientSize.Width) / 2);
            int cy = _workArea.Top + Math.Max(0, (_workArea.Height - ClientSize.Height) / 2);
            Location = new Point(cx, cy);
        }

        // Header drag: moves the whole border-less dialog by the same delta as
        // the pointer, guarding against move while a modal is closing.
        private void HeaderMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStart = Cursor.Position;
            _formStart = Location;
        }

        private void HeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point cur = Cursor.Position;
            Location = new Point(_formStart.X + (cur.X - _dragStart.X),
                _formStart.Y + (cur.Y - _dragStart.Y));
        }

        private void HeaderMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _dragging = false;
        }

        // UX015: the closed combo can truncate long candidate text; a
        // full-text tooltip follows the current selection and updates on every
        // selection change.
        private void UpdateSelectionTooltip()
        {
            if (_combo == null || _tip == null) return;
            ComboItem item = _combo.SelectedItem as ComboItem;
            _tip.SetToolTip(_combo,
                item == null || item.Text == null ? "" : item.Text);
        }

        // UX015: one layout pass. Reproduces the fixed 430x208 geometry
        // whenever it fits; reflows with a scrollable middle area and
        // bottom-anchored save / cancel when the work area is smaller, so the
        // buttons are never pushed out of the window.
        private void LayoutControls()
        {
            int cw = ClientSize.Width;
            int ch = ClientSize.Height;
            _headerPanel.SetBounds(0, 0, cw, S(46));
            _closeBtn.SetBounds(cw - S(6) - S(34), S(6), S(34), S(34));
            _heading.SetBounds(S(20), 0,
                Math.Max(S(40), cw - S(20) - _closeBtn.Width - S(26)), S(46));
            int btnH = S(34);
            int bottomPad = S(8);
            int hostTop = S(46);
            int hostH = Math.Max(S(40), ch - btnH - bottomPad - hostTop);
            _contentHost.SetBounds(0, hostTop, cw, hostH);
            int contentW = Math.Max(S(80), cw - S(40));
            _hint.SetBounds(S(20), S(16), contentW, S(22));
            _combo.SetBounds(S(20), S(44), contentW, S(28));
            _error.SetBounds(S(20), S(78), contentW, S(36));
            // UX015/T042: 84dp buttons normally, narrowed only if the client
            // width cannot hold the pair at the effective scale — the two
            // buttons never overlap and never leave the window.
            int btnW = Math.Min(S(84), Math.Max(S(20), (cw - S(26)) / 2));
            _cancelBtn.SetBounds(cw - S(20) - btnW, ch - bottomPad - btnH, btnW, btnH);
            _saveBtn.SetBounds(Math.Max(S(8), _cancelBtn.Left - S(6) - btnW),
                ch - bottomPad - btnH, btnW, btnH);
            SizeErrorForReadability();
        }

        private void SetError(string text)
        {
            _error.Text = text;
            SizeErrorForReadability();
        }

        // UX015: longer inline errors stay readable — the label wraps and
        // grows (the content area scrolls if needed) instead of clipping.
        private void SizeErrorForReadability()
        {
            if (_error == null) return;
            if (string.IsNullOrEmpty(_error.Text)) { _error.Height = S(20); return; }
            Size need = TextRenderer.MeasureText(_error.Text, _error.Font,
                new Size(Math.Max(40, _error.Width), 0), TextFormatFlags.WordBreak);
            _error.Height = Math.Max(S(20), need.Height + S(6));
        }

        private int S(int logical)
        {
            return (int)Math.Round(logical * _scale);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        // DPI-scaled font cache: the dialog must scale with the system DPI like
        // the rest of the UI instead of staying at raw point sizes.
        private Font F(float pt, bool bold)
        {
            string key = pt.ToString(CultureInfo.InvariantCulture) + (bold ? "b" : "r");
            Font cached;
            if (_fonts.TryGetValue(key, out cached)) return cached;
            Font f = new Font("Microsoft YaHei UI", (float)(pt * _scale),
                bold ? FontStyle.Bold : FontStyle.Regular);
            _fonts[key] = f;
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tip != null) { try { _tip.Dispose(); } catch (Exception) { } }
                foreach (Font f in _fonts.Values)
                {
                    try { f.Dispose(); } catch (Exception) { }
                }
                _fonts.Clear();
            }
            base.Dispose(disposing);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            ComboItem item = _combo.SelectedItem as ComboItem;
            if (item == null || item.Entry == null)
            {
                SetError("暂无数据，无法选择。");
                return;
            }
            if (!item.Entry.Selectable)
            {
                SetError("该项目前不可用（获取失败 / 未订阅），请选择其他周期。");
                return;
            }
            FloatingSettings s = new FloatingSettings();
            s.Version = FloatingSettingsStore.FormatVersion;
            s.ProductKey = item.Entry.ProductKey;
            s.PeriodLabel = item.Entry.Label;
            if (_save != null && !_save(s))
            {
                SetError("保存失败，未能记住本次选择。");
                return;
            }
            ResultSettings = s;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ---- test hooks ----

        internal int CandidateCountForTest { get { return _combo.Items.Count; } }
        internal string CandidateTextForTest(int i)
        {
            return (i >= 0 && i < _combo.Items.Count) ? _combo.Items[i].ToString() : null;
        }
        internal bool SelectForTest(int i)
        {
            if (i < 0 || i >= _combo.Items.Count) return false;
            _combo.SelectedIndex = i;
            return true;
        }
        internal bool SaveForTest()
        {
            OnSaveClick(null, EventArgs.Empty);
            return DialogResult == DialogResult.OK;
        }
        internal string ErrorForTest { get { return _error.Text; } }
        internal ComboBox ComboForTest { get { return _combo; } }
        internal System.Windows.Forms.Control FocusedForTest { get { return ActiveControl; } }
        internal int HeaderHeightForTest { get { return _headerPanel == null ? 0 : _headerPanel.Height; } }
        internal Rectangle WorkAreaForTest { get { return _workArea; } }
        internal double ScaleForTest { get { return _scale; } }
        internal System.Windows.Forms.Control SaveButtonForTest { get { return _saveBtn; } }
        internal System.Windows.Forms.Control CancelButtonForTest { get { return _cancelBtn; } }
        internal System.Windows.Forms.Control CloseButtonForTest { get { return _closeBtn; } }
        internal System.Windows.Forms.Control HeaderForTest { get { return _headerPanel; } }
        internal System.Windows.Forms.Control ErrorLabelForTest { get { return _error; } }
        internal System.Windows.Forms.Control HostForTest { get { return _contentHost; } }
        internal System.Windows.Forms.Control HeadingForTest { get { return _heading; } }
        internal System.Windows.Forms.Control HintForTest { get { return _hint; } }
        internal bool SaveEnabledForTest { get { return _saveBtn != null && _saveBtn.Enabled; } }
        internal string TooltipTextForTest
        {
            get { return _tip == null || _combo == null ? null : _tip.GetToolTip(_combo); }
        }
        internal int DropDownWidthForTest { get { return _combo == null ? 0 : _combo.DropDownWidth; } }
    }

    // Floating circle window. Consumes the SAME PanelView as the details panel
    // (no extra controller) and owns selection / settings. Events let TrayApp
    // wire the shared context menu; opening the settings dialog is guarded so a
    // second request cannot stack a duplicate and ExitApp disposes it.
    internal class FloatingQuotaForm : Form
    {
        // Appended to every circle tooltip so the interaction is discoverable
        // (details on click, settings on right-click) without visual clutter.
        internal const string CircleHint = "点击查看全部额度，右键设置";

        private readonly FloatingCircleControl _circle;
        private readonly Func<FloatingSettings> _load;
        private readonly Func<FloatingSettings, bool> _save;
        // v0.8 UX016: injectable preference load / save (offline tests use
        // null-returning / no-op callbacks; production uses the store).
        private readonly Func<FloatingPreferences> _prefsLoad;
        private readonly Func<FloatingPreferences, bool> _prefsSave;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _lockItem;
        // v0.9 UX017: checkable "减少动画" item, right after the lock item.
        private ToolStripMenuItem _motionItem;
        // v0.15 UX023: default menu (used when no owner shares one) mirrors the
        // new top level [设置(子菜单), 退出]: the 设置 submenu holds the
        // 悬浮内容 chooser plus the existing toggles.
        private ToolStripMenuItem _settingsItem;
        private ToolStripMenuItem _contentItem;
        private ToolStripMenuItem _homeItem;
        private ToolStripMenuItem _toggleItem;
        // v0.15 UX023: observable "设置 clicked -> native side dropdown" intent
        // (no real popup is created offline, where the strip is never shown).
        private bool _settingsDropDownRequested;
        // v0.15 UX023: bumped whenever ApplyModelView replaces the candidate
        // list, so a menu opened over an older snapshot can never save an item
        // that is no longer backed by the current entries (same key included).
        private int _contentGeneration;
        // Deterministic release bookkeeping for the rebuilt 悬浮内容 items.
        private int _contentItemsReleased;
        // Removed 悬浮内容 items awaiting a safe release. A single shared,
        // coalesced drain is posted (never one dangling closure per rebuild);
        // Dispose drains synchronously so nothing is left undisposed.
        private readonly List<ToolStripItem> _pendingContentItems = new List<ToolStripItem>();
        private bool _contentDrainPosted;
        // Candidate / identity signature of the last built menu; an equivalent
        // refresh (e.g. the 10s poll) must NOT invalidate an open selection.
        private string _contentSignature;
        private string _contentIdentitySignature;
        private FloatingSettings _stored;
        private FloatingSettings _selected;
        private List<FloatingEntry> _entries = new List<FloatingEntry>();
        private PanelView _view;
        private FloatingSettingsForm _settingsForm;
        private bool _positioned;
        private bool _autoHiddenForDetails;
        private bool _positionLocked;
        // v0.9 UX017: reduce-motion flag restored from / saved with the prefs.
        private bool _reduceMotion;
        private string _lockHint;

        public event EventHandler DetailsRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ExitRequested;
        // v0.8 UX016: raised after any lock toggle attempt (success or save
        // failure) so the tray menu re-syncs its check against the REAL
        // current state.
        public event EventHandler PositionLockChanged;
        // v0.8 UX016 fix: raised ONLY when a lock save fails. The owner uses
        // it to surface a short tray notification while the circle is hidden
        // (the circle tooltip path is useless there). The hint text itself
        // stays short and readable - never a raw exception.
        public event EventHandler LockSaveFailed;
        // v0.9 UX017: raised after any reduce-motion toggle attempt (success
        // or save failure) so the tray menu re-syncs its check against the
        // REAL current state.
        public event EventHandler ReduceMotionChanged;
        // v0.9 UX017: raised ONLY when a reduce-motion save fails. The owner
        // surfaces a DISTINCT short tray notification (never the lock-failure
        // wording) while the circle is hidden.
        public event EventHandler MotionSaveFailed;
        // v0.15 UX023: raised ONLY when a 悬浮内容 menu save fails. The owner
        // surfaces a DISTINCT short tray notification (never the lock / motion
        // wording) while the circle is hidden.
        public event EventHandler ContentSaveFailed;
        // v0.15 UX023: raised after any 悬浮内容 menu selection attempt so a
        // shared (tray) submenu can rebuild its checks from the real state.
        public event EventHandler ContentChanged;
        // v0.5 UX013: forwards the real circle's VisibleChanged. The wrapper's
        // own (1x1) window visibility does not track the circle, so callers must
        // subscribe here to observe show / hide transitions.
        public event EventHandler CircleVisibleChanged;

        public FloatingQuotaForm() : this(null, null, null, null) { }

        // load == null / save == null -> production store. Offline tests inject
        // null-returning / no-op callbacks so no real preference file is touched.
        public FloatingQuotaForm(Func<FloatingSettings> load,
            Func<FloatingSettings, bool> save) : this(load, save, null, null) { }

        // prefsLoad == null / prefsSave == null -> production preference store
        // (FloatingPreferencesStore). A corrupt / missing file means unlocked;
        // construction never writes.
        public FloatingQuotaForm(Func<FloatingSettings> load,
            Func<FloatingSettings, bool> save,
            Func<FloatingPreferences> prefsLoad,
            Func<FloatingPreferences, bool> prefsSave)
        {
            _load = load ?? FloatingSettingsStore.Load;
            _save = save ?? FloatingSettingsStore.Save;
            _prefsLoad = prefsLoad ?? FloatingPreferencesStore.Load;
            _prefsSave = prefsSave ?? FloatingPreferencesStore.Save;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1, 1);
            Text = "方舟剩余额度";
            _circle = new FloatingCircleControl();
            _circle.VisibleChanged += delegate
            {
                UpdateToggleItemText();
                if (CircleVisibleChanged != null) CircleVisibleChanged(this, EventArgs.Empty);
            };
            _circle.DetailsRequested += delegate
            {
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            };
            _menu = BuildDefaultMenu();
            _circle.ContextMenuStrip = _menu;
            _circle.AllowClose = false;
            _circle.ExplicitHideRequested += delegate { _autoHiddenForDetails = false; };

            _stored = _load();
            _selected = _stored;
            // v0.8 UX016: restore the position lock (default unlocked; corrupt
            // files fall back to unlocked). Read-only: no write on start.
            FloatingPreferences prefs = _prefsLoad();
            _positionLocked = prefs != null && prefs.PositionLocked;
            // v0.9 UX017: restore reduce motion the same way (default off; a
            // legacy format 1 file arrives migrated to the format 2 shape by
            // the store). Read-only: no write on start.
            _reduceMotion = prefs != null && prefs.ReduceMotion;
            _circle.PositionLocked = _positionLocked;
            _circle.SetReduceMotion(_reduceMotion);
            SyncPrefMenuChecks();
        }

        private ContextMenuStrip BuildDefaultMenu()
        {
            // v0.15 UX023: top level is only [设置(子菜单), 退出]; the left
            // click (details) is unchanged and the default menu never opens the
            // settings modal. The 设置 submenu expands natively to the side and
            // holds the 悬浮内容 chooser plus the existing toggles.
            ContextMenuStrip menu = new ContextMenuStrip();
            _settingsItem = new ToolStripMenuItem("设置");
            _contentItem = new ToolStripMenuItem("悬浮内容");
            _settingsItem.DropDownItems.Add(_contentItem);
            _settingsItem.DropDownItems.Add(new ToolStripSeparator());
            // v0.8 UX016: shared checkable "锁定位置" (same state / handler as
            // the tray menu item; TrayApp syncs via PositionLockChanged).
            _lockItem = new ToolStripMenuItem("锁定位置");
            _lockItem.CheckOnClick = false; // state is owned by the handler
            _lockItem.Click += delegate { TogglePositionLocked(); };
            _settingsItem.DropDownItems.Add(_lockItem);
            // v0.9 UX017: checkable "减少动画"; same state / handler as the
            // tray menu item.
            _motionItem = new ToolStripMenuItem("减少动画");
            _motionItem.CheckOnClick = false; // state is owned by the handler
            _motionItem.Click += delegate { ToggleReduceMotion(); };
            _settingsItem.DropDownItems.Add(_motionItem);
            // v0.12 UX020: "悬浮窗归位" (moved into the 设置 submenu).
            _homeItem = new ToolStripMenuItem("悬浮窗归位", null,
                delegate { RepositionCircleHome(); UpdateToggleItemText(); });
            _settingsItem.DropDownItems.Add(_homeItem);
            // v0.13 UX021: show / hide toggle (moved into the 设置 submenu).
            _toggleItem = new ToolStripMenuItem("隐藏悬浮窗", null,
                delegate { ToggleCircleForMenu(); });
            _settingsItem.DropDownItems.Add(_toggleItem);
            // Explicit native side expansion on click (never a modal). Guarded
            // so an offline PerformClick on a never-shown strip records the
            // intent without creating a real popup window.
            _settingsItem.Click += delegate { RequestSettingsDropDown(); };
            _settingsItem.DropDownOpening += delegate
            {
                PopulateContentMenu(_contentItem);
                SyncPrefMenuChecks();
                UpdateToggleItemText();
            };
            menu.Items.Add(_settingsItem);
            menu.Items.Add("退出", null, delegate { RaiseExit(); });
            PopulateContentMenu(_contentItem);
            SyncPrefMenuChecks();
            return menu;
        }

        // v0.15 UX023: explicit native side expansion for the 设置 submenu. In
        // production the strip is visible so ShowDropDown really opens the
        // native dropdown; offline (never-shown strip) only the intent flag is
        // recorded, so no orphan popup window is created in tests.
        private void RequestSettingsDropDown()
        {
            _settingsDropDownRequested = true;
            try
            {
                if (_menu != null && _menu.Visible) _settingsItem.ShowDropDown();
            }
            catch (Exception) { }
        }

        // v0.15 UX023: rebuild the 悬浮内容 candidates from the CURRENT
        // snapshot (called on every submenu open and after a selection). A menu
        // opened over an old snapshot is re-validated at click time, so a
        // period that disappears while the menu is open can never be persisted.
        // Zero query, no modal.
        public void PopulateContentMenu(ToolStripMenuItem contentItem)
        {
            if (contentItem == null) return;
            ClearContentItems(contentItem.DropDownItems);
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(_entries);
            if (candidates.Count == 0)
            {
                contentItem.Enabled = false; // no data -> disabled
                return;
            }
            contentItem.Enabled = true;
            int generation = _contentGeneration;
            foreach (FloatingEntry entry in candidates)
            {
                FloatingEntry captured = entry;
                ToolStripMenuItem item = new ToolStripMenuItem(DisplayNames.Period(captured.Label));
                item.CheckOnClick = false; // state is owned by the handler
                item.Checked = IsCurrentContent(captured);
                item.Click += delegate { ApplyContentSelection(captured, generation); };
                contentItem.DropDownItems.Add(item);
            }
        }

        // v0.15 UX023: rebuild deterministically releases the old items, but the
        // release is deferred so an item currently raising its own Click handler
        // is never disposed mid-handler (a real crash in WinForms). Removed items
        // go to a single queue and one shared drain is posted; Dispose drains the
        // queue synchronously, and a late posted drain on an empty queue is a
        // harmless no-op.
        private void ClearContentItems(ToolStripItemCollection items)
        {
            if (items == null || items.Count == 0) return;
            for (int i = 0; i < items.Count; i++) _pendingContentItems.Add(items[i]);
            items.Clear();
            RequestContentDrain();
        }

        private void RequestContentDrain()
        {
            if (_pendingContentItems.Count == 0) return;
            if (IsDisposed || Disposing) { DrainPendingContentItems(); return; }
            if (_contentDrainPosted) return; // one posted drain coalesces them all
            try
            {
                IntPtr h = Handle;
                GC.KeepAlive(h);
                if (IsDisposed || Disposing) { DrainPendingContentItems(); return; }
                _contentDrainPosted = true;
                BeginInvoke(new Action(delegate
                {
                    _contentDrainPosted = false;
                    DrainPendingContentItems();
                }));
            }
            catch (Exception) { _contentDrainPosted = false; DrainPendingContentItems(); }
        }

        // Releases every queued item. Safe on an empty queue and safe to call
        // from Dispose; never touches control state, so a late posted callback
        // after the form is gone only drains an (empty) list.
        private void DrainPendingContentItems()
        {
            for (int i = 0; i < _pendingContentItems.Count; i++)
            {
                try { _pendingContentItems[i].Dispose(); } catch (Exception) { }
                _contentItemsReleased++;
            }
            _pendingContentItems.Clear();
        }

        private bool IsCurrentContent(FloatingEntry e)
        {
            return _selected != null && _selected.ProductKey == e.ProductKey
                && _selected.PeriodLabel == e.Label;
        }

        private FloatingEntry FindCurrentContent(string productKey, string label)
        {
            foreach (FloatingEntry e in _entries)
            {
                if (e.ProductKey == productKey && e.Label == label) return e;
            }
            return null;
        }

        // v0.15 UX023: direct menu selection. Saves FIRST; only a successful
        // save flips _stored / _selected. A candidate that vanished or became
        // unavailable while the menu was open is rejected (never saved), and a
        // failed save keeps the old value plus a short readable hint. Zero
        // query, never opens a modal.
        public void ApplyContentSelection(FloatingEntry entry)
        {
            // Direct callers use the current candidate source.
            ApplyContentSelection(entry, _contentGeneration);
        }

        // The captured generation ties the click to the candidate list it was
        // built from: any ApplyModelView (new snapshot OR identity switch, same
        // key included) invalidates it. The entry is then re-validated in the
        // CURRENT entries before anything is persisted.
        public void ApplyContentSelection(FloatingEntry entry, int generation)
        {
            if (entry == null) return;
            if (generation != _contentGeneration)
            {
                ShowLockHint("悬浮内容不可用");
                return;
            }
            FloatingEntry current = FindCurrentContent(entry.ProductKey, entry.Label);
            if (current == null || !current.Selectable)
            {
                ShowLockHint("悬浮内容不可用");
                return;
            }
            if (IsCurrentContent(current)) return; // same target: skip the save
            FloatingSettings s = new FloatingSettings();
            s.Version = FloatingSettingsStore.FormatVersion;
            s.ProductKey = current.ProductKey;
            s.PeriodLabel = current.Label;
            bool saved;
            try { saved = _save != null && _save(s); }
            catch (Exception) { saved = false; }
            if (saved)
            {
                _stored = s;
                _selected = s;
                UpdateCircleDisplay();
                ShowLockHint("已切换悬浮内容");
            }
            else
            {
                ShowLockHint("悬浮内容未保存");
                if (ContentSaveFailed != null) ContentSaveFailed(this, EventArgs.Empty);
            }
            PopulateContentMenu(_contentItem);
            SyncPrefMenuChecks();
            UpdateToggleItemText();
            if (ContentChanged != null) ContentChanged(this, EventArgs.Empty);
        }

        private void ToggleCircleForMenu()
        {
            ToggleCircle();
            UpdateToggleItemText();
        }

        // The show / hide toggle label mirrors the real circle state (kept in
        // sync from the circle VisibleChanged hook and on every submenu open).
        private void UpdateToggleItemText()
        {
            if (_toggleItem == null) return;
            _toggleItem.Text = CircleVisible ? "隐藏悬浮窗" : "显示悬浮窗";
        }

        // Shares the tray menu (TrayApp owns it) with the circle so both show the
        // same button group. The default menu is disposed with the form.
        public void SetContextMenuStrip(ContextMenuStrip menu)
        {
            if (menu == null) return;
            _circle.ContextMenuStrip = menu;
        }

        private void RaiseDetails() { if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty); }
        private void RaiseSettings() { if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); }
        private void RaiseExit() { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); }

        // ---- v0.8 UX016 position lock ----

        public bool PositionLocked { get { return _positionLocked; } }

        public void TogglePositionLocked()
        {
            SetPositionLocked(!_positionLocked);
        }

        // The lock is persisted BEFORE the state flips: a failed write keeps
        // the old state and menu checks, and shows a short readable hint
        // (never the raw exception, never a claim that it was saved). Same-
        // value calls skip the save entirely.
        public void SetPositionLocked(bool locked)
        {
            if (locked != _positionLocked)
            {
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = locked;
                // v0.9 UX017: format 2 saves always carry BOTH flags, so a
                // lock toggle can never overwrite the reduce-motion choice
                // (and vice versa - see SetReduceMotion).
                p.ReduceMotion = _reduceMotion;
                bool saved;
                try { saved = _prefsSave(p); }
                catch (Exception) { saved = false; }
                if (saved)
                {
                    _positionLocked = locked;
                    _circle.PositionLocked = locked;
                    ShowLockHint(locked ? "已锁定位置" : "已解锁位置");
                }
                else
                {
                    ShowLockHint("锁定状态未保存");
                    if (LockSaveFailed != null) LockSaveFailed(this, EventArgs.Empty);
                }
            }
            SyncPrefMenuChecks();
            if (PositionLockChanged != null) PositionLockChanged(this, EventArgs.Empty);
        }

        // v0.9 UX017: syncs BOTH preference checks (lock + reduce motion);
        // called at the same points as before (ctor, build, toggles).
        private void SyncPrefMenuChecks()
        {
            if (_lockItem != null) _lockItem.Checked = _positionLocked;
            if (_motionItem != null) _motionItem.Checked = _reduceMotion;
        }

        // ---- v0.9 UX017 reduce motion ----

        public bool ReduceMotion { get { return _reduceMotion; } }

        public void ToggleReduceMotion()
        {
            SetReduceMotion(!_reduceMotion);
        }

        // Both flags are always saved together (format 2): a reduce-motion
        // save carries the current lock state and vice versa, so neither
        // toggle can overwrite the other. The state flips only after a
        // successful save; a failed save keeps every old preference and both
        // menu checks, shows a short motion-specific hint (never the lock
        // wording, never a raw exception), and raises MotionSaveFailed for
        // the hidden-circle tray notification. Same-value calls skip the
        // save entirely.
        public void SetReduceMotion(bool on)
        {
            if (on != _reduceMotion)
            {
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = _positionLocked;
                p.ReduceMotion = on;
                bool saved;
                try { saved = _prefsSave(p); }
                catch (Exception) { saved = false; }
                if (saved)
                {
                    _reduceMotion = on;
                    _circle.SetReduceMotion(on);
                    ShowLockHint(on ? "已减少动画" : "已恢复动画");
                }
                else
                {
                    ShowLockHint("动画设置未保存");
                    if (MotionSaveFailed != null) MotionSaveFailed(this, EventArgs.Empty);
                }
            }
            SyncPrefMenuChecks();
            if (ReduceMotionChanged != null) ReduceMotionChanged(this, EventArgs.Empty);
        }

        private void ShowLockHint(string text)
        {
            _lockHint = text;
            _circle.ShowStatusHint(text);
        }

        internal string LockHintForTest { get { return _lockHint; } }
        internal bool DefaultMenuLockCheckedForTest
        {
            get { return _lockItem != null && _lockItem.Checked; }
        }
        // v0.9 UX017 test hook: default (circle) menu motion check.
        internal bool DefaultMenuMotionCheckedForTest
        {
            get { return _motionItem != null && _motionItem.Checked; }
        }
        // v0.15 UX023 test hooks: the default menu's top level and its 设置
        // submenu (悬浮内容 chooser + existing toggles).
        internal bool SettingsDropDownRequestedForTest { get { return _settingsDropDownRequested; } }
        // v0.15 UX023: count of rebuilt 悬浮内容 items deterministically released.
        internal int ContentItemsReleasedForTest { get { return _contentItemsReleased; } }
        internal int ContentGenerationForTest { get { return _contentGeneration; } }
        internal int DefaultMenuTopCountForTest { get { return _menu == null ? 0 : _menu.Items.Count; } }
        internal string DefaultMenuTopTextForTest(int i)
        {
            return (_menu != null && i >= 0 && i < _menu.Items.Count) ? _menu.Items[i].Text : null;
        }
        internal int DefaultMenuSettingsCountForTest
        {
            get { return _settingsItem == null ? 0 : _settingsItem.DropDownItems.Count; }
        }
        internal string DefaultMenuSettingsTextForTest(int i)
        {
            return (_settingsItem != null && i >= 0 && i < _settingsItem.DropDownItems.Count)
                ? _settingsItem.DropDownItems[i].Text : null;
        }
        internal void PerformDefaultMenuSettingsForTest()
        {
            if (_settingsItem != null) _settingsItem.PerformClick();
        }
        internal int DefaultMenuContentCountForTest
        {
            get { return _contentItem == null ? 0 : _contentItem.DropDownItems.Count; }
        }
        internal bool DefaultMenuContentEnabledForTest
        {
            get { return _contentItem != null && _contentItem.Enabled; }
        }
        internal string DefaultMenuContentTextForTest(int i)
        {
            return (_contentItem != null && i >= 0 && i < _contentItem.DropDownItems.Count)
                ? _contentItem.DropDownItems[i].Text : null;
        }
        internal bool DefaultMenuContentCheckedForTest(int i)
        {
            if (_contentItem == null || i < 0 || i >= _contentItem.DropDownItems.Count) return false;
            ToolStripMenuItem it = _contentItem.DropDownItems[i] as ToolStripMenuItem;
            return it != null && it.Checked;
        }
        internal void PerformDefaultMenuContentForTest(int i)
        {
            if (_contentItem == null || i < 0 || i >= _contentItem.DropDownItems.Count) return;
            ToolStripMenuItem it = _contentItem.DropDownItems[i] as ToolStripMenuItem;
            if (it != null) it.PerformClick();
        }
        // Expands the default 设置 submenu content (rebuilds 悬浮内容 from the
        // current snapshot) without a real popup window.
        internal void OpenDefaultMenuContentForTest()
        {
            PopulateContentMenu(_contentItem);
        }
        internal string DefaultMenuToggleTextForTest
        {
            get { return _toggleItem == null ? null : _toggleItem.Text; }
        }
        // v0.12 UX020 test hook: a stale details auto-hide flag must be
        // cleared by an explicit re-home.
        internal bool AutoHiddenForDetailsForTest
        {
            get { return _autoHiddenForDetails; }
        }
        // v0.12 UX020 fix hook: the real once-per-session positioning state
        // (ShowCircleAtForTest intentionally does NOT set it, so a fresh
        // form is a genuine not-positioned fixture).
        internal bool PositionedForTest { get { return _positioned; } }

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

        // ---- model binding (same PanelView as the details panel) ----

        public void ApplyModelView(PanelView v)
        {
            _view = v;
            QuotaSnapshot snap = v == null ? null : v.Data;
            List<FloatingEntry> built = FloatingSelection.Build(snap);
            string signature = ContentSignature(built);
            string identitySignature = IdentitySignature(v);
            _entries = built;
            _selected = ResolveSelection();
            UpdateCircleDisplay();
            // A same-semantics refresh (the routine 10s poll) must NOT rebuild
            // the menu or invalidate a selection the user is making. Only a real
            // candidate-set change or a real scope change (the PanelView's
            // ScopeFingerprint / IdentityUnknown, never the display IdentityHint)
            // bumps the generation and rebuilds; the circle display above is
            // always refreshed.
            if (signature == _contentSignature
                && identitySignature == _contentIdentitySignature) return;
            _contentSignature = signature;
            _contentIdentitySignature = identitySignature;
            _contentGeneration++;
            PopulateContentMenu(_contentItem);
            if (ContentChanged != null) ContentChanged(this, EventArgs.Empty);
        }

        // Stable signature of the SELECTABLE candidate set (key + label in
        // order). Values / counts may move every poll without changing which
        // periods a user can pick, so they are deliberately excluded.
        private static string ContentSignature(List<FloatingEntry> entries)
        {
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(entries);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                sb.Append(candidates[i].ProductKey).Append('\u0001')
                    .Append(candidates[i].Label).Append('\u0002');
            }
            return sb.ToString();
        }

        // T-057: the scope authority of the built menu is the fingerprint of the
        // identity scope the PanelView's Data belongs to - NOT the friendly
        // IdentityHint (a real owner change keeps the same hint when type /
        // region / profile name are unchanged). A null fingerprint means "no
        // confirmed scope for this Data", which is distinct from every real
        // fingerprint, so an unconfirmed result can never keep an old-scope
        // selection valid. The value is opaque and is never rendered or logged.
        private static string IdentitySignature(PanelView v)
        {
            if (v == null) return "";
            return (v.ScopeFingerprint ?? "") + "\u0003" + (v.IdentityUnknown ? "1" : "0");
        }

        private FloatingSettings ResolveSelection()
        {
            if (_stored != null) return _stored; // keep the user's choice verbatim
            FloatingEntry def = FloatingSelection.Default(_entries);
            if (def == null) return null;
            FloatingSettings s = new FloatingSettings();
            s.Version = FloatingSettingsStore.FormatVersion;
            s.ProductKey = def.ProductKey;
            s.PeriodLabel = def.Label;
            return s; // runtime default only; never persisted
        }

        private void UpdateCircleDisplay()
        {
            FloatingDisplay d = new FloatingDisplay();
            if (_entries.Count == 0)
            {
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.Tooltip = AppendHint("暂无数据");
                _circle.SetDisplay(d);
                return;
            }
            if (_selected == null)
            {
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.Tooltip = AppendHint("暂无数据");
                _circle.SetDisplay(d);
                return;
            }

            FloatingEntry e = FloatingSelection.Find(_entries, _selected);
            if (e == null)
            {
                // Chosen target no longer present: keep the choice, ask the user
                // to reselect. Never silently switch to another target.
                d.HasData = false;
                d.PercentText = "暂无数据";
                d.PeriodCaption = DisplayNames.Period(_selected.PeriodLabel);
                d.Caption = d.PeriodCaption;
                d.Tooltip = AppendHint("已选目标暂无数据，请在设置中重新选择。");
                _circle.SetDisplay(d);
                return;
            }

            d.HasData = true;
            d.ProductCaption = e.ProductShort;
            d.PeriodCaption = e.PeriodTitle;
            d.Caption = e.ShortCaption;
            if (!e.HasTrustedValue)
            {
                d.PercentKnown = false;
                d.PercentText = "剩余未知";
                d.Tooltip = BuildUnknownTooltip(e);
            }
            else
            {
                d.PercentKnown = true;
                d.Percent = e.Percent;
                d.PercentText = CirclePercent(e.Percent);
                d.Tooltip = BuildValueTooltip(e);
            }
            _circle.SetDisplay(d);
        }

        private static string AppendHint(string baseText)
        {
            if (string.IsNullOrEmpty(baseText)) return CircleHint;
            return baseText + "\n" + CircleHint;
        }

        private string BuildValueTooltip(FloatingEntry e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(e.ShortCaption).Append('\n');
            sb.Append("剩余 ").Append(PercentFormat.Remaining(e.Percent));
            AppendFreshness(sb, false);
            sb.Append('\n').Append(CircleHint);
            return sb.ToString();
        }

        private string BuildUnknownTooltip(FloatingEntry e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(e.ShortCaption).Append('\n');
            sb.Append(e.StatusText);
            AppendFreshness(sb, e.PeriodError || e.ProductError);
            sb.Append('\n').Append(CircleHint);
            return sb.ToString();
        }

        private void AppendFreshness(StringBuilder sb, bool failed)
        {
            if (_view == null) return;
            if (_view.Data != null && _view.Data.FetchedAt != DateTime.MinValue)
            {
                sb.Append('\n');
                sb.Append(_view.FromCache ? "上次数据 " : "最后更新 ");
                sb.Append(DisplayNames.FormatTime(_view.Data.FetchedAt));
            }
            // Never let a limited / cached / failed result look freshly
            // confirmed: surface the PanelView's own safe note (no process state,
            // no raw identity). Fall back to a short line only if the model has
            // no note of its own.
            if (!string.IsNullOrEmpty(_view.Note))
            {
                sb.Append('\n').Append(_view.Note);
            }
            else if (failed || _view.State == PanelState.StaleError
                || _view.State == PanelState.CancelledStale)
            {
                sb.Append('\n').Append("更新失败，显示上次数据。");
            }
            if (_view.IdentityUnknown) sb.Append('\n').Append("身份未完全确认");
        }

        // 0 -> "0%", 100 -> "100%"; never rounds a real value into a false 100.
        public static string CirclePercent(double v)
        {
            if (v <= 0) return "0%";
            if (v >= 100) return "100%";
            if (v < 1) return "<1%";
            int r = (int)Math.Round(v);
            if (r >= 100) r = 99;
            if (r < 1) return "<1%";
            return r.ToString(CultureInfo.InvariantCulture) + "%";
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

        private Rectangle _pendingDetailsBounds;

        // ---- settings ----

        public void OpenSettings()
        {
            // UX015: no-owner call keeps the legacy primary-screen default.
            OpenSettings(null);
        }

        // UX015: the caller supplies the window that OWNS the modal — the
        // details panel when opened from its header, the floating window from
        // the circle / tray. The owner decides the dialog's screen, DPI and
        // work area, and where focus returns when it closes. Repeat requests
        // only activate the existing single instance; a disposing form never
        // resurrects it. Opening / saving / cancelling never queries.
        public void OpenSettings(IWin32Window owner)
        {
            if (IsDisposed || Disposing) return;
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                try { _settingsForm.Activate(); } catch (Exception) { }
                return;
            }
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(_entries);
            Screen scr = null;
            try
            {
                Control oc = owner as Control;
                if (oc != null && !oc.IsDisposed) scr = Screen.FromControl(oc);
                else if (owner != null && owner.Handle != IntPtr.Zero)
                    scr = Screen.FromHandle(owner.Handle);
            }
            catch (Exception) { }
            if (scr == null) scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, _selected,
                _save, DpiUtil.GetScale(scr), scr.WorkingArea);
            _settingsForm = dlg;
            try
            {
                DialogResult r = dlg.ShowDialog(owner ?? (IWin32Window)this);
                if (r == DialogResult.OK && dlg.ResultSettings != null)
                {
                    _stored = dlg.ResultSettings;
                    _selected = _stored;
                    UpdateCircleDisplay();
                }
            }
            finally
            {
                _settingsForm = null;
                try { dlg.Dispose(); } catch (Exception) { }
            }
        }

        public void CloseSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                try { _settingsForm.Close(); } catch (Exception) { }
            }
            _settingsForm = null;
        }

        // ---- test hooks ----

        internal void ShowCircleAtForTest(Rectangle workArea, double scale)
        {
            _circle.ShowAt(workArea, scale);
        }

        internal void HideCircleForTest() { HideCircle(); }
        internal int EntryCountForTest { get { return _entries.Count; } }
        internal FloatingEntry SelectedEntryForTest
        {
            get { return FloatingSelection.Find(_entries, _selected); }
        }
        internal FloatingSettings StoredSettingsForTest { get { return _stored; } }
        internal FloatingCircleControl CircleForTest { get { return _circle; } }
        public Rectangle CircleBounds { get { return _circle.Bounds; } }
        // UX015/T042: the REAL circle surface. The shell opens the settings
        // modal owned by the window that actually carries the circle, so the
        // dialog's screen, DPI and placement follow the circle — the wrapper
        // form itself is a hidden 1x1 window whose location follows nothing.
        public System.Windows.Forms.Control CircleSurface
        {
            get { return _circle; }
        }
        // UX015: true while the single settings modal is up. TrayApp decides
        // from this when its details-suppression window may really end: a
        // repeat request that merely activated the dialog must not end it.
        public bool SettingsModalOpen
        {
            get { return _settingsForm != null && !_settingsForm.IsDisposed; }
        }
        internal bool SettingsOpenForTest
        {
            get { return SettingsModalOpen; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseSettings();
                // Release every queued 悬浮内容 item before the menus go, so no
                // removed item survives Dispose (a late posted drain then sees an
                // empty queue and exits safely).
                DrainPendingContentItems();
                if (_menu != null) { try { _menu.Dispose(); } catch (Exception) { } _menu = null; }
                if (_circle != null) { try { _circle.Dispose(); } catch (Exception) { } }
            }
            base.Dispose(disposing);
        }
    }
}
