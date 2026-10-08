using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    internal enum AppUpdateStatus { Current, Available, Failed, Cancelled }

    internal sealed class AppUpdateResult
    {
        internal AppUpdateStatus Status;
        internal Version Version;
        internal string ReleaseUrl;
    }

    internal sealed class AppUpdateClient
    {
        internal const string RepositoryUrl = "https://github.com/123456yzj/volcengine-ark-plan-quota-tray";
        internal const string LatestUrl = "https://api.github.com/repos/123456yzj/volcengine-ark-plan-quota-tray/releases/latest";
        private readonly Version _current;
        private readonly Func<CancellationToken, Task<string>> _fetch;

        internal AppUpdateClient(Version current) : this(current, FetchAsync) { }
        internal AppUpdateClient(Version current, Func<CancellationToken, Task<string>> fetch)
        {
            _current = Normalize(current); _fetch = fetch;
        }

        private static async Task<string> FetchAsync(CancellationToken token)
        {
            byte[] bytes = await ArkCliDownloader.FetchAsync(LatestUrl,
                "application/vnd.github+json", 1024 * 1024, 20000, token).ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }

        internal async Task<AppUpdateResult> CheckAsync(CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                string json = await _fetch(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return Parse(json, _current);
            }
            catch (Exception)
            {
                return new AppUpdateResult { Status = token.IsCancellationRequested
                    ? AppUpdateStatus.Cancelled : AppUpdateStatus.Failed };
            }
        }

        internal static string DisplayVersion(Version version)
        {
            Version normalized = Normalize(version);
            return normalized.ToString(normalized.Revision == 0 ? 3 : 4);
        }

        private static Version Normalize(Version version)
        {
            return new Version(version.Major, version.Minor,
                Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        internal static AppUpdateResult Parse(string json, Version current)
        {
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json)
                as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("draft") || !root.ContainsKey("prerelease")
                || !(root["draft"] is bool) || !(root["prerelease"] is bool)
                || (bool)root["draft"] || (bool)root["prerelease"])
                throw new FormatException("release");
            string tag = Text(root, "tag_name");
            if (tag == null || !Regex.IsMatch(tag, @"^v?[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$"))
                throw new FormatException("version");
            Version version;
            if (!Version.TryParse(tag.TrimStart('v'), out version)) throw new FormatException("version");
            version = Normalize(version);
            string releaseUrl = RepositoryUrl + "/releases/tag/" + tag;
            if (Text(root, "html_url") != releaseUrl) throw new FormatException("release-url");
            object rawAssets;
            object[] assets = root.TryGetValue("assets", out rawAssets) ? rawAssets as object[] : null;
            string installer = "ark_left-" + DisplayVersion(version) + "-windows-setup.exe";
            string expectedDownload = RepositoryUrl + "/releases/download/" + tag + "/" + installer;
            bool ready = false;
            if (assets != null) foreach (object raw in assets)
            {
                Dictionary<string, object> asset = raw as Dictionary<string, object>;
                if (asset != null && Text(asset, "name") == installer
                    && Text(asset, "state") == "uploaded"
                    && Text(asset, "browser_download_url") == expectedDownload) ready = true;
            }
            if (!ready) throw new FormatException("installer");
            return new AppUpdateResult { Status = version > Normalize(current)
                ? AppUpdateStatus.Available : AppUpdateStatus.Current,
                Version = version, ReleaseUrl = releaseUrl };
        }

        private static string Text(Dictionary<string, object> root, string key)
        {
            object raw;
            return root.TryGetValue(key, out raw) ? raw as string : null;
        }
    }
}
