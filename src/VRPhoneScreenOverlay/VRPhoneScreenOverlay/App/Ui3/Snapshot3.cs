using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using PocketDeck.Settings;

namespace PocketDeck.App.Ui3;

/// <summary>v3 快照：把新外壳按多种状态与缩放渲染成 PNG，用于视觉自检。</summary>
internal static class Snapshot3
{
	public static int Run(string? outputDirectory)
	{
		try
		{
			string root = Path.GetFullPath(string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Environment.CurrentDirectory, "artifacts", "ui3") : outputDirectory);
			Directory.CreateDirectory(root);
			RenderSet(root, 1f, 980, 820);
			RenderSet(Path.Combine(root, "narrow"), 1f, 820, 820);
			RenderSet(Path.Combine(root, "150"), 1.5f, 980, 820);
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.ToString());
			return 20;
		}
	}

	private static void RenderSet(string dir, float scale, int logicalWidth, int logicalHeight)
	{
		Directory.CreateDirectory(dir);
		Render(dir, "home-live.png", scale, logicalWidth, logicalHeight, State.HomeRunning);
		Render(dir, "home-idle.png", scale, logicalWidth, logicalHeight, State.HomeIdle);
		Render(dir, "home-error.png", scale, logicalWidth, logicalHeight, State.HomeError);
		Render(dir, "settings.png", scale, logicalWidth, logicalHeight, State.Settings);
		Render(dir, "settings-dirty.png", scale, logicalWidth, logicalHeight, State.SettingsDirty);
		Render(dir, "about.png", scale, logicalWidth, logicalHeight, State.About);
	}

	private enum State
	{
		HomeIdle,
		HomeRunning,
		HomeError,
		Settings,
		SettingsDirty,
		About,
	}

	private static void Render(string dir, string file, float scale, int logicalWidth, int logicalHeight, State state)
	{
		int width = (int)MathF.Round(logicalWidth * scale);
		int height = (int)MathF.Round(logicalHeight * scale);
		using Shell3 shell = new Shell3
		{
			Location = Point.Empty,
			Size = new Size(width, height)
		};
		Home3 home = new Home3();
		Settings3 settings = new Settings3();
		About3 about = new About3();
		shell.SetProductName("PocketDeck VR");
		shell.SetPages(home, settings, about);
		shell.SetTabTitles("主页", "设置", "关于");
		shell.SetHeaderStatus(state == State.HomeRunning ? "已连接 · vivo V2307A · WiFi" : "等待手机", state == State.HomeRunning ? Tone.Ok : Tone.Info, state == State.HomeRunning);
		shell.ApplyScale(scale);
		Configure(home, settings, about, state);

		int page = state switch
		{
			State.Settings or State.SettingsDirty => 1,
			State.About => 2,
			_ => 0,
		};
		shell.SelectPage(page);
		shell.Relayout();
		CreateVisibleTree(shell);
		shell.Relayout();
		Application.DoEvents();

		using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
		bitmap.SetResolution(96f, 96f);
		shell.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
		string target = Path.Combine(dir, file);
		string temp = target + ".new";
		bitmap.Save(temp, ImageFormat.Png);
		File.Move(temp, target, overwrite: true);
		if (string.Equals(Environment.GetEnvironmentVariable("UI3_DUMP"), "1", StringComparison.Ordinal) && file == "home-live.png")
		{
			File.WriteAllText(Path.Combine(dir, "dump.txt"),
				$"shell={shell.ClientSize.Width}x{shell.ClientSize.Height} scale={scale} twoCol={logicalWidth}\n" +
				home.DumpSlots() + "\nsettings:\n" + settings.DumpSlots());
		}
	}

	private static void CreateVisibleTree(Control control)
	{
		control.CreateControl();
		foreach (Control child in control.Controls)
		{
			if (child.Visible)
			{
				CreateVisibleTree(child);
			}
		}
	}

	private static void Configure(Home3 home, Settings3 settings, About3 about, State state)
	{
		// 通用：选项内容
		settings.Hand.SetNodes(new FixedChoiceNode3<ControllerHandPreference>[2]
		{
			new FixedChoiceNode3<ControllerHandPreference>(ControllerHandPreference.Left, "左手"),
			new FixedChoiceNode3<ControllerHandPreference>(ControllerHandPreference.Right, "右手")
		}, ControllerHandPreference.Right);
		IReadOnlyList<VideoResolutionProfile> profiles = VideoResolutionProfiles.Create(1440, 3200);
		List<FixedChoiceNode3<VideoResolutionProfile>> resNodes = new List<FixedChoiceNode3<VideoResolutionProfile>>();
		foreach (VideoResolutionProfile profile in profiles)
		{
			resNodes.Add(new FixedChoiceNode3<VideoResolutionProfile>(profile, profile.Percent + "%"));
		}
		VideoResolutionProfile selected = profiles.Count > 0 ? profiles[0] : default;
		foreach (VideoResolutionProfile profile in profiles)
		{
			if (profile.Percent == 80)
			{
				selected = profile;
			}
		}
		settings.Resolution.SetNodes(resNodes, selected);
		List<FixedChoiceNode3<int>> bitrates = new List<FixedChoiceNode3<int>>();
		List<FixedChoiceNode3<int>> rates = new List<FixedChoiceNode3<int>>();
		foreach (int value in AppSettingsPolicy.VideoBitratesMbps)
		{
			bitrates.Add(new FixedChoiceNode3<int>(value, value + " Mbps"));
		}
		foreach (int value in AppSettingsPolicy.VideoMaximumFrameRates)
		{
			rates.Add(new FixedChoiceNode3<int>(value, value + " FPS"));
		}
		bitrates.Sort((a, b) => a.Value.CompareTo(b.Value));
		rates.Sort((a, b) => a.Value.CompareTo(b.Value));
		settings.Bitrate.SetNodes(bitrates, 16);
		settings.FrameRate.SetNodes(rates, 60);
		settings.KeepAwake.Checked = true;
		settings.UnlockKeypad.Checked = false;
		settings.SetQuality("80% · 16 Mbps · 60 FPS · 已保存", Tone.Ok);
		home.Multiplier.SetNodes(new FixedChoiceNode3<int>[5]
		{
			new FixedChoiceNode3<int>(1, "1x"),
			new FixedChoiceNode3<int>(5, "5x"),
			new FixedChoiceNode3<int>(10, "10x"),
			new FixedChoiceNode3<int>(20, "20x"),
			new FixedChoiceNode3<int>(40, "40x")
		}, 1);
		about.SetIdentity("PocketDeck VR", "在 VR 里悬浮你的手机屏幕");
		about.SetUpdateState("发现新版本 v0.2.0", Tone.Accent, true, "0.2.0", "本次更新：手柄绑定健康判定修正；无线自动连接；更新检查上线。（示例文案，用于渲染验证）");
		about.SetDetails(AppIdentity.DisplayVersion, "local.pocketdeck.desktop.v1", "可用");
		about.SetFooter("SteamVR 原生浮窗 · 本地运行，仅在检查更新时联网");

		switch (state)
		{
			case State.HomeIdle:
				home.SetHero("手机浮窗未运行", "连接手机后即可把屏幕搬进 SteamVR");
				home.SetPhone("等待设备", Tone.Info, false, "内置 ADB 已就绪，正在扫描手机", "已发现 0 台设备；请连接已启用 USB 调试的手机", "未开通；首次请先用 USB 数据线连接手机");
				home.SetStream(0, "未运行", Tone.Muted, false);
				home.SetStream(1, "未运行", Tone.Muted, false);
				home.SetStream(2, "未运行", Tone.Muted, false);
				home.SetStream(3, "已连接", Tone.Ok, false);
				home.SetMetrics("--", 0f, "--", 0f, "--", 0f, "--", 0f);
				home.SetPlayspace("已关闭", Tone.Muted, "0.00", "0.00", "0.00");
				home.Action.Text = "开启手机浮窗";
				home.Action.Icon = Icon3.Play;
				home.Action.Variant = Button3.Look.Soft;
				home.Action.Enabled = false;
				break;
			case State.HomeRunning:
				home.SetHero("手机浮窗运行中", "vivo V2307A · WiFi 无线 · 1080×2400");
				home.SetPhone("已连接", Tone.Ok, true, "ADB 37.0.0 · 视频协议 4.1 · WiFi 无线", "vivo V2307A · Android 16\n1260 × 2800", "已连接，可直接开启手机浮窗");
				home.SetStream(0, "运行中", Tone.Ok, true);
				home.SetStream(1, "播放中", Tone.Ok, true);
				home.SetStream(2, "已就绪", Tone.Ok, false);
				home.SetStream(3, "已连接", Tone.Ok, false);
				home.SetMetrics("1080×2400", 1f, "15.8 Mbps", 0.49f, "59.7 FPS", 0.99f, "42 ms", 0.28f);
				home.SetPlayspace("已启用", Tone.Ok, "0.12", "0.04", "-0.28");
				home.Action.Text = "关闭手机浮窗";
				home.Action.Icon = Icon3.Pause;
				home.Action.Variant = Button3.Look.Danger;
				home.Action.Enabled = true;
				home.PlayspaceSwitch.Checked = true;
				break;
			case State.HomeError:
				home.SetHero("需要手机授权", "解锁手机并允许这台电脑进行 USB 调试");
				home.SetPhone("需要授权", Tone.Warn, true, "已发现设备，但 ADB 返回 unauthorized", "请解锁手机并允许这台电脑进行 USB 调试", "未开通");
				home.SetStream(0, "不可用", Tone.Bad, false);
				home.SetStream(1, "等待手机", Tone.Warn, true);
				home.SetStream(2, "不可用", Tone.Bad, false);
				home.SetStream(3, "已连接", Tone.Ok, false);
				home.SetMetrics("--", 0f, "--", 0f, "--", 0f, "--", 0f);
				home.SetPlayspace("已关闭", Tone.Muted, "0.00", "0.00", "0.00");
				home.Action.Text = "开启手机浮窗";
				home.Action.Icon = Icon3.Play;
				home.Action.Variant = Button3.Look.Soft;
				home.Action.Enabled = false;
				break;
			case State.SettingsDirty:
				settings.SetDirty(true);
				settings.SetQuality("90% · 24 Mbps · 72 FPS · 待应用", Tone.Warn);
				settings.SetBindingNotice("检测到绑定文件缺失，点「手柄绑定」重新加载", Tone.Warn);
				settings.Apply.Enabled = true;
				settings.Discard.Enabled = true;
				break;
			default:
				settings.SetDirty(false);
				settings.Apply.Enabled = false;
				settings.Discard.Enabled = false;
				break;
		}
	}
}
