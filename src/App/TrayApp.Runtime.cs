using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
        private async void StartRefresh()
        {
            await _controller.Refresh();
        }

        private async Task Login()
        {
            if (_disposed || _loginAction == null) return;
            if (_login != null) { _login.Cancel(); await _controller.PauseAsync(); return; }
            CancellationTokenSource login = new CancellationTokenSource();
            _login = login;
            _menuLogin.Text = "取消登录";
            try
            {
                await _controller.PauseAsync();
                if (_disposed || login.IsCancellationRequested) return;
                // Explicit identity boundary: old displayed data cannot survive a failed switch.
                PanelView switching = _form.Model.BeginIdentityChange();
                _form.ApplyModelView(switching); _floating.ApplyModelView(switching);
                CliResult result = await _loginAction(login.Token);
                if (_disposed) return;
                if (result.Started && !result.Cancelled && !result.TimedOut && result.Failure == null && result.ExitCode == 0)
                {
                    _controller.Resume();
                    await _controller.Refresh(); // re-confirm auth and scope before committing usage
                }
                else
                {
                    string message = result.Cancelled ? "登录已取消，可重新登录。" : result.TimedOut
                        ? "登录超时，请重新登录。" : "登录未完成，请重新登录。";
                    PanelView failed = _form.Model.OnUsageFailure(QuotaStatus.Failed, message, null);
                    failed.AllowCopyLogin = true;
                    _form.ApplyModelView(failed); _floating.ApplyModelView(failed);
                }
            }
            catch (Exception)
            {
                if (!_disposed)
                {
                    PanelView failed = _form.Model.OnUsageFailure(QuotaStatus.Failed, "登录未完成，请重新登录。", null);
                    failed.AllowCopyLogin = true;
                    _form.ApplyModelView(failed); _floating.ApplyModelView(failed);
                }
            }
            finally
            {
                _controller.Resume();
                _login = null; login.Dispose();
                if (!_disposed && _menuLogin != null) _menuLogin.Text = "登录方舟 / 重新登录 / 切换账号";
            }
        }

        private void ShowRuntimeDiagnostics()
        {
            if (_disposed || _cli == null) return;
            using (Form dialog = new Form())
            {
                dialog.Text = "ark_left 关于 / 诊断"; dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.ClientSize = new Size(430, 235); dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false; dialog.MinimizeBox = false;
                Label label = new Label { AutoSize = false, Bounds = new Rectangle(20, 20, 390, 130) };
                Action display = delegate {
                    CliResolveResult resolved = QuotaCli.Resolve(_cli.RuntimeManager);
                    ArkCliRuntimeState state = _cli.RuntimeManager.State;
                    label.Text = "ArkCLI Runtime：" + (resolved.Runtime == null ? "未知" : resolved.Runtime.Version)
                        + "\r\n来源：" + (resolved.Runtime != null ? "Managed" : resolved.IsUsable ? "开发覆盖 / System" : "不可用")
                        + "\r\n状态：" + _cli.RuntimeManager.LastError
                        + "\r\n上次更新检查（UTC）：" + (state.lastCheckAt ?? "尚未检查")
                        + "\r\n待激活版本：" + (state.pendingVersion ?? "无");
                };
                display(); dialog.Controls.Add(label);
                Button check = new Button { Text = "检查更新", Bounds = new Rectangle(20, 165, 120, 32) };
                check.Click += async delegate {
                    check.Enabled = false;
                    ArkCliRuntimeManager manager = _cli.RuntimeManager;
                    CancellationToken token = _lifetime.Token;
                    try { await Task.Run(() => manager.CheckForUpdateAsync(token)); }
                    finally { if (!dialog.IsDisposed && !_disposed) { display(); check.Enabled = true; } }
                };
                dialog.Controls.Add(check);
                Label ttl = new Label { Text = "每 24 小时最多联网检查一次", AutoSize = true, Location = new Point(155, 173) };
                dialog.Controls.Add(ttl);
                _form.SetDialogOpen(true);
                try { dialog.ShowDialog(_floating.CircleSurface); }
                finally { if (!_disposed) _form.SetDialogOpen(false); }
            }
        }

        // One UI commit from the authoritative final outcome, independent of Progress.
        internal static void ApplyFinalOutcome(PopupForm form, QueryOutcome outcome)
        {
            if (form == null || outcome == null) return;
            form.ApplyModelView(form.Model.CommitOutcome(outcome));
        }
    }
}
