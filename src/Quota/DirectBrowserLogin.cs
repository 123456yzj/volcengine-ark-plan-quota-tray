using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    internal sealed class DirectAuthorization
    {
        public string Code;
        public string Verifier;
        public string RedirectUri;
    }
    internal interface IDirectBrowserLogin
    {
        Task<DirectAuthorization> AuthorizeAsync(CancellationToken token);
    }

    internal sealed class ManualLoginRequest
    {
        internal readonly string LoginUrl;
        internal readonly string RedirectUri;
        private readonly string _state;
        internal ManualLoginRequest(string loginUrl, string redirectUri, string state)
        {
            LoginUrl = loginUrl; RedirectUri = redirectUri; _state = state;
        }

        internal bool TryAccept(string text, out string code, out string message)
        {
            code = null;
            message = "请粘贴本次浏览器登录后的完整回调地址。";
            if (string.IsNullOrWhiteSpace(text) || text.Length > 8192) return false;
            Uri uri;
            Uri expected = new Uri(RedirectUri);
            if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out uri) || uri.Scheme != expected.Scheme
                || uri.Host != expected.Host || uri.Port != expected.Port || uri.AbsolutePath != expected.AbsolutePath
                || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            {
                message = "回调地址不属于本次登录，请从浏览器地址栏重新复制。";
                return false;
            }
            Dictionary<string, string> query = DirectBrowserLogin.ParseQuery(uri.Query);
            string state, value;
            if (!query.TryGetValue("state", out state) || state != _state)
            {
                message = "回调校验失败，请使用本次登录链接完成授权后重新复制地址。";
                return false;
            }
            if (query.ContainsKey("error"))
            {
                message = "浏览器授权未完成，请重新打开登录页面完成授权。";
                return false;
            }
            if (!query.TryGetValue("code", out value) || string.IsNullOrWhiteSpace(value) || value.Length > 4096)
                return false;
            code = value; message = null; return true;
        }
    }

    internal sealed class DirectBrowserLogin : IDirectBrowserLogin
    {
        internal const string ClientId = "trn:signin:::devtools/same-device";
        internal const string Scope = "Console:All:All";
        internal const string TokenUrl = "https://signin.volcengine.com/authorize/oauth/token";
        private readonly Func<ManualLoginRequest, CancellationToken, Task<string>> _input;
        internal DirectBrowserLogin(Func<ManualLoginRequest, CancellationToken, Task<string>> input) { _input = input; }
        public DirectBrowserLogin() : this(ManualLoginForm.ShowAsync) { }

        internal static string RandomValue(int size)
        {
            byte[] value = new byte[size];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(value);
            return Base64Url(value);
        }
        internal static string Base64Url(byte[] value) { return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        internal static string Challenge(string verifier)
        {
            using (SHA256 sha = SHA256.Create()) return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }
        internal static string AuthorizeUrl(string redirect, string state, string verifier)
        {
            byte[] query = DirectJson.Form(new Dictionary<string, string> {
                { "response_type", "code" }, { "client_id", ClientId }, { "redirect_uri", redirect },
                { "scope", Scope }, { "state", state }, { "code_challenge", Challenge(verifier) },
                { "code_challenge_method", "S256" } });
            return "https://signin.volcengine.com/authorize/oauth/authorize?" + Encoding.UTF8.GetString(query);
        }

        public async Task<DirectAuthorization> AuthorizeAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string verifier = RandomValue(32), state = RandomValue(24);
            byte[] randomPort = new byte[2];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(randomPort);
            int port = 49152 + BitConverter.ToUInt16(randomPort, 0) % 16384;
            string redirect = "http://127.0.0.1:" + port + "/oauth/callback";
            ManualLoginRequest request = new ManualLoginRequest(AuthorizeUrl(redirect, state, verifier), redirect, state);
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(600000);
                try
                {
                    string code = await _input(request, deadline.Token).ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(code)) throw new OperationCanceledException();
                    return new DirectAuthorization { Code = code, Verifier = verifier, RedirectUri = redirect };
                }
                catch (OperationCanceledException)
                {
                    if (deadline.IsCancellationRequested && !token.IsCancellationRequested)
                        throw new DirectFailure(QuotaStatus.LoginFailed, "等待手动提交回调超时，请重新登录。");
                    throw;
                }
            }
        }

        internal static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string pair in query.TrimStart('?').Split('&'))
            {
                int equals = pair.IndexOf('='); if (equals < 0) continue;
                string key = Uri.UnescapeDataString(pair.Substring(0, equals).Replace('+', ' '));
                if (result.ContainsKey(key)) return new Dictionary<string, string>();
                result[key] = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '));
            }
            return result;
        }
    }
}
