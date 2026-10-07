using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm
    {
        // ---- public layout test hooks (used by the offline layout smoke test) ----

        public void BeginLayoutSession(Screen screen)
        {
            _openScreen = screen;
            _openScale = DpiUtil.GetScale(screen);
            _scale = _openScale;
            SizeToFit(false);
            _hide.State(true, Environment.TickCount);
            if (!_clock.Enabled) _clock.Start();
        }

        // Synthetic identity used only by the offline smoke/test hooks. No real
        // account identifier is ever embedded (all-zero account, sample user).
        internal static AuthIdentity SampleIdentity()
        {
            AuthIdentity id = new AuthIdentity();
            id.Present = true;
            id.Name = "sample";
            id.Type = "volc-sso";
            id.OwnerTrn = "trn:iam::000000000000:user/sample";
            id.Region = "cn-beijing";
            id.Project = "default";
            return id;
        }

        public void ForceLoading() { ApplyModelView(_model.BeginQuery()); }
        public void ForceRender(QuotaSnapshot snap)
        {
            _model.OnAuthResult(true, SampleIdentity(), QuotaStatus.Ok, null);
            ApplyModelView(_model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
        }

        public void ForceError(QuotaStatus status, string message)
        {
            ApplyModelView(_model.OnUsageFailure(status, message, null));
        }

        public void ForceIdentityChanged()
        {
            ApplyModelView(_model.OnUsageResult(null, null, ScopeVerdict.Mismatch, null));
        }

        public int CurrentHeight { get { return Height; } }
        public int CurrentWidth { get { return Width; } }
        public int ContentCardCount { get { return _content.Controls.Count; } }
        public Rectangle CurrentBounds { get { return Bounds; } }
        public Screen OpenScreenRef { get { return ActiveScreen(); } }
        public PanelState CurrentState { get { return _view != null ? _view.State : PanelState.Loading; } }

        // Test hook: whether the model still holds a reusable snapshot.
        public bool ModelHasLast { get { return _model.Last != null; } }
        // ---- UX019 / UX022 test hooks (offline; never the real clipboard) ----

        // UX022: the real details-local context menu and its four actions.
        internal ContextMenuStrip DetailsMenuForTest { get { return _detailsMenu; } }
        internal ToolStripMenuItem RefreshMenuItemForTest { get { return _miRefresh; } }
        internal ToolStripMenuItem CopyMenuItemForTest { get { return _miCopy; } }
        internal ToolStripMenuItem SettingsMenuItemForTest { get { return _miSettings; } }
        internal ToolStripMenuItem CloseMenuItemForTest { get { return _miClose; } }
        internal bool MenuOpenForTest { get { return _menuOpen; } }
        internal System.Windows.Forms.ToolTip ToolTipForTest { get { return _tip; } }
        internal System.Windows.Forms.Timer CopyFeedbackTimerForTest { get { return _copyFeedback; } }
        internal string CopyFeedbackTextForTest { get { return _copyFeedbackText; } }

        // Offline tests inject a recorder instead of Clipboard.SetText.
        internal Action<string> ClipboardSetForTest
        {
            get { return _clipboardSet; }
            set { _clipboardSet = value; }
        }

        // Layout matrix injection: a scale change must REBUILD the cards
        // (fonts / paddings / radius / height are scale-derived) and re-fit
        // the window; the per-card layout cache is invalidated so a
        // same-width card still reflows.
        internal void SetScaleForTest(double scale)
        {
            _scale = scale;
            InvalidateCardLayouts();
            RelayoutContent();
        }

        internal FlowLayoutPanel ContentForTest { get { return _content; } }
        internal bool DialogOpenForTest { get { return _dialogOpen; } }
        internal System.Windows.Forms.Control ActiveControlForTest { get { return ActiveControl; } }
        // UX022: consumed by the owner — re-anchors the visible window against
        // the CURRENT circle after a visible size change.
        internal bool ConsumePendingReposition()
        {
            bool pending = _pendingReposition;
            _pendingReposition = false;
            return pending;
        }
        // UX022: the footer update time moved onto the cards' tooltip.
        internal string UpdateTimeTextForTest
        {
            get
            {
                return _content.Controls.Count > 0
                    ? _tip.GetToolTip(_content.Controls[0]) : "";
            }
        }

        public System.Collections.Generic.List<Control> ContentControls
        {
            get
            {
                System.Collections.Generic.List<Control> list =
                    new System.Collections.Generic.List<Control>();
                foreach (Control c in _content.Controls) list.Add(c);
                return list;
            }
        }
    }
}
