using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ArkLeft
{
    public sealed class ArkCliVersion : IComparable<ArkCliVersion>
    {
        public readonly int Major, Minor, Patch;
        private ArkCliVersion(int major, int minor, int patch) { Major = major; Minor = minor; Patch = patch; }
        public static ArkCliVersion Parse(string text)
        {
            if (text == null) return null;
            Match m = Regex.Match(text, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$");
            int major, minor, patch;
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out major)
                || !int.TryParse(m.Groups[2].Value, out minor) || !int.TryParse(m.Groups[3].Value, out patch)) return null;
            return new ArkCliVersion(major, minor, patch);
        }
        public static ArkCliVersion FromOutput(string text)
        {
            if (text == null) return null;
            // Version output is a single line, with optional name and build metadata.
            Match m = Regex.Match(text.Trim(), @"^(?:arkcli(?: version)?\s+)?v?([0-9]+\.[0-9]+\.[0-9]+)(?:\s+\([^\r\n]*\))?$", RegexOptions.IgnoreCase);
            return m.Success ? Parse(m.Groups[1].Value) : null;
        }
        public int CompareTo(ArkCliVersion other)
        {
            if (other == null) return 1;
            int c = Major.CompareTo(other.Major);
            if (c != 0) return c;
            c = Minor.CompareTo(other.Minor);
            return c != 0 ? c : Patch.CompareTo(other.Patch);
        }
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);
        }
    }

    public static class ArkCliRuntimeConfig
    {
        public const string BootstrapVersion = "1.0.37";
        public const string MinimumSupportedVersion = "1.0.37";
        public static string BootstrapDigest(string architecture)
        {
            if (architecture == "amd64") return "sha256:86d640ffccafa3ca5562536f226a7aadfbb362566741c1ea7e3a6a536fb58c05";
            if (architecture == "arm64") return "sha256:975fc57ab3ec093060a1772c273b53090b0fc45a07207eef1763a27d5838e71e";
            return null;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemInfo
        {
            public ushort Architecture, Reserved;
            public uint PageSize;
            public IntPtr MinimumAddress, MaximumAddress, ProcessorMask;
            public uint ProcessorCount, ProcessorType, AllocationGranularity;
            public ushort ProcessorLevel, ProcessorRevision;
        }
        [DllImport("kernel32.dll")]
        private static extern void GetNativeSystemInfo(out SystemInfo info);
        public static string Architecture()
        {
            try
            {
                SystemInfo info;
                GetNativeSystemInfo(out info);
                if (info.Architecture == 9) return "amd64";
                if (info.Architecture == 12) return "arm64";
            }
            catch (Exception) { }
            return null; // unsupported/unknown never guesses an asset
        }
    }

    public enum ArkCliRuntimeError
    {
        None, RuntimeMissing, RuntimeDownloadFailed, RuntimeIntegrityFailed,
        RuntimeSignatureInvalid, RuntimeStartFailed, RuntimeIncompatible, RuntimeUpdateUnavailable
    }
    public sealed class ArkCliRuntimeException : Exception
    {
        public readonly ArkCliRuntimeError Category;
        public ArkCliRuntimeException(ArkCliRuntimeError category) : base(category.ToString()) { Category = category; }
    }
    public sealed class ArkCliRuntimeInfo
    {
        public string Version, ExePath;
        public bool IsBundled, IsManaged;
    }
}
