using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    internal static class DirectArkUsage
    {
        internal const string Host = "ark.cn-beijing.volcengineapi.com";
        internal const string Query = "Action=GetAFPUsage&Version=2024-01-01";
        internal const string Url = "https://" + Host + "/?" + Query;
        internal static readonly byte[] Body = Encoding.UTF8.GetBytes("{}");

        internal static Dictionary<string, string> Sign(DirectSts sts, DateTime utc)
        {
            string date = utc.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            string hash = Hash(Body);
            string names = "content-type;host;x-content-sha256;x-date;x-security-token";
            string canonicalHeaders = "content-type:application/json\nhost:" + Host
                + "\nx-content-sha256:" + hash + "\nx-date:" + date
                + "\nx-security-token:" + sts.SessionToken + "\n";
            string canonical = "POST\n/\n" + Query + "\n" + canonicalHeaders + "\n" + names + "\n" + hash;
            string scope = date.Substring(0, 8) + "/cn-beijing/ark/request";
            string message = "HMAC-SHA256\n" + date + "\n" + scope + "\n" + Hash(Encoding.UTF8.GetBytes(canonical));
            byte[] key = Encoding.UTF8.GetBytes(sts.SecretKey);
            foreach (string part in new[] { date.Substring(0, 8), "cn-beijing", "ark", "request" })
            {
                byte[] next = Hmac(key, part); Array.Clear(key, 0, key.Length); key = next;
            }
            string signature;
            try { signature = Hex(Hmac(key, message)); }
            finally { Array.Clear(key, 0, key.Length); }
            return new Dictionary<string, string> {
                { "Host", Host }, { "X-Content-Sha256", hash }, { "X-Date", date },
                { "X-Security-Token", sts.SessionToken },
                { "Authorization", "HMAC-SHA256 Credential=" + sts.AccessKey + "/" + scope
                    + ", SignedHeaders=" + names + ", Signature=" + signature } };
        }
        private static byte[] Hmac(byte[] key, string value)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key)) return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        }
        private static string Hash(byte[] value)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(value));
        }
        private static string Hex(byte[] value)
        {
            StringBuilder result = new StringBuilder();
            foreach (byte b in value) result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        internal static QuotaSnapshot Parse(DirectResponse response)
        {
            Dictionary<string, object> root;
            try { root = DirectJson.Object(response.Body); }
            catch (DirectFailure)
            {
                if (response.Status == 401 || response.Status == 403)
                    return Failure(response.Status == 401 ? QuotaStatus.Unauthorized : QuotaStatus.Forbidden);
                return Failure(QuotaStatus.FormatError);
            }
            Dictionary<string, object> metadata = DirectJson.Child(root, "ResponseMetadata");
            Dictionary<string, object> error = DirectJson.Child(metadata, "Error") ?? DirectJson.Child(root, "Error");
            string code = (DirectJson.Text(error, "Code") ?? "").ToLowerInvariant();
            if (code.Contains("signature")) return Failure(QuotaStatus.SignatureFailed);
            if (code.Contains("notsubscrib") || code.Contains("subscriptionnotfound")) return Failure(QuotaStatus.NoSubscription);
            if (response.Status == 401) return Failure(QuotaStatus.Unauthorized);
            if (response.Status == 403 || code.Contains("accessdenied") || code.Contains("forbidden")) return Failure(QuotaStatus.Forbidden);
            if (error != null || response.Status < 200 || response.Status >= 300) return Failure(QuotaStatus.Failed);
            object raw;
            if (!root.TryGetValue("Result", out raw)) return Failure(QuotaStatus.FormatError);
            if (raw == null) return Failure(QuotaStatus.NoSubscription);
            Dictionary<string, object> data = raw as Dictionary<string, object>;
            if (data == null) return Failure(QuotaStatus.FormatError);
            string tier = DirectJson.Text(data, "PlanType");
            if (string.IsNullOrWhiteSpace(tier)) return Failure(QuotaStatus.FormatError);
            List<object> periods = new List<object>();
            string[] fields = { "AFPFiveHour", "AFPDaily", "AFPWeekly", "AFPMonthly" };
            string[] labels = { "5h", "daily", "weekly", "monthly" };
            for (int i = 0; i < fields.Length; i++)
            {
                Dictionary<string, object> period = DirectJson.Child(data, fields[i]);
                Dictionary<string, object> mapped = new Dictionary<string, object> { { "label", labels[i] } };
                if (period == null) mapped["error"] = "missing-period";
                else
                {
                    if (period.TryGetValue("Used", out raw)) mapped["used"] = raw;
                    if (period.TryGetValue("Quota", out raw)) mapped["total"] = raw;
                    double ms;
                    if (period.TryGetValue("ResetTime", out raw) && Numeric(raw, out ms) && ms > 0 && ms < 253402300799000)
                        mapped["reset_at"] = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                            .AddMilliseconds(ms).ToString("o", CultureInfo.InvariantCulture);
                }
                periods.Add(mapped);
            }
            // Reuse the established remaining/clamping/unknown rules through a
            // local model adapter; this does not invoke or depend on ArkCLI.
            Dictionary<string, object> item = new Dictionary<string, object> {
                { "product", "agent-plan" }, { "edition", "personal" }, { "tier", tier },
                { "subscribed", true }, { "periods", periods } };
            return QuotaParser.Parse(new JavaScriptSerializer().Serialize(
                new Dictionary<string, object> { { "items", new[] { item } } }));
        }
        private static bool Numeric(object value, out double result)
        {
            result = 0;
            if (!(value is int) && !(value is long) && !(value is double) && !(value is decimal)) return false;
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }
        internal static QuotaSnapshot Failure(QuotaStatus status)
        {
            string message;
            switch (status)
            {
                case QuotaStatus.NotLoggedIn: message = "尚未登录方舟账号，请点击“登录方舟”。"; break;
                case QuotaStatus.LoginFailed: message = "浏览器登录失败，请重新登录。"; break;
                case QuotaStatus.TokenRefreshFailed: message = "登录凭证续期失败，重新登录未完成。"; break;
                case QuotaStatus.Unauthorized: message = "接口返回 401，临时凭证无效，请重新登录。"; break;
                case QuotaStatus.Forbidden: message = "接口返回 403 或权限被拒绝，请检查当前账号权限。"; break;
                case QuotaStatus.SignatureFailed: message = "接口签名校验失败，请检查系统时间并重试。"; break;
                case QuotaStatus.NoSubscription: message = "当前身份未订阅 Agent Plan 个人版。"; break;
                case QuotaStatus.NetworkError: message = "网络请求失败，请检查连接后重试。"; break;
                case QuotaStatus.CredentialStorageFailed: message = "无法安全保存登录凭证，请检查本地状态目录。"; break;
                case QuotaStatus.FormatError: message = "接口返回格式不正确。"; break;
                case QuotaStatus.Cancelled: message = "查询或登录已取消。"; break;
                default: message = "额度查询失败，请稍后重试。"; break;
            }
            return new QuotaSnapshot { Status = status, Message = message, FetchedAt = DateTime.Now };
        }
    }
}
