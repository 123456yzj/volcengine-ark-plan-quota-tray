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
            BackColor = Color.White;
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
            header.BackColor = Color.White;
            _headerPanel = header;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiStyle.Border))
                    e.Graphics.DrawLine(pen, 0, 0, header.Width - 1, 0);
                using (Pen pen = new Pen(UiStyle.Divider))
                    e.Graphics.DrawLine(pen, 1, header.Height - 1, header.Width - 2, header.Height - 1);
            };
            // The border-less header is the drag handle (production mouse drag,
            // not just a static title). The close button is a child and its own
            // clicks do not bubble as a header MouseDown on it.
            header.MouseDown += HeaderMouseDown;
            header.MouseMove += HeaderMouseMove;
            header.MouseUp += HeaderMouseUp;
            Label heading = new Label();
            heading.Text = "悬浮内容";
            heading.Font = F(10f, true);
            heading.ForeColor = UiStyle.Navy;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.MouseDown += HeaderMouseDown;
            heading.MouseMove += HeaderMouseMove;
            heading.MouseUp += HeaderMouseUp;
            _heading = heading;
            Button closeBtn = new ModernButton();
            closeBtn.Text = "×";
            closeBtn.AccessibleName = "关闭设置";
            closeBtn.Font = F(14f, false);
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.BackColor = Color.White;
            closeBtn.ForeColor = UiStyle.Navy;
            closeBtn.FlatAppearance.MouseOverBackColor = UiStyle.TealLight;
            closeBtn.FlatAppearance.MouseDownBackColor = UiStyle.Selected;
            closeBtn.Click += delegate { Close(); };
            _closeBtn = closeBtn;
            header.Controls.Add(heading);
            header.Controls.Add(closeBtn);
            Controls.Add(header);

            // UX015: scrollable middle host — the header and the save / cancel
            // row stay fixed so a short work area can never hide the buttons.
            Panel host = new Panel();
            host.BackColor = Color.White;
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

            _combo = new ModernComboBox();
            _combo.DropDownStyle = ComboBoxStyle.DropDownList;
            _combo.AccessibleName = "显示目标";
            _combo.Font = F(9f, false);
            _combo.ForeColor = UiStyle.Navy;
            _combo.BackColor = Color.White;
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
            _error.ForeColor = UiStyle.Error;
            _error.Font = F(8.25f, false);
            host.Controls.Add(_error);

            Button saveBtn = new ModernButton();
            saveBtn.Text = "保存";
            saveBtn.AccessibleName = "保存显示目标";
            saveBtn.Font = F(9f, false);
            UiStyle.StyleButton(saveBtn, true);
            saveBtn.Click += OnSaveClick;
            _saveBtn = saveBtn;
            Controls.Add(saveBtn);

            Button cancelBtn = new ModernButton();
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
            _headerPanel.SetBounds(1, 0, cw - 2, S(46));
            _closeBtn.SetBounds(cw - S(6) - S(34), S(6), S(34), S(34));
            _heading.SetBounds(S(20), 1,
                Math.Max(S(40), cw - S(20) - _closeBtn.Width - S(26)), S(46) - 2);
            int btnH = S(34);
            int bottomPad = S(8);
            int hostTop = S(46);
            int hostH = Math.Max(S(40), ch - btnH - bottomPad - hostTop);
            _contentHost.SetBounds(1, hostTop, cw - 2, hostH);
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
            using (GraphicsPath path = UiStyle.RoundedRectangle(
                new Rectangle(0, 0, cw - 1, ch - 1), S(8)))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(UiStyle.Border))
            using (GraphicsPath path = UiStyle.RoundedRectangle(
                new Rectangle(0, 0, Width - 1, Height - 1), S(8)))
                e.Graphics.DrawPath(pen, path);
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
                if (Region != null) { Region.Dispose(); Region = null; }
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

}
