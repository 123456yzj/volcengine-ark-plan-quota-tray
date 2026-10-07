using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    public interface IArkCliDownloader
    {
        Task DownloadAsync(ArkCliRelease release, string part, CancellationToken token);
    }
    public sealed class ArkCliDownloader : IArkCliDownloader
    {
        public const long MaxBinaryBytes = 256 * 1024 * 1024;
        public const int DownloadTimeoutMs = 300000;
        public async Task DownloadAsync(ArkCliRelease release, string part, CancellationToken token)
        {
            if (!part.EndsWith(".part", StringComparison.Ordinal)) throw new ArgumentException("part");
            try
            {
                using (FileStream file = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    long size = await TransferAsync(release.DownloadUrl, "application/octet-stream", file, MaxBinaryBytes, DownloadTimeoutMs, token).ConfigureAwait(false);
                    if (size != release.Size) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeDownloadFailed);
                    file.Flush(true);
                }
            }
            catch (Exception)
            {
                ArkCliRuntimeStore.DeleteQuiet(part);
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeDownloadFailed);
            }
        }
        public static async Task<byte[]> FetchAsync(string url, string accept, long limit, int timeoutMs, CancellationToken token)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                await TransferAsync(url, accept, memory, limit, timeoutMs, token).ConfigureAwait(false);
                return memory.ToArray();
            }
        }
        internal static bool AllowedUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;
            return uri.Host == "api.github.com" || uri.Host == "github.com"
                || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com";
        }
        private static async Task<long> TransferAsync(string url, string accept, Stream output, long limit, int timeoutMs, CancellationToken token)
        {
            // .NET Framework 4.x defaults may otherwise attempt obsolete TLS.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(timeoutMs);
                for (int redirects = 0; redirects <= 5; redirects++)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    if (!AllowedUrl(url)) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.AllowAutoRedirect = false;
                    request.UserAgent = "ArkLeft-Managed-Runtime";
                    request.Accept = accept;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (deadline.Token.Register(request.Abort))
                    using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        int status = (int)response.StatusCode;
                        if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                        {
                            string location = response.Headers["Location"];
                            if (location == null) throw new IOException("redirect");
                            url = new Uri(new Uri(url), location).AbsoluteUri;
                            continue;
                        }
                        if (status != 200 || response.ContentLength > limit) throw new IOException("response");
                        using (Stream input = response.GetResponseStream())
                        {
                            byte[] buffer = new byte[81920];
                            long total = 0;
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) != 0)
                            {
                                total += count;
                                if (total > limit) throw new IOException("size");
                                await output.WriteAsync(buffer, 0, count, deadline.Token).ConfigureAwait(false);
                            }
                            if (response.ContentLength >= 0 && total != response.ContentLength) throw new IOException("partial");
                            deadline.Token.ThrowIfCancellationRequested();
                            return total;
                        }
                    }
                }
                throw new IOException("redirect-limit");
            }
        }
    }
}
