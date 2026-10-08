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
        // ---- UX022 / UX029 test hooks ----

        internal bool MenuOpenForTest { get { return _menuOpen; } }

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
        // UX029: the details show the update time only as the card's
        // "arkUpdateTime" label text (tooltips were removed). Tests read that
        // label directly instead of a tooltip.
        internal string UpdateTimeTextForTest
        {
            get
            {
                if (_content.Controls.Count == 0) return "";
                foreach (Control child in _content.Controls[0].Controls)
                    if (child.Name == "arkUpdateTime") return child.Text;
                return "";
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
