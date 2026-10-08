using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("ark_left")]
[assembly: AssemblyProduct("ark_left")]
[assembly: AssemblyDescription("火山方舟订阅额度托盘查询工具")]
[assembly: AssemblyVersion("0.24.2.0")]
[assembly: AssemblyFileVersion("0.24.2.0")]

namespace ArkLeft
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool smoke = false;
            bool forceShow = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--smoke-test", StringComparison.OrdinalIgnoreCase))
                    smoke = true;
                else if (string.Equals(args[i], "--show", StringComparison.OrdinalIgnoreCase))
                    forceShow = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (smoke)
            {
                // Offline layout smoke test: never touches the first-run marker or
                // the single-instance IPC used by a real running instance.
                return TrayApp.RunSmokeTest();
            }

            return TrayApp.Run(args, forceShow);
        }
    }
}
