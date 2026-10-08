using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed class ManualLoginForm : Form
    {
        private readonly ManualLoginRequest _request;
        private readonly Action<string> _open;
        private readonly Label _message;
        internal readonly TextBox CallbackInput;
        internal readonly Button SubmitButton;
        internal string AuthorizationCode { get; private set; }

        internal ManualLoginForm(ManualLoginRequest request, Action<string> open)
        {
            _request = request; _open = open;
            Text = "ark_left 登录方舟";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = true;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 385);
            Font = SystemFonts.MessageBoxFont;

            Label steps = new Label { Bounds = new Rectangle(20, 18, 520, 64),
                Text = "1. 打开登录链接，在浏览器中完成登录和授权。\r\n2. 授权后复制浏览器地址栏中的完整回调地址。\r\n3. 将地址粘贴到下方，点击“提交回调”。" };
            Controls.Add(steps);
            TextBox loginUrl = new TextBox { ReadOnly = true, Text = request.LoginUrl,
                Bounds = new Rectangle(20, 88, 520, 28) };
            Controls.Add(loginUrl);
            Button copy = new Button { Text = "复制登录链接", Bounds = new Rectangle(20, 122, 130, 30) };
            copy.Click += delegate {
                try { Clipboard.SetText(_request.LoginUrl); _message.Text = "登录链接已复制。"; }
                catch (Exception) { _message.Text = "复制失败，可选中上方登录链接后按 Ctrl+C。"; }
            };
            Controls.Add(copy);
            Button openButton = new Button { Text = "打开浏览器", Bounds = new Rectangle(162, 122, 120, 30) };
            openButton.Click += delegate { OpenBrowser(); };
            Controls.Add(openButton);
            Controls.Add(new Label { Text = "回调页面可能提示无法访问；请直接复制地址栏中的完整地址。",
                Bounds = new Rectangle(20, 164, 520, 35) });
            CallbackInput = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical,
                MaxLength = 8192, Bounds = new Rectangle(20, 202, 520, 65) };
            Controls.Add(CallbackInput);
            _message = new Label { Bounds = new Rectangle(20, 277, 520, 45), ForeColor = Color.FromArgb(170, 55, 40) };
            Controls.Add(_message);
            Button paste = new Button { Text = "粘贴回调地址", Bounds = new Rectangle(20, 335, 130, 30) };
            paste.Click += delegate {
                try { CallbackInput.Text = Clipboard.GetText(); CallbackInput.Focus(); }
                catch (Exception) { _message.Text = "读取剪贴板失败，请在输入框中按 Ctrl+V。"; }
            };
            Controls.Add(paste);
            SubmitButton = new Button { Text = "提交回调", Bounds = new Rectangle(306, 335, 110, 30) };
            SubmitButton.Click += delegate {
                string code, message;
                if (!_request.TryAccept(CallbackInput.Text, out code, out message))
                { _message.Text = message; CallbackInput.Focus(); return; }
                AuthorizationCode = code;
                CallbackInput.Clear();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(SubmitButton);
            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(430, 335, 110, 30) };
            Controls.Add(cancel); CancelButton = cancel; AcceptButton = SubmitButton;
            Shown += delegate { Activate(); CallbackInput.Focus(); };
            FormClosed += delegate { CallbackInput.Clear(); };
        }

        private void OpenBrowser()
        {
            try { _open(_request.LoginUrl); }
            catch (Exception) { _message.Text = "无法打开浏览器，请复制登录链接并手动打开。"; }
        }

        internal static Task<string> ShowAsync(ManualLoginRequest request, CancellationToken token)
        {
            return ShowAsync(request, token, delegate(string url) {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }, null);
        }

        internal static Task<string> ShowAsync(ManualLoginRequest request, CancellationToken token,
            Action<string> open, Action<ManualLoginForm> shown)
        {
            TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
            Thread thread = new Thread(delegate() {
                try
                {
                    token.ThrowIfCancellationRequested();
                    using (ManualLoginForm dialog = new ManualLoginForm(request, open))
                    {
                        IntPtr handle = dialog.Handle;
                        using (token.Register(delegate {
                            try { dialog.BeginInvoke(new Action(delegate { dialog.Close(); })); }
                            catch (InvalidOperationException) { }
                        }))
                        {
                            if (shown != null) dialog.Shown += delegate { shown(dialog); };
                            if (!token.IsCancellationRequested) dialog.OpenBrowser();
                            if (!token.IsCancellationRequested) dialog.ShowDialog();
                            if (token.IsCancellationRequested || dialog.AuthorizationCode == null) completion.TrySetCanceled();
                            else completion.TrySetResult(dialog.AuthorizationCode);
                            GC.KeepAlive(handle);
                        }
                    }
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception) { completion.TrySetException(new DirectFailure(QuotaStatus.LoginFailed, "无法打开登录窗口，请重试。")); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
