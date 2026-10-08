using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PocketDeck.Setup
{
    internal static class InstallEngine
    {
        public const string AppName = "PocketDeck";
        public const string ExeName = "PocketDeck.exe";
        public const string Publisher = "PocketDeck 社区修改版";
        public const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PocketDeck";

        public static string DefaultInstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

        public static Stream GetPayload() =>
            typeof(InstallEngine).Assembly.GetManifestResourceStream("app.zip");

        public static void Install(string targetDir, bool desktop, bool startMenu, bool launch,
            Action<string> log = null, Action<int> progress = null)
        {
            log?.Invoke("正在准备安装目录…");
            Directory.CreateDirectory(targetDir);

            // 程序为依赖框架发布（负载仅 14MB，不再内嵌 124MB 运行时），
            // 因此这里必须先确保目标机具备 .NET 10 桌面运行时，否则装完也起不来。
            log?.Invoke("正在检查 .NET 10 桌面运行时…");
            EnsureDesktopRuntime(log);

            log?.Invoke("正在解压应用程序文件…");
            using (var zip = GetPayload())
            {
                if (zip == null) throw new InvalidOperationException("未找到内嵌的安装包数据 (app.zip)。");
                ExtractZip(zip, targetDir, progress);
            }

            string exePath = Path.Combine(targetDir, ExeName);

            log?.Invoke("正在创建快捷方式…");
            if (startMenu)
            {
                string smDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                Directory.CreateDirectory(smDir);
                CreateShortcut(Path.Combine(smDir, AppName + ".lnk"), exePath, targetDir, AppName);
            }
            if (desktop)
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk"),
                    exePath, targetDir, AppName);
            }

            log?.Invoke("正在写入卸载信息…");
            WriteUninstallScript(targetDir);
            WriteRegistry(targetDir, exePath);

            log?.Invoke("安装完成。");
            if (launch)
            {
                log?.Invoke("正在启动应用程序…");
                try
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    log?.Invoke("启动失败: " + ex.Message);
                }
            }
        }

        /// <summary>官方 .NET 10 桌面运行时安装包（x64，微软 aka.ms 短链）。</summary>
        private const string DesktopRuntimeUrl = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";

        /// <summary>检测 .NET 10 桌面运行时是否可用（目录 + 注册表双通道，避免漏判）。</summary>
        public static bool IsDesktopRuntimeInstalled()
        {
            foreach (var root in new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
            })
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                var shared = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
                if (Directory.Exists(shared) &&
                    Directory.EnumerateDirectories(shared).Any(d => Path.GetFileName(d).StartsWith("10.")))
                {
                    return true;
                }
            }
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
                if (key != null && key.GetValueNames().Any(n => n.StartsWith("10."))) return true;
            }
            catch { }
            return false;
        }

        /// <summary>缺运行时则联网安装（静默），装完复检；失败抛出带下载地址的明确错误。</summary>
        private static void EnsureDesktopRuntime(Action<string> log)
        {
            if (IsDesktopRuntimeInstalled())
            {
                log?.Invoke(".NET 10 桌面运行时已就绪。");
                return;
            }
            log?.Invoke("未检测到 .NET 10 桌面运行时，正在联网下载安装…");
            var installer = Path.Combine(Path.GetTempPath(), "windowsdesktop-runtime-10-x64.exe");
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile(DesktopRuntimeUrl, installer);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "下载 .NET 10 桌面运行时失败：" + ex.Message +
                    "\n请手动安装后重试：" + DesktopRuntimeUrl);
            }

            log?.Invoke("正在静默安装运行时（可能需要几分钟，若弹出权限确认请允许）…");
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = installer,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
            }))
            {
                if (process == null) throw new InvalidOperationException("无法启动 .NET 运行时安装程序。");
                if (!process.WaitForExit(600000))
                {
                    throw new InvalidOperationException("安装 .NET 10 桌面运行时超时，请手动安装后重试：" + DesktopRuntimeUrl);
                }
                log?.Invoke("运行时安装程序退出码：" + process.ExitCode);
            }

            if (!IsDesktopRuntimeInstalled())
            {
                throw new InvalidOperationException(
                    "安装后仍未检测到 .NET 10 桌面运行时，请手动安装后重试：" + DesktopRuntimeUrl);
            }
            log?.Invoke(".NET 10 桌面运行时安装完成。");
        }

        private static void ExtractZip(Stream zip, string targetDir, Action<int> progress)
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
            var entries = archive.Entries;
            int total = entries.Count, done = 0;
            foreach (var entry in entries)
            {
                string full = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string dest = Path.Combine(targetDir, full);
                if (full.EndsWith(Path.DirectorySeparatorChar) || string.IsNullOrEmpty(entry.Name))
                {
                    if (!string.IsNullOrEmpty(full)) Directory.CreateDirectory(dest);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, overwrite: true);
                }
                done++;
                if (progress != null && (done % 20 == 0 || done == total))
                    progress((int)(100.0 * done / total));
            }
            progress?.Invoke(100);
        }

        private static void CreateShortcut(string linkPath, string targetPath, string workingDir, string description)
        {
            string ps = "$ws = New-Object -ComObject WScript.Shell\n" +
                        "$sc = $ws.CreateShortcut('" + EscapePs(linkPath) + "')\n" +
                        "$sc.TargetPath = '" + EscapePs(targetPath) + "'\n" +
                        "$sc.WorkingDirectory = '" + EscapePs(workingDir) + "'\n" +
                        "$sc.Description = '" + EscapePs(description) + "'\n" +
                        "$sc.Save()\n";
            RunPowerShellScript(ps);
        }

        private static string EscapePs(string s) => s.Replace("'", "''");

        private static void RunPowerShellScript(string script)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "vrpso_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(tmp, script);
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + tmp + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        private static void WriteUninstallScript(string targetDir)
        {
            string script =
                "$dir = Split-Path -Parent $MyInvocation.MyCommand.Definition\n" +
                "Stop-Process -Name '" + AppName + "' -Force -ErrorAction SilentlyContinue\n" +
                "$sm = [Environment]::GetFolderPath('StartMenu') + '\\Programs\\" + AppName + ".lnk'\n" +
                "$dt = [Environment]::GetFolderPath('Desktop') + '\\" + AppName + ".lnk'\n" +
                "Remove-Item $sm, $dt -Force -ErrorAction SilentlyContinue\n" +
                "Remove-Item 'HKCU:\\" + RegistryKey + "' -Recurse -Force -ErrorAction SilentlyContinue\n" +
                "Set-Location $env:TEMP\n" +
                "Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue\n";
            File.WriteAllText(Path.Combine(targetDir, "uninstall.ps1"), script);
        }

        private static void WriteRegistry(string targetDir, string exePath)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
            string version = "0.2.6";
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.FileVersion)) version = vi.FileVersion;
            }
            catch { }
            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", Publisher);
            key.SetValue("InstallLocation", targetDir);
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("UninstallString",
                "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" +
                Path.Combine(targetDir, "uninstall.ps1") + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        public static void Uninstall(Action<string> log = null)
        {
            string targetDir = null;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                targetDir = key?.GetValue("InstallLocation") as string;
            }
            catch { }

            log?.Invoke("正在停止应用程序…");
            try
            {
                foreach (var p in Process.GetProcessesByName(AppName))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }

            log?.Invoke("正在删除快捷方式…");
            string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk");
            string dt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
            try { File.Delete(sm); } catch { }
            try { File.Delete(dt); } catch { }

            log?.Invoke("正在清理卸载信息…");
            try { Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false); } catch { }

            if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
            {
                log?.Invoke("正在删除安装目录…");
                try { Directory.Delete(targetDir, true); }
                catch (Exception ex) { log?.Invoke("删除目录部分失败: " + ex.Message); }
            }
            log?.Invoke("卸载完成。");
        }
    }
}
