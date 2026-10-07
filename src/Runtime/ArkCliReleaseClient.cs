using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    public sealed class ArkCliRelease
    {
        public string Version, DownloadUrl, Digest;
        public long Size;
    }
    public interface IArkCliReleaseSource
    {
        Task<ArkCliRelease> LatestAsync(string architecture, CancellationToken token);
    }
    public sealed class ArkCliReleaseClient : IArkCliReleaseSource
    {
        public const string LatestUrl = "https://api.github.com/repos/volcengine/ark-cli/releases/latest";
        public async Task<ArkCliRelease> LatestAsync(string architecture, CancellationToken token)
        {
            byte[] json = await ArkCliDownloader.FetchAsync(LatestUrl, "application/vnd.github+json", 2 * 1024 * 1024, 20000, token).ConfigureAwait(false);
            return Parse(System.Text.Encoding.UTF8.GetString(json), architecture);
        }
        public static ArkCliRelease Parse(string json, string architecture)
        {
            if (architecture != "amd64" && architecture != "arm64")
                throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("draft") || !root.ContainsKey("prerelease")
                || !(root["draft"] is bool) || !(root["prerelease"] is bool)
                || (bool)root["draft"] || (bool)root["prerelease"])
                throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
            ArkCliVersion version = ArkCliVersion.Parse(Str(root, "tag_name"));
            if (version == null) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
            object assetsRaw;
            object[] assets = root.TryGetValue("assets", out assetsRaw) ? assetsRaw as object[] : null;
            if (assets != null) foreach (object raw in assets)
            {
                Dictionary<string, object> asset = raw as Dictionary<string, object>;
                if (asset == null || Str(asset, "name") != "arkcli-" + version + "-windows-" + architecture + ".exe") continue;
                string browser = Str(asset, "browser_download_url");
                string expected = "https://github.com/volcengine/ark-cli/releases/download/v" + version
                    + "/arkcli-" + version + "-windows-" + architecture + ".exe";
                string api = Str(asset, "url");
                long size;
                object sizeRaw;
                if (browser != expected || api == null
                    || !System.Text.RegularExpressions.Regex.IsMatch(api, @"^https://api\.github\.com/repos/volcengine/ark-cli/releases/assets/[0-9]+$")
                    || !asset.TryGetValue("size", out sizeRaw) || !long.TryParse(Convert.ToString(sizeRaw, System.Globalization.CultureInfo.InvariantCulture), out size)
                    || size <= 0 || size > ArkCliDownloader.MaxBinaryBytes)
                    throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
                string digest = Str(asset, "digest");
                if (asset.ContainsKey("digest") && asset["digest"] != null
                    && (digest == null || !System.Text.RegularExpressions.Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")))
                    throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIntegrityFailed);
                return new ArkCliRelease { Version = version.ToString(), DownloadUrl = api, Digest = digest, Size = size };
            }
            throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeUpdateUnavailable);
        }
        private static string Str(Dictionary<string, object> obj, string key)
        { object raw; return obj.TryGetValue(key, out raw) ? raw as string : null; }
    }
}
