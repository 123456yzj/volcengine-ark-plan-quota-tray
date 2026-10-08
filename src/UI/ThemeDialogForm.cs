using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ArkLeft
{
    // v0.24 UX029: standalone theme dialog (accent preset + dark content
    // surface) reached from the first-level 主题 item of the circle / tray
    // context menu. Changes are PREVIEWED live so the user sees the effect;
    // 保存 persists the choice and 取消 / Esc / window close rolls the preview
    // back so no unconfirmed theme leaks into the UI.
    internal class ThemeDialogForm : Form
    {
        private readonly Action<int, bool> _themePreview;
        private readonly Func<int, bool, bool> _themeSave;
        private int _themeAccent;
        private bool _themeDark;
        private readonly int _initialAccent;
        private readonly bool _initialDark;
        private bool _themeCommitted;
        private double _scale = 1.0;
        private Rectangle _workArea;
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private Panel _headerPanel;
        private Label _heading;
        private Label _accentLabel;
        private Label _error;
        private Button _saveBtn;
        private Button _cancelBtn;
        private Button _closeBtn;
        private ModernComboBox _accentCombo;
        private CheckBox _darkCheck;
        private bool _dragging;
        private Point _dragStart;
        private Point _formStart;

        internal ThemeDialogForm(double scale, Rectangle workArea,
            Action<int, bool> themePreview, Func<int, bool, bool> themeSave,
            int accentIndex, bool darkMode)
        {
            _themePreview = themePreview;
            _themeSave = themeSave;
            _workArea = workArea;
            _themeAccent = ThemeCatalog.Normalize(accentIndex);
            _themeDark = darkMode;
            _initialAccent = _themeAccent;
            _initialDark = _themeDark;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            MaximizeBox = false;
            MinimizeBox = false;
            // Manual placement centred in the OWNER screen's work area (never a
            // blind CenterScreen on the primary display).
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = "主题";
            AccessibleName = "主题";
            BackColor = UiStyle.Surface;
            KeyPreview = true;

            // Physical margins first, then an effective layout scale so a small
            // work area shrinks the whole layout instead of overlapping.
            int marginX = Math.Max(4, Math.Min(32, _workArea.Width / 20));
            int marginY = Math.Max(4, Math.Min(32, _workArea.Height / 20));
            int availW = Math.Max(60, _workArea.Width - marginX * 2);
            int availH = Math.Max(60, _workArea.Height - marginY * 2);
            double les = Math.Min(scale, Math.Min(availW / 300.0, availH / 218.0));
            _scale = Math.Max(0.5, les);
            ClientSize = new Size(Math.Min(S(380), availW), Math.Min(S(218), availH));

            Panel header = new Panel();
            header.BackColor = UiStyle.Surface;
            _headerPanel = header;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiStyle.Border))
                    e.Graphics.DrawLine(pen, 0, 0, header.Width - 1, 0);
                using (Pen pen = new Pen(UiStyle.Divider))
                    e.Graphics.DrawLine(pen, 1, header.Height - 1, header.Width - 2, header.Height - 1);
            };
            header.MouseDown += HeaderMouseDown;
            header.MouseMove += HeaderMouseMove;
            header.MouseUp += HeaderMouseUp;
            Label heading = new Label();
            heading.Text = "主题";
            heading.Font = F(10f, true);
            heading.ForeColor = UiStyle.Navy;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.MouseDown += HeaderMouseDown;
            heading.MouseMove += HeaderMouseMove;
            heading.MouseUp += HeaderMouseUp;
            _heading = heading;
            Button closeBtn = new ModernButton();
            closeBtn.Text = "×";
            closeBtn.AccessibleName = "关闭主题设置";
            closeBtn.Font = F(14f, false);
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.BackColor = UiStyle.Surface;
            closeBtn.ForeColor = UiStyle.Navy;
            closeBtn.FlatAppearance.MouseOverBackColor = UiStyle.TealLight;
            closeBtn.FlatAppearance.MouseDownBackColor = UiStyle.Selected;
            closeBtn.Click += delegate { Close(); };
            _closeBtn = closeBtn;
            header.Controls.Add(heading);
            header.Controls.Add(closeBtn);
            Controls.Add(header);

            Label accentLabel = new Label();
            accentLabel.Text = "主题色：";
            accentLabel.Font = F(9f, false);
            accentLabel.AutoSize = false;
            accentLabel.ForeColor = UiStyle.Navy;
            _accentLabel = accentLabel;
            Controls.Add(accentLabel);

            _accentCombo = new ModernComboBox();
            _accentCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _accentCombo.AccessibleName = "主题强调色";
            _accentCombo.Font = F(9f, false);
            _accentCombo.ForeColor = UiStyle.Navy;
            _accentCombo.BackColor = UiStyle.Surface;
            for (int i = 0; i < ThemeCatalog.Count; i++)
                _accentCombo.Items.Add(ThemeCatalog.Name(i));
            _accentCombo.SelectedIndex = _themeAccent;
            _accentCombo.SelectedIndexChanged += delegate { OnThemeChanged(); };
            Controls.Add(_accentCombo);

            CheckBox darkCheck = new CheckBox();
            darkCheck.Text = "深色内容面";
            darkCheck.AccessibleName = "深色模式";
            darkCheck.AutoSize = false;
            darkCheck.Font = F(9f, false);
            darkCheck.ForeColor = UiStyle.Navy;
            darkCheck.BackColor = UiStyle.Surface;
            darkCheck.FlatStyle = FlatStyle.Flat;
            darkCheck.Checked = _themeDark;
            darkCheck.CheckedChanged += delegate { OnThemeChanged(); };
            _darkCheck = darkCheck;
            Controls.Add(darkCheck);

            _error = new Label();
            _error.AutoSize = false;
            _error.ForeColor = UiStyle.Error;
            _error.Font = F(8.25f, false);
            Controls.Add(_error);

            Button saveBtn = new ModernButton();
            saveBtn.Text = "保存";
            saveBtn.AccessibleName = "保存主题设置";
            saveBtn.Font = F(9f, false);
            UiStyle.StyleButton(saveBtn, true);
            saveBtn.Click += OnSaveClick;
            _saveBtn = saveBtn;
            Controls.Add(saveBtn);

            Button cancelBtn = new ModernButton();
            cancelBtn.Text = "取消";
            cancelBtn.AccessibleName = "取消主题设置";
            cancelBtn.Font = F(9f, false);
            UiStyle.StyleButton(cancelBtn, false);
            cancelBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _cancelBtn = cancelBtn;
            Controls.Add(cancelBtn);

            CancelButton = cancelBtn;
            AcceptButton = saveBtn;

            _accentCombo.TabIndex = 0;
            _darkCheck.TabIndex = 1;
            saveBtn.TabIndex = 2;
            cancelBtn.TabIndex = 3;
            header.TabIndex = 4;
            header.TabStop = false;
            closeBtn.TabIndex = 0; // first (only) tab stop inside the header
            _accentCombo.TabStop = true;
            ActiveControl = _accentCombo;

            LayoutControls();
            int cx = _workArea.Left + Math.Max(0, (_workArea.Width - ClientSize.Width) / 2);
            int cy = _workArea.Top + Math.Max(0, (_workArea.Height - ClientSize.Height) / 2);
            Location = new Point(cx, cy);
        }

        // Header drag: moves the whole border-less dialog by the same delta as
        // the pointer.
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

        private void LayoutControls()
        {
            int cw = ClientSize.Width;
            int ch = ClientSize.Height;
            _headerPanel.SetBounds(1, 0, cw - 2, S(46));
            _closeBtn.SetBounds(cw - S(6) - S(34), S(6), S(34), S(34));
            _heading.SetBounds(S(20), 1,
                Math.Max(S(40), cw - S(20) - _closeBtn.Width - S(26)), S(46) - 2);
            int contentW = Math.Max(S(80), cw - S(40));
            _accentLabel.SetBounds(S(20), S(60), S(64), S(24));
            int accentW = Math.Min(S(180), Math.Max(S(60), contentW - S(70)));
            _accentCombo.SetBounds(S(20) + S(64), S(58), Math.Max(S(60), accentW), S(28));
            _darkCheck.SetBounds(S(20), S(96), contentW, S(24));
            _error.SetBounds(S(20), S(126), contentW, S(36));
            SizeErrorForReadability();
            int btnH = S(34);
            int bottomPad = S(8);
            int btnW = Math.Min(S(84), Math.Max(S(20), (cw - S(26)) / 2));
            _cancelBtn.SetBounds(cw - S(20) - btnW, ch - bottomPad - btnH, btnW, btnH);
            _saveBtn.SetBounds(Math.Max(S(8), _cancelBtn.Left - S(6) - btnW),
                ch - bottomPad - btnH, btnW, btnH);
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
            // Persist the theme selection. Failure keeps the dialog open,
            // reverts the live preview and reports it inline so the user never
            // leaves believing the theme was remembered.
            if (_themeSave != null && !_themeSave(_themeAccent, _themeDark))
            {
                RevertThemePreview();
                SetError("主题设置保存失败，未能记住本次选择。");
                return;
            }
            _themeCommitted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        // A live preview of the chosen accent / mode; persistence only happens
        // on 保存.
        private void OnThemeChanged()
        {
            _themeAccent = _accentCombo == null ? _themeAccent
                : ThemeCatalog.Normalize(_accentCombo.SelectedIndex);
            _themeDark = _darkCheck != null && _darkCheck.Checked;
            if (_themePreview != null)
            {
                try { _themePreview(_themeAccent, _themeDark); } catch (Exception) { }
            }
            RefreshTheme();
        }

        private void RefreshTheme()
        {
            BackColor = UiStyle.Surface;
            _headerPanel.BackColor = UiStyle.Surface;
            _heading.ForeColor = UiStyle.Navy;
            _accentLabel.ForeColor = UiStyle.Navy;
            _accentCombo.BackColor = UiStyle.Surface;
            _accentCombo.ForeColor = UiStyle.Navy;
            _darkCheck.BackColor = UiStyle.Surface;
            _darkCheck.ForeColor = UiStyle.Navy;
            _error.ForeColor = UiStyle.Error;
            _closeBtn.BackColor = UiStyle.Surface;
            _closeBtn.ForeColor = UiStyle.Navy;
            _closeBtn.FlatAppearance.MouseOverBackColor = UiStyle.TealLight;
            _closeBtn.FlatAppearance.MouseDownBackColor = UiStyle.Selected;
            UiStyle.StyleButton(_saveBtn, true);
            UiStyle.StyleButton(_cancelBtn, false);
            Invalidate(true);
        }

        private void RevertThemePreview()
        {
            if (_themeCommitted) return;
            if (_themePreview == null) return;
            try { _themePreview(_initialAccent, _initialDark); } catch (Exception) { }
            RefreshTheme();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // A cancel / window-close that is not a committed save must undo the
            // live preview so no unconfirmed theme leaks into the UI.
            if (DialogResult != DialogResult.OK) RevertThemePreview();
            base.OnFormClosing(e);
        }

        // ---- test hooks ----

        internal ComboBox AccentComboForTest { get { return _accentCombo; } }
        internal CheckBox DarkCheckForTest { get { return _darkCheck; } }
        internal System.Windows.Forms.Control ThemeRowForTest { get { return _darkCheck; } }
        internal int ThemeAccentForTest { get { return _themeAccent; } }
        internal bool ThemeDarkForTest { get { return _themeDark; } }
        internal string ErrorForTest { get { return _error.Text; } }
        internal bool SaveForTest()
        {
            OnSaveClick(null, EventArgs.Empty);
            return DialogResult == DialogResult.OK;
        }
        internal void SelectAccentForTest(int i)
        {
            if (_accentCombo != null && i >= 0 && i < _accentCombo.Items.Count)
                _accentCombo.SelectedIndex = i;
        }
        internal void SetDarkForTest(bool on)
        {
            if (_darkCheck != null) _darkCheck.Checked = on;
        }
        internal double ScaleForTest { get { return _scale; } }
        internal Rectangle WorkAreaForTest { get { return _workArea; } }
        internal System.Windows.Forms.Control HeadingForTest { get { return _heading; } }
        internal System.Windows.Forms.Control HeaderForTest { get { return _headerPanel; } }
        internal System.Windows.Forms.Control SaveButtonForTest { get { return _saveBtn; } }
        internal System.Windows.Forms.Control CancelButtonForTest { get { return _cancelBtn; } }
        internal System.Windows.Forms.Control CloseButtonForTest { get { return _closeBtn; } }
        internal System.Windows.Forms.Control ErrorLabelForTest { get { return _error; } }
        internal bool SaveEnabledForTest { get { return _saveBtn != null && _saveBtn.Enabled; } }
    }
}
