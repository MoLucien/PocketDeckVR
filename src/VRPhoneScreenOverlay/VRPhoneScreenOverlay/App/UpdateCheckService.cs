using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.App;

/// <summary>更新通道：稳定版只取正式发布，测试版连预发布一起取。</summary>
internal enum UpdateChannel
{
	Stable,
	Beta,
}

/// <summary>检查结果状态。</summary>
internal enum UpdateStatus
{
	NotConfigured,
	Idle,
	Checking,
	UpToDate,
	Available,
	Failed,
}

/// <summary>一次发布（GitHub Release 的必要字段）。</summary>
internal sealed record UpdateRelease(string Version, string Tag, string Title, string Notes, string PageUrl, bool PreRelease, DateTimeOffset? PublishedAt);

/// <summary>检查结果。</summary>
internal sealed record UpdateCheckResult(UpdateStatus Status, string CurrentVersion, UpdateRelease? Latest, string Message)
{
	public bool HasUpdate => Status == UpdateStatus.Available && Latest != null;
}

/// <summary>更新偏好：自动检查、通道、更新源（仓库）。存 %LOCALAPPDATA%\PocketDeck\update-prefs.json。</summary>
internal sealed class UpdatePreferences
{
	public bool AutoCheck { get; set; } = true;

	public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

	/// <summary>更新源仓库（"owner/repo"）。为空则用内置默认。</summary>
	public string Repository { get; set; } = string.Empty;

	private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketDeck", "update-prefs.json");

	public static UpdatePreferences Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				UpdatePreferences? loaded = JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(FilePath));
				if (loaded != null)
				{
					return loaded;
				}
			}
		}
		catch (Exception)
		{
		}
		return new UpdatePreferences();
	}

	public void Save()
	{
		try
		{
			string path = FilePath;
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			string temp = path + ".new";
			File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
			File.Move(temp, path, overwrite: true);
		}
		catch (Exception)
		{
		}
	}
}

/// <summary>
/// 更新检查：读 GitHub Releases（公开 API，无需令牌），比较版本，返回最新发布与更新内容。
/// 只做"检查 + 展示"，下载交给浏览器（按用户选择）。
/// </summary>
internal static class UpdateCheckService
{
	private static readonly HttpClient Http = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd("PocketDeckVR/" + AppIdentity.DisplayVersion);
		client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return client;
	}

	/// <summary>解析 "owner/repo"（允许输入完整 URL）。</summary>
	public static bool TryParseRepository(string text, out string owner, out string repo)
	{
		owner = string.Empty;
		repo = string.Empty;
		string value = (text ?? string.Empty).Trim();
		if (value.Length == 0)
		{
			return false;
		}
		value = value.Replace("https://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("http://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Trim('/');
		int slash = value.IndexOf('/');
		if (slash <= 0 || slash == value.Length - 1)
		{
			return false;
		}
		owner = value.Substring(0, slash);
		repo = value.Substring(slash + 1).Split('/')[0];
		return owner.Length > 0 && repo.Length > 0;
	}

	public static async Task<UpdateCheckResult> CheckAsync(string repository, UpdateChannel channel, CancellationToken cancellationToken)
	{
		if (!TryParseRepository(repository, out string owner, out string repo))
		{
			return new UpdateCheckResult(UpdateStatus.NotConfigured, AppIdentity.DisplayVersion, null, "尚未配置更新源仓库");
		}
		try
		{
			string url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=5";
			using HttpResponseMessage response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				string hint = response.StatusCode switch
				{
					System.Net.HttpStatusCode.NotFound => "仓库不存在或未公开",
					System.Net.HttpStatusCode.Forbidden => "请求被 GitHub 限制（稍后再试）",
					_ => "HTTP " + (int)response.StatusCode,
				};
				return new UpdateCheckResult(UpdateStatus.Failed, AppIdentity.DisplayVersion, null, "检查更新失败：" + hint);
			}
			string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			UpdateRelease? latest = SelectLatest(json, channel);
			if (latest == null)
			{
				return new UpdateCheckResult(UpdateStatus.UpToDate, AppIdentity.DisplayVersion, null, (channel == UpdateChannel.Stable) ? "稳定通道暂无正式版（可切到测试通道查看预发布版）" : "该仓库还没有可用发布");
			}
			int compare = CompareVersions(latest.Version, AppIdentity.Version);
			if (compare > 0)
			{
				return new UpdateCheckResult(UpdateStatus.Available, AppIdentity.DisplayVersion, latest, "发现新版本 v" + latest.Version);
			}
			return new UpdateCheckResult(UpdateStatus.UpToDate, AppIdentity.DisplayVersion, latest, "已是最新版本（v" + AppIdentity.DisplayVersion + "）");
		}
		catch (OperationCanceledException)
		{
			return new UpdateCheckResult(UpdateStatus.Failed, AppIdentity.DisplayVersion, null, "检查更新超时");
		}
		catch (Exception ex)
		{
			return new UpdateCheckResult(UpdateStatus.Failed, AppIdentity.DisplayVersion, null, "检查更新失败：" + ex.Message);
		}
	}

	private static UpdateRelease? SelectLatest(string json, UpdateChannel channel)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		if (document.RootElement.ValueKind != JsonValueKind.Array)
		{
			return null;
		}
		List<UpdateRelease> releases = new List<UpdateRelease>();
		foreach (JsonElement item in document.RootElement.EnumerateArray())
		{
			if (GetBool(item, "draft"))
			{
				continue;
			}
			bool preRelease = GetBool(item, "prerelease");
			if (channel == UpdateChannel.Stable && preRelease)
			{
				continue;
			}
			string tag = GetString(item, "tag_name");
			if (tag.Length == 0)
			{
				continue;
			}
			DateTimeOffset? published = DateTimeOffset.TryParse(GetString(item, "published_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed) ? parsed : null;
			releases.Add(new UpdateRelease(
				Version: NormalizeVersion(tag),
				Tag: tag,
				Title: GetString(item, "name") is { Length: > 0 } name ? name : tag,
				Notes: GetString(item, "body"),
				PageUrl: GetString(item, "html_url"),
				PreRelease: preRelease,
				PublishedAt: published));
		}
		return releases
			.OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue)
			.ThenByDescending(release => release.Version, Comparer<string>.Create(CompareVersions))
			.FirstOrDefault();
	}

	private static string GetString(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty) : string.Empty;
	}

	private static bool GetBool(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
	}

	/// <summary>把 "v0.1.1" / "0.1beta" / "1.0.0-rc1" 归一成 "主.次.修订"。</summary>
	public static string NormalizeVersion(string tag)
	{
		string text = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
		List<int> parts = new List<int>();
		string current = string.Empty;
		foreach (char c in text)
		{
			if (char.IsDigit(c))
			{
				current += c;
				continue;
			}
			if (c == '.' && current.Length > 0)
			{
				parts.Add(int.Parse(current, CultureInfo.InvariantCulture));
				current = string.Empty;
				continue;
			}
			break;
		}
		if (current.Length > 0)
		{
			parts.Add(int.Parse(current, CultureInfo.InvariantCulture));
		}
		while (parts.Count < 3)
		{
			parts.Add(0);
		}
		return $"{parts[0]}.{parts[1]}.{parts[2]}";
	}

	/// <summary>比较归一化版本："a" 比 "b" 新返回正数。</summary>
	public static int CompareVersions(string a, string b)
	{
		int[] left = ToNumbers(NormalizeVersion(a));
		int[] right = ToNumbers(NormalizeVersion(b));
		for (int i = 0; i < 3; i++)
		{
			if (left[i] != right[i])
			{
				return left[i] - right[i];
			}
		}
		return 0;
	}

	private static int[] ToNumbers(string version)
	{
		string[] parts = version.Split('.');
		int[] numbers = new int[3];
		for (int i = 0; i < 3 && i < parts.Length; i++)
		{
			int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]);
		}
		return numbers;
	}
}
