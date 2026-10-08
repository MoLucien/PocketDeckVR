using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.App;

/// <summary>
/// <c>--update-now</c>：无界面执行一次「检查 → 下载 → 校验 sha256 → 就地静默安装 → 重启新版本」。
/// 用于自动化验证更新链路，也给高级用户一个命令行更新入口。
/// </summary>
internal static class UpdateNowCommand
{
	public static async Task<int> RunAsync()
	{
		UpdatePreferences preferences = UpdatePreferences.Load();
		string repository = string.IsNullOrWhiteSpace(preferences.Repository) ? AppIdentity.GitHubRepository : preferences.Repository;
		Console.WriteLine("[update] repo=" + repository + " channel=" + preferences.Channel + " current=" + AppIdentity.DisplayVersion);
		UpdateCheckResult result = await UpdateCheckService.CheckAsync(repository, preferences.Channel, CancellationToken.None).ConfigureAwait(false);
		Console.WriteLine("[update] status=" + result.Status + " message=" + result.Message);
		if (!result.HasUpdate || result.Latest == null)
		{
			return 0;
		}
		UpdateRelease release = result.Latest;
		Console.WriteLine("[update] latest=" + release.Version + " asset=" + release.AssetName + (release.AssetIsAppPackage ? "(应用包)" : "(安装器)") + " bytes=" + release.AssetSize + " sha256=" + (release.AssetSha256.Length > 12 ? release.AssetSha256.Substring(0, 12) + "..." : "(none)"));
		string fileName = release.AssetName.Length > 0 ? release.AssetName : ("PocketDeck-" + release.Version + "-setup.exe");
		string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketDeck", "updates", fileName);
		Progress<double> progress = new Progress<double>(delegate(double ratio)
		{
			Console.WriteLine("[update] download " + (int)Math.Round(ratio * 100.0) + "%");
		});
		bool ok = await UpdateCheckService.DownloadAsync(release.AssetUrl, target, release.AssetSha256, progress, CancellationToken.None).ConfigureAwait(false);
		long size = File.Exists(target) ? new FileInfo(target).Length : 0L;
		Console.WriteLine("[update] downloaded=" + ok + " path=" + target + " bytes=" + size);
		if (!ok)
		{
			return 2;
		}
		string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
		string scriptPath = Path.Combine(Path.GetTempPath(), "pocketdeck-update-cli.cmd");
		string apply = release.AssetIsAppPackage
			? "tar -xf \"" + target + "\" -C \"" + baseDir + "\""
			: "\"" + target + "\" --silent --install-dir \"" + baseDir + "\"";
		string script = string.Join("\r\n", new string[]
		{
			"@echo off",
			"ping -n 3 127.0.0.1 >nul",
			apply,
			"start \"\" \"" + Path.Combine(baseDir, AppIdentity.ExecutableName) + "\"",
			"del \"%~f0\"",
			string.Empty,
		});
		File.WriteAllText(scriptPath, script, System.Text.Encoding.Default);
		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
		});
		Console.WriteLine("[update] deferred install started install-dir=" + baseDir);
		return 0;
	}
}
