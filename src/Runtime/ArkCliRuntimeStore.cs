using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    // Contains lifecycle metadata only, never identity or credential data.
    public sealed class ArkCliRuntimeState
    {
        public int version = 1;
        public string activeVersion, previousVersion, pendingVersion, lastCheckAt, lastSuccessfulVersion;
        public ArkCliRuntimeState Copy() { return (ArkCliRuntimeState)MemberwiseClone(); }
    }

    public sealed class ArkCliRuntimeStore
    {
        public readonly string Root;
        public string StatePath { get { return Path.Combine(Root, "runtime-state.json"); } }
        public string RuntimeDirectory { get { return Path.Combine(Root, "runtime"); } }
        public string DownloadsDirectory { get { return Path.Combine(Root, "downloads"); } }
        public ArkCliRuntimeStore(string root) { Root = Path.GetFullPath(root); }
        public static string DefaultRoot()
        {
            string diagnostic = Environment.GetEnvironmentVariable("ARK_LEFT_RUNTIME_DIR");
            return string.IsNullOrEmpty(diagnostic)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArkLeft") : diagnostic;
        }
        public string ExePath(string version)
        {
            ArkCliVersion v = ArkCliVersion.Parse(version);
            if (v == null || v.ToString() != version) return null;
            return Path.Combine(RuntimeDirectory, version, "arkcli.exe");
        }
        public bool Exists(string version)
        {
            string path = ExePath(version);
            return path != null && File.Exists(path);
        }
        private ArkCliRuntimeState Read(string path)
        {
            try
            {
                if (new FileInfo(path).Length > 16384) return null;
                string json = File.ReadAllText(path);
                var raw = new JavaScriptSerializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
                object format;
                if (raw == null || !raw.TryGetValue("version", out format) || !(format is int) || (int)format != 1) return null;
                ArkCliRuntimeState state = new JavaScriptSerializer().Deserialize<ArkCliRuntimeState>(json);
                if (state == null || state.version != 1) return null;
                if (!Exists(state.activeVersion)) state.activeVersion = null;
                if (!Exists(state.previousVersion)) state.previousVersion = null;
                if (!Exists(state.pendingVersion)) state.pendingVersion = null;
                if (ArkCliVersion.Parse(state.lastSuccessfulVersion) == null) state.lastSuccessfulVersion = null;
                DateTime checkedAt;
                if (!DateTime.TryParse(state.lastCheckAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out checkedAt)
                    || checkedAt.ToUniversalTime() > DateTime.UtcNow) state.lastCheckAt = null;
                return state;
            }
            catch (Exception) { return null; }
        }
        public ArkCliRuntimeState Load()
        {
            ArkCliRuntimeState state = Read(StatePath) ?? Read(StatePath + ".bak") ?? new ArkCliRuntimeState();
            if (state.activeVersion == null && state.previousVersion != null)
            { state.activeVersion = state.previousVersion; state.previousVersion = null; }
            return state;
        }
        public void Save(ArkCliRuntimeState state)
        {
            Directory.CreateDirectory(Root);
            string temp = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(state));
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(StatePath)) File.Replace(temp, StatePath, StatePath + ".bak");
                else File.Move(temp, StatePath);
            }
            finally { DeleteQuiet(temp); }
        }
        public void Cleanup(ArkCliRuntimeState state)
        {
            if (!Directory.Exists(RuntimeDirectory)) return;
            foreach (string dir in Directory.GetDirectories(RuntimeDirectory))
            {
                string version = Path.GetFileName(dir);
                if (ArkCliVersion.Parse(version) == null || version == state.activeVersion
                    || version == state.previousVersion || version == state.pendingVersion) continue;
                try
                {
                    // Do not follow directory junctions or remove unrelated content.
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                    File.Delete(Path.Combine(dir, "arkcli.exe"));
                    Directory.Delete(dir, false);
                }
                catch (Exception) { }
            }
        }
        public static void DeleteQuiet(string path) { try { if (path != null) File.Delete(path); } catch (Exception) { } }
    }
}
