using System;
using System.Windows.Forms;

namespace PocketDeck.Setup
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool silent = false, uninstall = false, launch = false;
            string installDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--silent" || a == "-s") silent = true;
                else if (a == "--uninstall" || a == "-u") uninstall = true;
                else if (a == "--launch") launch = true;
                else if (a == "--install-dir" || a == "-i")
                {
                    if (i + 1 < args.Length) installDir = args[++i];
                }
                else if (a == "--help" || a == "-h" || a == "-?")
                {
                    Console.WriteLine("PocketDeck 安装程序");
                    Console.WriteLine("用法:");
                    Console.WriteLine("  (无参数)                                 启动图形安装向导");
                    Console.WriteLine("  --silent --install-dir <路径> [--launch]   静默安装到指定目录");
                    Console.WriteLine("  --silent --uninstall                     静默卸载");
                    return 0;
                }
            }

            if (silent)
            {
                if (uninstall)
                {
                    InstallEngine.Uninstall(Console.WriteLine);
                    return 0;
                }
                if (installDir == null) installDir = InstallEngine.DefaultInstallDir;
                try
                {
                    InstallEngine.Install(installDir, true, true, launch, Console.WriteLine, null);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("安装失败: " + ex.Message);
                    return 1;
                }
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.Run(new InstallerForm());
            return 0;
        }
    }
}
