using System;
using System.Text;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private static void FloatingSettingsStoreCases()
        {
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_float_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                FloatingSettings s = new FloatingSettings();
                s.Version = FloatingSettingsStore.FormatVersion;
                s.ProductKey = "agent-plan|personal|medium";
                s.PeriodLabel = "5h";
                Check("floatSetting.save", FloatingSettingsStore.Save(s), true);
                FloatingSettings back = FloatingSettingsStore.Load();
                Check("floatSetting.loadKey", back.ProductKey, s.ProductKey);
                Check("floatSetting.loadPeriod", back.PeriodLabel, "5h");
                Check("floatSetting.loadVersion", back.Version, 1);

                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath, "{not json", Encoding.UTF8);
                Check("floatSetting.corruptNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":2,\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongVersionNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":\"\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.emptyKeyNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\",\"Extra\":1}", Encoding.UTF8);
                Check("floatSetting.extraFieldNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":1,\"ProductKey\":123,\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongTypeKeyNull", FloatingSettingsStore.Load(), null);
                System.IO.File.WriteAllText(FloatingSettingsStore.FilePath,
                    "{\"Version\":\"1\",\"ProductKey\":\"a|b|c\",\"PeriodLabel\":\"5h\"}", Encoding.UTF8);
                Check("floatSetting.wrongTypeVersionNull", FloatingSettingsStore.Load(), null);
                FloatingSettings longKey = new FloatingSettings {
                    Version = 1, ProductKey = new string('x', 129), PeriodLabel = "5h" };
                Check("floatSetting.longKeyReject", FloatingSettingsStore.Valid(longKey), false);
                FloatingSettings longPeriod = new FloatingSettings {
                    Version = 1, ProductKey = "a|b|c", PeriodLabel = new string('y', 65) };
                Check("floatSetting.longPeriodReject", FloatingSettingsStore.Valid(longPeriod), false);
                Check("floatSetting.nullReject", FloatingSettingsStore.Valid(null), false);

                FloatingSettings valid = new FloatingSettings {
                    Version = 1, ProductKey = "a|b|c", PeriodLabel = "5h" };
                Check("floatSetting.resave", FloatingSettingsStore.Save(valid), true);
                using (System.IO.FileStream locked = new System.IO.FileStream(
                    FloatingSettingsStore.FilePath, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    FloatingSettings other = new FloatingSettings {
                        Version = 1, ProductKey = "z|y|x", PeriodLabel = "weekly" };
                    Check("floatSetting.replaceFailure", FloatingSettingsStore.Save(other), false);
                }
                Check("floatSetting.replaceRetains", FloatingSettingsStore.Load().ProductKey, "a|b|c");
                FloatingSettingsStore.Clear();
                Check("floatSetting.clear", FloatingSettingsStore.Load(), null);
                Check("floatSetting.notCacheFile", FloatingSettingsStore.FileName == "quota-cache.dat", false);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }
    }
}
