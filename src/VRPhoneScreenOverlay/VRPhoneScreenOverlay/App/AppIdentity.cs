namespace PocketDeck.App;

/// <summary>
/// 产品身份：全部对外可见的名字/标识集中在此，改名只需改这一处。
/// （程序集与安装包文件名见各自工程文件与 pack.ps1。）
/// </summary>
internal static class AppIdentity
{
	/// <summary>显示名（窗口标题、标题栏、关于页、安装包 DisplayName）。</summary>
	public const string ProductName = "PocketDeck VR";

	/// <summary>短名（开始菜单/桌面快捷方式、进程名、安装目录）。</summary>
	public const string ShortName = "PocketDeckVR";

	/// <summary>对外显示的版本号（按需求为 0.1beta；程序集元数据为 0.1.0.0）。</summary>
	public const string DisplayVersion = "0.4beta";

	/// <summary>用于版本比较的数值版本（三段式）。</summary>
	public const string Version = "0.4.0";

	/// <summary>开源许可名称与全文地址。</summary>
	public const string LicenseName = "Boost Software License 1.0";

	public const string LicenseUrl = "https://www.boost.org/LICENSE_1_0.txt";

	/// <summary>更新源仓库（"owner/repo"）。留空则关于页显示"尚未配置更新源"，可在偏好里覆盖。</summary>
	public const string GitHubRepository = "MoLucien/PocketDeckVR";

	/// <summary>问题反馈地址（留空则由更新源仓库推导 /issues）。</summary>
	public const string FeedbackUrl = "";

	/// <summary>可执行文件名。</summary>
	public const string ExecutableName = "PocketDeck.exe";

	/// <summary>一句话定位。</summary>
	public const string Tagline = "在 VR 里悬浮你的手机屏幕";

	/// <summary>关于页页脚。</summary>
	public const string Footer = "本地运行 · 无云端上传 · SteamVR 原生浮窗";

	/// <summary>SteamVR 应用标识（与 manifest.vrmanifest 的 app_key 一致）。</summary>
	public const string SteamVrAppKey = "local.pocketdeck.desktop.v1";

	/// <summary>%LOCALAPPDATA% 下的数据目录名。</summary>
	public const string DataFolderName = "PocketDeckVR";

	/// <summary>发行方（安装包注册表项）。</summary>
	public const string Publisher = "PocketDeck Project";
}
