using System;
using System.IO;

namespace ArkLeft
{
    // First-run marker + test-isolatable state directory. The marker contains
    // only a date/version line: never identity, quota or credentials.
    internal static class Marker
    {
        public const string StateDirEnv = "ARK_LEFT_STATE_DIR";

        public static string StateDir()
        {
            string custom = Environment.GetEnvironmentVariable(StateDirEnv);
            if (!string.IsNullOrEmpty(custom)) return custom;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local)) local = Path.GetTempPath();
            return Path.Combine(local, "ark_left");
        }

        public static string MarkerPath()
        {
            return Path.Combine(StateDir(), "first-run.done");
        }

        public static bool Exists()
        {
            try { return File.Exists(MarkerPath()); }
            catch (Exception) { return false; }
        }

        public static bool ShouldShowIntro()
        {
            return !Exists();
        }

        public static void WriteFirstRun()
        {
            try
            {
                Directory.CreateDirectory(StateDir());
                string line = "version=" + System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version + " date=" + DateTime.Now.ToString("yyyy-MM-dd");
                File.WriteAllText(MarkerPath(), line);
            }
            catch (Exception) { }
        }
    }
}
