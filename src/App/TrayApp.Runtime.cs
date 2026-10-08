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

        private Task Login()
        {
            if (_disposed || _loggingOut || _loginAction == null) return Task.FromResult(0);
            if (_login != null) { _login.Cancel(); return _controller.PauseAsync(); }
            _loginTask = LoginCore();
            return _loginTask;
        }

        private async Task LoginCore()
        {
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
                if (_disposed || _loggingOut) return;
                if (result.Started && !result.Cancelled && !result.TimedOut && result.Failure == null && result.ExitCode == 0)
                {
                    _controller.Resume();
                    await _controller.Refresh(); // re-confirm auth and scope before committing usage
                }
                else
                {
                    string message = result.Cancelled ? "登录已取消，可重新登录。" : result.TimedOut
                        ? "登录超时，请重新登录。" : "登录未完成，请重新登录。";
                    QuotaStatus status;
                    if (!Enum.TryParse<QuotaStatus>(result.Failure, out status)) status = QuotaStatus.LoginFailed;
                    if (!result.Cancelled && !result.TimedOut) message = DirectArkUsage.Failure(status).Message;
                    PanelView failed = _form.Model.OnUsageFailure(status, message, null);
                    failed.AllowCopyLogin = true;
                    _form.ApplyModelView(failed); _floating.ApplyModelView(failed);
                }
            }
            catch (Exception)
            {
                if (!_disposed && !_loggingOut)
                {
                    PanelView failed = _form.Model.OnUsageFailure(QuotaStatus.Failed, "登录未完成，请重新登录。", null);
                    failed.AllowCopyLogin = true;
                    _form.ApplyModelView(failed); _floating.ApplyModelView(failed);
                }
            }
            finally
            {
                if (!_loggingOut) _controller.Resume();
                _login = null; login.Dispose();
                if (!_disposed && _menuLogin != null) _menuLogin.Text = "重新登录";
            }
        }

        private async Task Logout()
        {
            if (_disposed || _loggingOut || _logoutAction == null) return;
            _loggingOut = true;
            _menuLogout.Enabled = false; _menuLogin.Enabled = false;
            try
            {
                if (_login != null) _login.Cancel();
                await _controller.PauseAsync();
                Task login = _loginTask;
                if (login != null) await login;
                if (_disposed) return;
                PanelView cleared = _form.Model.BeginIdentityChange();
                _form.ApplyModelView(cleared); _floating.ApplyModelView(cleared);
                CliResult result = await _logoutAction(_lifetime.Token);
                if (_disposed) return;
                bool success = result.Started && !result.Cancelled && !result.TimedOut && result.Failure == null && result.ExitCode == 0;
                PanelView view = _form.Model.OnUsageFailure(success ? QuotaStatus.NotLoggedIn : QuotaStatus.CredentialStorageFailed,
                    success ? "已登出，请通过设置重新登录。" : "登出未完成，无法清除本地登录凭据，请重试。", null);
                _form.ApplyModelView(view); _floating.ApplyModelView(view);
            }
            catch (Exception)
            {
                if (!_disposed)
                {
                    PanelView failed = _form.Model.OnUsageFailure(QuotaStatus.CredentialStorageFailed,
                        "登出未完成，请重试。", null);
                    _form.ApplyModelView(failed); _floating.ApplyModelView(failed);
                }
            }
            finally
            {
                _loggingOut = false;
                if (!_disposed)
                {
                    _menuLogout.Enabled = true; _menuLogin.Enabled = true;
                    _controller.Resume();
                }
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
