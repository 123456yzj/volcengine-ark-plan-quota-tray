using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    internal sealed class DirectResponse
    {
        public int Status;
        public string Body;
    }

    internal interface IDirectTransport
    {
        Task<DirectResponse> PostAsync(string url, string contentType, byte[] body,
            IDictionary<string, string> headers, CancellationToken token);
    }

    internal sealed class DirectFailure : Exception
    {
        public readonly QuotaStatus Status;
        public DirectFailure(QuotaStatus status, string safeMessage) : base(safeMessage) { Status = status; }
    }

    internal sealed class DirectTransport : IDirectTransport
    {
        public async Task<DirectResponse> PostAsync(string url, string contentType, byte[] body,
            IDictionary<string, string> headers, CancellationToken token)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST"; request.ContentType = contentType;
            request.ContentLength = body.Length; request.AllowAutoRedirect = false;
            request.Timeout = 30000; request.ReadWriteTimeout = 30000;
            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    if (header.Key == "Host") request.Host = header.Value;
                    else request.Headers[header.Key] = header.Value;
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(30000);
                using (deadline.Token.Register(request.Abort))
                {
                    try
                    {
                        using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                            await stream.WriteAsync(body, 0, body.Length, deadline.Token).ConfigureAwait(false);
                        HttpWebResponse response;
                        try { response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false); }
                        catch (WebException error)
                        {
                            response = error.Response as HttpWebResponse;
                            if (response == null) throw;
                        }
                        using (response)
                        using (Stream stream = response.GetResponseStream())
                        using (MemoryStream buffer = new MemoryStream())
                        {
                            byte[] chunk = new byte[4096]; int count;
                            while ((count = await stream.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false)) != 0)
                            {
                                if (buffer.Length + count > 1024 * 1024)
                                    throw new DirectFailure(QuotaStatus.FormatError, "接口响应过大，已停止解析。");
                                buffer.Write(chunk, 0, count);
                            }
                            return new DirectResponse { Status = (int)response.StatusCode,
                                Body = Encoding.UTF8.GetString(buffer.ToArray()) };
                        }
                    }
                    catch (DirectFailure) { throw; }
                    catch (Exception)
                    {
                        token.ThrowIfCancellationRequested();
                        throw new DirectFailure(QuotaStatus.NetworkError,
                            deadline.IsCancellationRequested ? "网络请求超时，请稍后重试。" : "网络连接失败，请检查网络后重试。");
                    }
                }
            }
        }
    }

    internal static class DirectJson
    {
        public static Dictionary<string, object> Object(string json)
        {
            try
            {
                Dictionary<string, object> result = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (result != null) return result;
            }
            catch (Exception) { }
            throw new DirectFailure(QuotaStatus.FormatError, "接口返回格式不正确。");
        }
        public static string Text(Dictionary<string, object> data, string key)
        {
            object raw; return data != null && data.TryGetValue(key, out raw) ? raw as string : null;
        }
        public static Dictionary<string, object> Child(Dictionary<string, object> data, string key)
        {
            object raw; return data != null && data.TryGetValue(key, out raw) ? raw as Dictionary<string, object> : null;
        }
        public static byte[] Form(IDictionary<string, string> values)
        {
            List<string> pairs = new List<string>();
            foreach (KeyValuePair<string, string> pair in values)
                pairs.Add(Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value));
            return Encoding.UTF8.GetBytes(string.Join("&", pairs));
        }
    }
}
