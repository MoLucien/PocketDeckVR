using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using PocketDeck.Android;
using PocketDeck.Contracts;
using PocketDeck.Core;
using PocketDeck.Session;
using PocketDeck.Settings;
using PocketDeck.SteamVR;

using PocketDeck.App.Ui3;

namespace PocketDeck.App;

/// <summary>
/// 主窗体：只负责“服务 ↔ 视图”的接线，界面结构与绘制全部下沉到 Views 与自绘控件。
/// </summary>
internal sealed class MainForm : Form
{
	private enum OverlayButtonProgress
	{
		None,
		Opening,
		Closing
	}

	private sealed record DeviceListItem(AndroidDeviceView Device)
	{
		public override string ToString()
		{
			return $"{(Device.IsSelected ? "●" : "○")} {Device.DisplayName}\u3000{AndroidDisplayNames.Transport(Device.Transport)}\u3000{AndroidDisplayNames.DeviceStatus(Device.Status)}\u3000标识 {Device.DeviceKey}";
		}
	}

	private static readonly int[] PlayspaceDragMultipliers = new int[5] { 1, 5, 10, 20, 40 };

	private const string ProductId = "local.pocketdeck.desktop.v1";

	private readonly AppRuntime _runtime;

	private readonly IAndroidConnectionService _phone;

	private readonly IPhoneVideoDecodeProbeService _videoProbe;

	private readonly IPhoneOverlayService _phoneOverlay;

	private readonly IPhoneAudioService _phoneAudio;

	private readonly IPhoneControlService _phoneControl;

	private readonly IPhoneMediaSessionCoordinator _mediaSession;

	private readonly IOpenVrPlayspaceDragService _playspaceDrag;

	private readonly IAppSettingsService _settings;

	private readonly string? _connectionLogPath;

	private readonly CancellationTokenSource _formLifetime = new CancellationTokenSource();

	// ——— v3 视图（全部为新实现，旧 UI 已移除） ———
	private Shell3 _shell;

	private Home3 _home;

	private Settings3 _settingsPage;

	private About3 _aboutPage;


	private Ui3.Sink _phoneStatus;

	private Ui3.Sink _phoneDetails;

	private Ui3.Sink _adbDetail;

	private Ui3.Sink _wifiDetail;

	private Ui3.Sink _videoStatus;

	private Ui3.Sink _audioStatus;

	private Ui3.Sink _controlStatus;

	private Ui3.Sink _steamVrStatus;

	private Ui3.Sink _metricResolution;

	private Ui3.Sink _metricBitrate;

	private Ui3.Sink _metricFrameRate;

	private Ui3.Sink _metricLatency;

	private Ui3.Sink _playspaceStatus;

	private Ui3.Sink _playspaceCoordinates;

	private Ui3.Sink _qualityStatus;

	private Ui3.Sink _operationStatus;

	private Ui3.Sink _runtimeStatus;

	private Button3 _phoneOverlayButton;

	private Button3 _steamVrBindingsButton;

	private Button3 _videoProbeButton;


	private Button3 _applySettingsButton;

	private Button3 _discardSettingsButton;

	private Switch3 _keepAwakeWhileGrabbedToggle;

	private Switch3 _vrUnlockKeypadToggle;

	private Switch3 _playspaceDragToggle;

	private Segments3<ControllerHandPreference> _controllerHandSelector;

	private Segments3<VideoResolutionProfile> _resolutionSelector;

	private Stepper3<int> _playspaceDragMultiplierSelector;

	private Stepper3<int> _bitrateSelector;

	private Stepper3<int> _frameRateSelector;

	/// <summary>设备选择/扫描是内部管道，保留隐藏控件以维持原有行为契约。</summary>
	private ListBox _deviceList;

	private Button _refreshButton;

	// ——— 运行状态 ———
	private DateTimeOffset _nextPhoneControlStartAt;

	private bool _updatingDeviceList;

	private bool _operationInProgress;

	private bool _immediateSettingsInProgress;

	private bool _bindingRecoveryInProgress;

	private bool _updatingSettingsControls;

	private AppSettings _pendingSettings;

	private bool _settingsDirty;

	private OverlayButtonProgress _overlayButtonProgress;

	private Icon? _windowIcon;

	// ——— 显示缩放 ———
	private float _displayScale = 1f;

	private bool _applyingScale;

	private Point _dragStartCursor;

	private Point _dragStartWindow;

	private static string DisplayVersion => AppIdentity.DisplayVersion;

	public MainForm(AppRuntime runtime, IAndroidConnectionService phone, IPhoneVideoDecodeProbeService videoProbe, IPhoneOverlayService phoneOverlay, IPhoneAudioService phoneAudio, IPhoneControlService phoneControl, IPhoneMediaSessionCoordinator mediaSession, IOpenVrPlayspaceDragService playspaceDrag, IAppSettingsService settings, string? connectionLogPath)
	{
		_runtime = runtime ?? throw new ArgumentNullException("runtime");
		_phone = phone ?? throw new ArgumentNullException("phone");
		_videoProbe = videoProbe ?? throw new ArgumentNullException("videoProbe");
		_phoneOverlay = phoneOverlay ?? throw new ArgumentNullException("phoneOverlay");
		_phoneAudio = phoneAudio ?? throw new ArgumentNullException("phoneAudio");
		_phoneControl = phoneControl ?? throw new ArgumentNullException("phoneControl");
		_mediaSession = mediaSession ?? throw new ArgumentNullException("mediaSession");
		_playspaceDrag = playspaceDrag ?? throw new ArgumentNullException("playspaceDrag");
		_settings = settings ?? throw new ArgumentNullException("settings");
		_connectionLogPath = connectionLogPath;
		_pendingSettings = _settings.Snapshot.Value;

		BuildVisualTree(connectionLogPath != null);
		AttachEvents();

		_phoneOverlay.ConfigureLockScreenFeatures(_pendingSettings.KeepAwakeWhileGrabbed, _pendingSettings.VrUnlockKeypadEnabled);
		RefreshSettingsControls();
		UpdatePhoneView(_phone.Snapshot);
		UpdateSteamVrBindingNotice(_phoneOverlay.Snapshot);
		UpdateDashboardSnapshots();
	}

	private void AttachEvents()
	{
		_runtime.StateChanged += OnRuntimeStateChanged;
		_phone.StateChanged += OnPhoneStateChanged;
		_phoneOverlay.StateChanged += OnPhoneOverlayStateChanged;
		_phoneAudio.StateChanged += OnPhoneAudioStateChanged;
		_phoneControl.StateChanged += OnPhoneControlStateChanged;
		_mediaSession.StateChanged += OnPhoneMediaSessionStateChanged;
		_playspaceDrag.StateChanged += OnPlayspaceDragStateChanged;
		_settings.Changed += OnSettingsChanged;
		_deviceList.SelectedIndexChanged += OnDeviceSelected;
		_refreshButton.Click += OnRefreshClicked;
		_videoProbeButton.Click += OnVideoProbeClicked;
		_phoneOverlayButton.Click += OnPhoneOverlayClicked;
		_steamVrBindingsButton.Click += OnSteamVrBindingsClicked;
		_keepAwakeWhileGrabbedToggle.CheckedChanged += OnImmediateLockScreenSettingChanged;
		_vrUnlockKeypadToggle.CheckedChanged += OnImmediateLockScreenSettingChanged;
		_controllerHandSelector.SelectedValueChanged += OnSettingChoiceChanged;
		_playspaceDragToggle.CheckedChanged += OnPlayspaceDragChanged;
		_playspaceDragMultiplierSelector.SelectedValueChanged += OnPlayspaceDragMultiplierChanged;
		_resolutionSelector.SelectedValueChanged += OnSettingChoiceChanged;
		_bitrateSelector.SelectedValueChanged += OnSettingChoiceChanged;
		_frameRateSelector.SelectedValueChanged += OnSettingChoiceChanged;
		_applySettingsButton.Click += OnApplySettingsClicked;
		_discardSettingsButton.Click += OnDiscardSettingsClicked;
		FormClosed += OnFormClosed;
	}

	private void DetachEvents()
	{
		_runtime.StateChanged -= OnRuntimeStateChanged;
		_phone.StateChanged -= OnPhoneStateChanged;
		_phoneOverlay.StateChanged -= OnPhoneOverlayStateChanged;
		_phoneAudio.StateChanged -= OnPhoneAudioStateChanged;
		_phoneControl.StateChanged -= OnPhoneControlStateChanged;
		_mediaSession.StateChanged -= OnPhoneMediaSessionStateChanged;
		_playspaceDrag.StateChanged -= OnPlayspaceDragStateChanged;
		_settings.Changed -= OnSettingsChanged;
		_deviceList.SelectedIndexChanged -= OnDeviceSelected;
		_refreshButton.Click -= OnRefreshClicked;
		_videoProbeButton.Click -= OnVideoProbeClicked;
		_phoneOverlayButton.Click -= OnPhoneOverlayClicked;
		_steamVrBindingsButton.Click -= OnSteamVrBindingsClicked;
		_keepAwakeWhileGrabbedToggle.CheckedChanged -= OnImmediateLockScreenSettingChanged;
		_vrUnlockKeypadToggle.CheckedChanged -= OnImmediateLockScreenSettingChanged;
		_controllerHandSelector.SelectedValueChanged -= OnSettingChoiceChanged;
		_playspaceDragToggle.CheckedChanged -= OnPlayspaceDragChanged;
		_playspaceDragMultiplierSelector.SelectedValueChanged -= OnPlayspaceDragMultiplierChanged;
		_resolutionSelector.SelectedValueChanged -= OnSettingChoiceChanged;
		_bitrateSelector.SelectedValueChanged -= OnSettingChoiceChanged;
		_frameRateSelector.SelectedValueChanged -= OnSettingChoiceChanged;
		_applySettingsButton.Click -= OnApplySettingsClicked;
		_discardSettingsButton.Click -= OnDiscardSettingsClicked;

	}

	// ============================ 视图构建 ============================

	private void BuildVisualTree(bool connectionLogEnabled)
	{
		Text = AppIdentity.ProductName;
		_windowIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		if (_windowIcon != null)
		{
			Icon = _windowIcon;
		}
		StartPosition = FormStartPosition.CenterScreen;
		AutoScaleMode = AutoScaleMode.None;
		AutoSize = false;
		FormBorderStyle = FormBorderStyle.None;
		ControlBox = false;
		MaximizeBox = false;
		MinimizeBox = false;
		SizeGripStyle = SizeGripStyle.Hide;
		DoubleBuffered = true;
		BackColor = Tok.Canvas;
		ForeColor = Tok.Text1;
		Font = Tok.Body();

		_displayScale = Math.Max(1f, DeviceDpi / 96f);
		Size defaultSize = ScaledSize(Tok.DefaultWidth, Tok.DefaultHeight);
		Size working = Screen.FromPoint(Cursor.Position).WorkingArea.Size;
		defaultSize = new Size(Math.Min(defaultSize.Width, (int)(working.Width * 0.94f)), Math.Min(defaultSize.Height, (int)(working.Height * 0.94f)));
		MinimumSize = ScaledSize(Tok.MinWidth, Tok.MinHeight);
		ClientSize = defaultSize;

		_home = new Home3();
		_settingsPage = new Settings3();
		_aboutPage = new About3();

		_shell = new Shell3 { Location = Point.Empty, Size = ClientSize, Dock = DockStyle.Fill };
		_shell.SetProductName(AppIdentity.ProductName);
		_shell.SetPages(_home, _settingsPage, _aboutPage);
		_shell.SetTabTitles("主页", "设置", "关于");
		_shell.ApplyScale(_displayScale);
		_home.Probe.Tooltip("探测手机的编码与显卡解码链路");

		// —— 状态适配器：既有业务逻辑继续写「文本 + 颜色」，由 v3 视图承接 ——
		_adbDetail = new Sink((text, _) => _home.SetAdbDetail(text));
		_phoneStatus = new Sink((text, color) => _home.SetPhoneStatus(text, ToneOf(color)));
		_phoneDetails = new Sink((text, _) => _home.SetPhoneDetail(text));
		_wifiDetail = new Sink((text, _) => _home.SetWifiDetail(text));
		_videoStatus = new Sink((text, color) => _home.SetStream(0, text, ToneOf(color), IsLiveTone(color)));
		_audioStatus = new Sink((text, color) => _home.SetStream(1, text, ToneOf(color), IsLiveTone(color)));
		_controlStatus = new Sink((text, color) => _home.SetStream(2, text, ToneOf(color), false));
		_steamVrStatus = new Sink((text, color) => _home.SetStream(3, text, ToneOf(color), false));
		_metricResolution = new Sink((text, _) => _home.SetMetric(0, text));
		_metricBitrate = new Sink((text, _) => _home.SetMetric(1, text));
		_metricFrameRate = new Sink((text, _) => _home.SetMetric(2, text));
		_metricLatency = new Sink((text, _) => _home.SetMetric(3, text));
		_playspaceStatus = new Sink((text, color) => _home.SetPlayspaceStatus(text, ToneOf(color)));
		_playspaceCoordinates = new Sink((text, _) => _home.SetCoordinates(text));
		_qualityStatus = new Sink((text, color) => _settingsPage.SetQuality(text, ToneOf(color)));
		_operationStatus = new Sink(
			(text, color) => ApplyStatusMessage(text, color),
			(text, color) => NotifyOutcome(text, ToneOf(color)));
		_runtimeStatus = new Sink((_, _) => { });

		_phoneOverlayButton = _home.Action;
		_videoProbeButton = _home.Probe;
		_steamVrBindingsButton = _settingsPage.Bindings;
		_applySettingsButton = _settingsPage.Apply;
		_discardSettingsButton = _settingsPage.Discard;
		_keepAwakeWhileGrabbedToggle = _settingsPage.KeepAwake;
		_vrUnlockKeypadToggle = _settingsPage.UnlockKeypad;
		_controllerHandSelector = _settingsPage.Hand;
		_resolutionSelector = _settingsPage.Resolution;
		_bitrateSelector = _settingsPage.Bitrate;
		_frameRateSelector = _settingsPage.FrameRate;
		_playspaceDragToggle = _home.PlayspaceSwitch;
		_playspaceDragMultiplierSelector = _home.Multiplier;

		_aboutPage.SetIdentity(AppIdentity.ProductName, AppIdentity.Tagline);
		_aboutPage.SetDetails(DisplayVersion, ProductId, connectionLogEnabled ? "可用" : "不可写");
		_aboutPage.SetAutoCheck(_updatePrefs.AutoCheck);
		_aboutPage.SetChannelText(_updatePrefs.Channel == UpdateChannel.Stable ? "稳定版" : "测试版");
		_aboutPage.CheckUpdate.Click += async delegate
		{
			await CheckForUpdatesAsync(manual: true);
		};
		_aboutPage.Feedback.Click += delegate
		{
			OpenExternalUrl(FeedbackUrl);
		};
		_aboutPage.OpenDownload.Click += async delegate
		{
			await PerformInAppUpdateAsync();
		};
		_aboutPage.AutoCheck.CheckedChanged += delegate
		{
			_updatePrefs.AutoCheck = _aboutPage.AutoCheck.Checked;
			_updatePrefs.Save();
		};
		_aboutPage.ChannelToggle.Click += async delegate
		{
			_updatePrefs.Channel = (_updatePrefs.Channel == UpdateChannel.Stable) ? UpdateChannel.Beta : UpdateChannel.Stable;
			_updatePrefs.Save();
			_aboutPage.SetChannelText(_updatePrefs.Channel == UpdateChannel.Stable ? "稳定版" : "测试版");
			await CheckForUpdatesAsync(manual: true);
		};
		_aboutPage.SetLicense(AppIdentity.LicenseName, "随程序 licenses 目录");
		_aboutPage.LicenseViewRequested += delegate
		{
			OpenLocalPath(Path.Combine(AppContext.BaseDirectory, "LICENSE"));
		};
		_aboutPage.LicenseFolderRequested += delegate
		{
			OpenLocalPath(Path.Combine(AppContext.BaseDirectory, "licenses"));
		};
		_aboutPage.SetFooter(AppIdentity.Footer);

		// 设备选择/扫描仍是内部管道：隐藏控件维持原有契约
		_deviceList = new ListBox { Visible = false };
		_refreshButton = new Button { Visible = false };
		Controls.Add(_deviceList);
		Controls.Add(_refreshButton);
		Controls.Add(_shell);

		_controllerHandSelector.SetNodes(new FixedChoiceNode3<ControllerHandPreference>[2]
		{
			new FixedChoiceNode3<ControllerHandPreference>(ControllerHandPreference.Left, "左手"),
			new FixedChoiceNode3<ControllerHandPreference>(ControllerHandPreference.Right, "右手")
		}, _pendingSettings.ControllerHand);
		_playspaceDragMultiplierSelector.SetNodes(PlayspaceDragMultipliers.Select((int value) => new FixedChoiceNode3<int>(value, value + "x")), 1);
		_bitrateSelector.SetNodes(from value in AppSettingsPolicy.VideoBitratesMbps.Order()
			select new FixedChoiceNode3<int>(value, value + " Mbps"), _pendingSettings.VideoBitrateMbps);
		_frameRateSelector.SetNodes(from value in AppSettingsPolicy.VideoMaximumFrameRates.Order()
			select new FixedChoiceNode3<int>(value, value + " FPS"), _pendingSettings.VideoMaximumFramesPerSecond);

		Resize += (_, _) => _shell.Relayout();
		ApplyDisplayScale(_displayScale);
	}

	/// <summary>把语义颜色映射回 v3 色调（业务逻辑继续用颜色表达状态）。</summary>
	private static Tone ToneOf(Color color)
	{
		if (color == Tok.Ok)
		{
			return Tone.Ok;
		}
		if (color == Tok.Bad)
		{
			return Tone.Bad;
		}
		if (color == Tok.Warn)
		{
			return Tone.Warn;
		}
		if (color == Tok.Info)
		{
			return Tone.Info;
		}
		if (color == Tok.Accent)
		{
			return Tone.Accent;
		}
		if (color == Tok.Text3)
		{
			return Tone.Muted;
		}
		return Tone.Neutral;
	}

	private static bool IsLiveTone(Color color)
	{
		return color == Tok.Ok;
	}

	/// <summary>
	/// 结果提示：只对「结果性」消息弹 Toast，并且
	/// 1) 归一化后去重（文案里的数字/时长变化不算新消息）；
	/// 2) 同一类消息 90 秒内不重复；
	/// 3) 全局限流，至少间隔 4 秒。
	/// 状态栏始终显示最新消息，Toast 只做「值得一提」的提示，避免刷屏挡操作。
	/// </summary>
	/// <summary>
	/// 状态栏策略：操作结果用语义色常驻；后台环境类消息（"正在…/重试"）走安静样式、
	/// 去掉原因码尾巴、且不覆盖刚刚产生的操作结果 —— 避免一条后台重试把状态栏钉成红色告警。
	/// </summary>
	private void ApplyStatusMessage(string text, Color color)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return; // 空消息不覆盖当前状态：此前会被 StripReasonCode 变成"就绪"，顶掉真实结果
		}
		string message = StripReasonCode(text);
		bool ambient = IsAmbientMessage(text);
		if (ambient)
		{
			if ((DateTime.UtcNow - _lastUserMessageAt) < TimeSpan.FromSeconds(15))
			{
				return;
			}
			_shell.Status.SetMessage(message, Tone.Muted);
			return;
		}
		_lastUserMessageAt = DateTime.UtcNow;
		_shell.Status.SetMessage(message, ToneOf(color));
	}

	private static bool IsAmbientMessage(string text)
	{
		return text.Contains("正在", StringComparison.Ordinal) || text.Contains("重试", StringComparison.Ordinal);
	}

	/// <summary>去掉状态栏里的 "[REASON_CODE]" 尾巴（原因码仍完整写进日志）。</summary>
	private static string StripReasonCode(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "就绪";
		}
		int index = text.LastIndexOf('[');
		if (index > 0 && text.EndsWith("]", StringComparison.Ordinal))
		{
			return text.Substring(0, index).TrimEnd('\u3000', ' ');
		}
		return text;
	}

	private DateTime _lastUserMessageAt = DateTime.MinValue;

	private readonly UpdatePreferences _updatePrefs = UpdatePreferences.Load();

	private string _updatePageUrl = string.Empty;

	private UpdateCheckResult _lastUpdateResult = new UpdateCheckResult(UpdateStatus.Idle, AppIdentity.DisplayVersion, null, "尚未检查更新");

	/// <summary>启动后的静默自动检查：失败不打扰，结果只进状态栏与关于页。</summary>
	private async void AutoCheckOnStartup()
	{
		if (!_updatePrefs.AutoCheck)
		{
			return;
		}
		await Task.Delay(1200, _formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
		if (!IsDisposed && !Disposing)
		{
			await CheckForUpdatesAsync(manual: false);
		}
	}

	/// <summary>更新源仓库：偏好里可覆盖内置默认（便于换镜像/自建仓库测试）。</summary>
	private string UpdateRepository => string.IsNullOrWhiteSpace(_updatePrefs.Repository) ? AppIdentity.GitHubRepository : _updatePrefs.Repository;

	private string FeedbackUrl => string.IsNullOrWhiteSpace(AppIdentity.FeedbackUrl) ? ("https://github.com/" + UpdateRepository + "/issues") : AppIdentity.FeedbackUrl;

	/// <summary>检查更新：只展示结果与更新内容，下载交给浏览器。</summary>
	private async Task CheckForUpdatesAsync(bool manual)
	{
		_aboutPage.SetUpdateState("正在检查更新…", Tone.Info, false, string.Empty, string.Empty);
		UpdateCheckResult result = await UpdateCheckService.CheckAsync(UpdateRepository, _updatePrefs.Channel, _formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
		if (IsDisposed || Disposing)
		{
			return;
		}
		_updatePageUrl = result.Latest?.PageUrl ?? string.Empty;
		_lastUpdateResult = result;
		Tone tone = result.HasUpdate ? Tone.Accent : ((result.Status == UpdateStatus.Failed) ? Tone.Bad : ((result.Status == UpdateStatus.UpToDate) ? Tone.Ok : Tone.Muted));
		_aboutPage.SetUpdateState(result.Message, tone, result.HasUpdate, result.Latest?.Version ?? string.Empty, result.HasUpdate ? result.Latest!.Notes : string.Empty);
		if (manual || result.Status != UpdateStatus.NotConfigured)
		{
			_lastUserMessageAt = DateTime.UtcNow;
			_shell.Status.SetMessage(result.Message, tone);
		}
	}

	/// <summary>应用内更新：下载安装包 → 校验 sha256 → 退出程序并由接力脚本静默安装 → 自动重启新版本。</summary>
	internal async Task<bool> PerformInAppUpdateAsync()
	{
		UpdateCheckResult result = _lastUpdateResult;
		if (!result.HasUpdate || result.Latest == null || result.Latest.AssetUrl.Length == 0)
		{
			_lastUserMessageAt = DateTime.UtcNow;
			_shell.Status.SetMessage("没有可下载的安装包（请先检查更新）", Tone.Warn);
			return false;
		}
		string fileName = result.Latest.AssetName.Length > 0 ? result.Latest.AssetName : ("PocketDeck-" + result.Latest.Version + "-setup.exe");
		string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketDeck", "updates", fileName);
		try
		{
			_aboutPage.SetUpdateState("正在下载更新… 0%", Tone.Info, true, result.Latest.Version, result.Latest.Notes);
			Progress<double> progress = new Progress<double>(delegate(double ratio)
			{
				if (!IsDisposed && !Disposing)
				{
					_aboutPage.SetUpdateState($"正在下载更新… {(int)Math.Round(ratio * 100.0)}%", Tone.Info, true, result.Latest.Version, result.Latest.Notes);
				}
			});
			bool ok = await UpdateCheckService.DownloadAsync(result.Latest.AssetUrl, target, result.Latest.AssetSha256, progress, _formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
			if (IsDisposed || Disposing)
			{
				return false;
			}
			if (!ok)
			{
				_lastUserMessageAt = DateTime.UtcNow;
				_shell.Status.SetMessage("更新包校验失败（sha256 不匹配），已丢弃", Tone.Bad);
				_aboutPage.SetUpdateState("下载校验失败，请重试", Tone.Bad, true, result.Latest.Version, result.Latest.Notes);
				return false;
			}
			_lastUserMessageAt = DateTime.UtcNow;
			_shell.Status.SetMessage("更新包已下载并校验通过，正在安装…", Tone.Ok);
			_aboutPage.SetUpdateState("正在安装并重启…", Tone.Ok, true, result.Latest.Version, result.Latest.Notes);
			StartDeferredInstall(target);
			Close();
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			_lastUserMessageAt = DateTime.UtcNow;
			_shell.Status.SetMessage("更新失败：" + ex.Message, Tone.Bad);
			_aboutPage.SetUpdateState("更新失败：" + ex.Message, Tone.Bad, true, result.Latest.Version, result.Latest.Notes);
			return false;
		}
	}

	/// <summary>
	/// 接力脚本：等本进程退出 → 静默安装到当前目录（就地更新，便携版与安装版都适用）→ 重新启动 → 自删。
	/// 运行中的 exe 无法被覆盖，所以必须先退出，由脚本接手。
	/// </summary>
	private void StartDeferredInstall(string installerPath)
	{
		string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
		string scriptPath = Path.Combine(Path.GetTempPath(), "pocketdeck-update.cmd");
		string script = string.Join("\r\n", new string[]
		{
			"@echo off",
			"ping -n 3 127.0.0.1 >nul",
			"\"" + installerPath + "\" --silent --install-dir \"" + baseDir + "\"",
			"start \"\" \"" + Path.Combine(baseDir, AppIdentity.ExecutableName) + "\"",
			"del \"%~f0\"",
			string.Empty,
		});
		File.WriteAllText(scriptPath, script, System.Text.Encoding.Default);
		Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		});
	}

	private void OpenExternalUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			NotifyOutcome("没有可打开的链接", Tone.Warn);
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			NotifyOutcome("打开链接失败：" + ex.Message, Tone.Warn);
		}
	}

	/// <summary>用系统默认程序打开文件或目录（用于查看许可全文 / 第三方许可目录）。</summary>
	private void OpenLocalPath(string path)
	{
		try
		{
			if (!File.Exists(path) && !Directory.Exists(path))
			{
				NotifyOutcome("找不到：" + Path.GetFileName(path), Tone.Warn);
				return;
			}
			Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			NotifyOutcome("打开失败：" + ex.Message, Tone.Warn);
		}
	}

	private void NotifyOutcome(string text, Tone tone)
	{
		if (string.IsNullOrWhiteSpace(text) || text.Length < 6)
		{
			return;
		}
		if (tone != Tone.Ok && tone != Tone.Bad)
		{
			return;
		}
		if (text.Contains("正在", StringComparison.Ordinal) || text.Contains("重试", StringComparison.Ordinal))
		{
			return; // 后台进度/重试类消息只进状态栏，不弹 Toast
		}
		if (!text.Contains("完成", StringComparison.Ordinal) && !text.Contains("成功", StringComparison.Ordinal)
			&& !text.Contains("正常", StringComparison.Ordinal) && !text.Contains("就绪", StringComparison.Ordinal)
			&& !text.Contains("失败", StringComparison.Ordinal) && !text.Contains("错误", StringComparison.Ordinal)
			&& !text.Contains("不可用", StringComparison.Ordinal) && !text.Contains("无效", StringComparison.Ordinal))
		{
			return;
		}
		string key = NormalizeToastKey(text);
		DateTime now = DateTime.UtcNow;
		if (now - _lastToastAt < TimeSpan.FromSeconds(4))
		{
			return;
		}
		if (_recentToasts.TryGetValue(key, out DateTime seen) && (now - seen) < TimeSpan.FromSeconds(90))
		{
			return;
		}
		_recentToasts[key] = now;
		if (_recentToasts.Count > 24)
		{
			_recentToasts.Clear();
			_recentToasts[key] = now;
		}
		_lastToastAt = now;
		_shell.Toast(tone == Tone.Ok ? "操作完成" : "操作失败", text, tone, tone == Tone.Ok ? Icon3.Check : Icon3.Info);
	}

	/// <summary>去掉数字/空白/标点，只留语义骨架，用于判断"是不是同一条消息"。</summary>
	private static string NormalizeToastKey(string text)
	{
		System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (char.IsDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
			{
				continue;
			}
			builder.Append(c);
		}
		return builder.ToString();
	}

	private readonly Dictionary<string, DateTime> _recentToasts = new Dictionary<string, DateTime>(StringComparer.Ordinal);

	private DateTime _lastToastAt = DateTime.MinValue;

	private Size ScaledSize(int logicalWidth, int logicalHeight)
	{
		return new Size((int)MathF.Round(logicalWidth * _displayScale), (int)MathF.Round(logicalHeight * _displayScale));
	}

	private void ApplyDisplayScale(float scale)
	{
		if (_applyingScale || _shell == null)
		{
			return;
		}
		_applyingScale = true;
		try
		{
			_displayScale = Math.Max(1f, scale);
			MinimumSize = ScaledSize(Tok.MinWidth, Tok.MinHeight);
			_shell.ApplyScale(_displayScale);
			_shell.Size = ClientSize;
			_shell.Relayout();
			Invalidate();
		}
		finally
		{
			_applyingScale = false;
		}
	}

	// ============================ 无线连接 ============================

	/// <summary>
	/// 自动开通 / 自动重连无线（不需要手动点按钮）：
	/// 已有网络设备 → 什么都不做；有 USB 且曾用过无线 → 自动 tcpip 开通；否则按存档端点直连。
	/// 退避：45 秒一次，连续 3 次失败即停手，等设备状态变化再重置。
	/// </summary>
	private async void AutoConnectWirelessAsync()
	{
		AndroidConnectionSnapshot snapshot = _phone.Snapshot;
		if (_autoWirelessInFlight || _operationInProgress)
		{
			return;
		}
		if (snapshot.Devices.Any((AndroidDeviceView device) => device.Transport == AndroidTransport.Network && device.Status == AndroidDeviceStatus.Ready))
		{
			_autoWirelessFailures = 0;
			return;
		}
		bool hasReadyUsb = snapshot.Devices.Any((AndroidDeviceView device) => device.Transport == AndroidTransport.Usb && device.Status == AndroidDeviceStatus.Ready);
		string endpoint = LoadPersistedWirelessEndpoint();
		bool enableViaUsb = hasReadyUsb && endpoint.Length > 0;
		if (!enableViaUsb && endpoint.Length == 0)
		{
			return;
		}
		if (_autoWirelessFailures >= 3 || DateTime.UtcNow - _lastAutoWirelessAttempt < TimeSpan.FromSeconds(45))
		{
			return;
		}
		_lastAutoWirelessAttempt = DateTime.UtcNow;
		_autoWirelessInFlight = true;
		try
		{
			SetWirelessOperationInProgress(inProgress: true);
			AndroidWirelessConnectResult result;
			if (enableViaUsb)
			{
				SetControlText(_wifiDetail, "正在自动开通 WiFi 无线…");
				result = await _phone.EnableWirelessViaUsbAsync(_formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
			}
			else
			{
				SetControlText(_wifiDetail, "正在自动连接 " + endpoint + " …");
				result = await _phone.ConnectWirelessAsync(endpoint, _formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
			}
			if (IsDisposed || Disposing)
			{
				return;
			}
			_autoWirelessFailures = (result.Succeeded ? 0 : (_autoWirelessFailures + 1));
			SetControlText(_wifiDetail, result.Message);
			if (result.Succeeded && !string.IsNullOrEmpty(result.Endpoint))
			{
				PersistWirelessEndpoint(result.Endpoint);
			}
		}
		catch (OperationCanceledException) when (_formLifetime.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_autoWirelessFailures++;
			if (!IsDisposed && !Disposing)
			{
				SetControlText(_wifiDetail, "无线自动连接失败：" + ex.Message);
			}
		}
		finally
		{
			_autoWirelessInFlight = false;
			if (!IsDisposed && !Disposing)
			{
				SetWirelessOperationInProgress(inProgress: false);
			}
		}
	}

	private bool _autoWirelessInFlight;

	private int _autoWirelessFailures;

	private bool _hadReadyUsb;

	private DateTime _lastAutoWirelessAttempt = DateTime.MinValue;

	private void SetWirelessOperationInProgress(bool inProgress)
	{
		_operationInProgress = inProgress;
		UpdatePhoneOverlayButton();
		UpdateVideoProbeButton();
		if (!inProgress)
		{
			UpdateWirelessDetail(_phone.Snapshot);
		}
	}

	/// <summary>按真实链路状态刷新 WiFi 行文案，避免「行里写着已连接、主按钮却是灰的」这类自相矛盾。</summary>
	/// <summary>WiFi 行按真实链路状态显示，并在需要时自动发起连接。</summary>
	private void UpdateWirelessDetail(AndroidConnectionSnapshot snapshot)
	{
		bool hasReadyNetwork = snapshot.Devices.Any((AndroidDeviceView device) => device.Transport == AndroidTransport.Network && device.Status == AndroidDeviceStatus.Ready);
		bool hasReadyUsb = snapshot.Devices.Any((AndroidDeviceView device) => device.Transport == AndroidTransport.Usb && device.Status == AndroidDeviceStatus.Ready);
		string savedEndpoint = LoadPersistedWirelessEndpoint();
		if (hasReadyUsb && !_hadReadyUsb)
		{
			_autoWirelessFailures = 0;
		}
		_hadReadyUsb = hasReadyUsb;

		// ADB 行只认有线链路；WiFi 行只认无线链路；手机行才是"手机总体状态"。
		if (hasReadyUsb)
		{
			_home.SetAdbStatus("已连接", Tone.Ok, true);
		}
		else if (hasReadyNetwork)
		{
			_home.SetAdbStatus("未连接", Tone.Muted, false);
		}
		else
		{
			_home.SetAdbStatus("等待设备", Tone.Info, false);
		}

		// ADB 行的说明也写清"当前走的是哪条链路"，让这一行自解释、一眼可验证。
		if (!_operationInProgress)
		{
			if (hasReadyUsb)
			{
				SetControlText(_adbDetail, snapshot.Message);
			}
			else if (hasReadyNetwork)
			{
				SetControlText(_adbDetail, "未使用数据线，当前通过 WiFi 无线链路连接");
			}
			else if (snapshot.Devices.Count > 0)
			{
				SetControlText(_adbDetail, "已发现手机，等待建立可用链路");
			}
			else
			{
				SetControlText(_adbDetail, "等待 USB 数据线连接（也可自动重连 WiFi）");
			}
		}

		if (hasReadyNetwork)
		{
			_home.SetWifiStatus("已连接", Tone.Ok);
		}
		else if (hasReadyUsb)
		{
			_home.SetWifiStatus((savedEndpoint.Length == 0) ? "未启用" : "自动开通中", (savedEndpoint.Length == 0) ? Tone.Muted : Tone.Warn);
		}
		else if (savedEndpoint.Length > 0)
		{
			_home.SetWifiStatus("自动重连中", Tone.Warn);
		}
		else
		{
			_home.SetWifiStatus("未开启", Tone.Muted);
		}

		if (!_operationInProgress)
		{
			if (hasReadyNetwork)
			{
				SetControlText(_wifiDetail, "WiFi 无线链路已连接，可直接开启手机浮窗");
			}
			else if (hasReadyUsb)
			{
				SetControlText(_wifiDetail, (savedEndpoint.Length == 0) ? "插着数据线即可，程序会自动开通无线" : "已通过 USB 连接，正在自动开通 WiFi 无线…");
			}
			else if (savedEndpoint.Length > 0)
			{
				SetControlText(_wifiDetail, "上次端点 " + savedEndpoint + "，正在自动重连…");
			}
			else
			{
				SetControlText(_wifiDetail, "首次用 USB 连接后会自动开通");
			}
		}

		if (!hasReadyNetwork)
		{
			AutoConnectWirelessAsync();
		}
	}

	private static string WirelessEndpointFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketDeck", "wireless-endpoint.txt");

	private static string LoadPersistedWirelessEndpoint()
	{
		try
		{
			if (File.Exists(WirelessEndpointFilePath))
			{
				return File.ReadAllText(WirelessEndpointFilePath).Trim() ?? string.Empty;
			}
		}
		catch (Exception)
		{
		}
		return string.Empty;
	}

	private static void PersistWirelessEndpoint(string endpoint)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(WirelessEndpointFilePath));
			string tempPath = WirelessEndpointFilePath + ".tmp";
			File.WriteAllText(tempPath, endpoint);
			File.Move(tempPath, WirelessEndpointFilePath, overwrite: true);
		}
		catch (Exception)
		{
		}
	}

	// ============================ 状态刷新 ============================

	private string FormatRuntimeStatus()
	{
		return $"运行 {_runtime.Snapshot.State} · {_runtime.Snapshot.Reason.Code}";
	}

	private void OnRuntimeStateChanged(object? sender, AppRuntimeStateChangedEventArgs eventArgs)
	{
		if (InvokeRequired)
		{
			BeginInvoke(() => OnRuntimeStateChanged(sender, eventArgs));
			return;
		}
		SetControlText(_runtimeStatus, FormatRuntimeStatus());
	}

	private void OnPhoneStateChanged(object? sender, AndroidConnectionChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPhoneStateChanged(sender, eventArgs));
			return;
		}
		UpdatePhoneView(eventArgs.Snapshot);
	}

	private void UpdatePhoneView(AndroidConnectionSnapshot snapshot)
	{
		SetControlText(_phoneStatus, ConnectionStateName(snapshot.State));
		SetControlForeColor(_phoneStatus, ConnectionStateColor(snapshot.State));
		SetControlText(_adbDetail, snapshot.Message);
		// 顶栏实时状态胶囊
		AndroidDeviceDetails headerDevice = snapshot.SelectedDevice;
		string headerText = ConnectionStateName(snapshot.State);
		if (headerDevice != null && !string.IsNullOrWhiteSpace(headerDevice.Model))
		{
			headerText = headerText + " · " + headerDevice.Model.Trim();
		}
		_shell.SetHeaderStatus(headerText, ToneOf(ConnectionStateColor(snapshot.State)), snapshot.State == AndroidConnectionState.Ready);
		AndroidDeviceDetails selectedDevice = snapshot.SelectedDevice;
		SetControlText(_phoneDetails, ((object)selectedDevice == null) ? $"已发现 {snapshot.Devices.Count} 台设备；等待选择可用手机" : ($"{selectedDevice.Manufacturer} {selectedDevice.Model} · Android {selectedDevice.AndroidVersion}\n" + FormatDisplaySize(selectedDevice)));

		string selectedKey = snapshot.Devices.FirstOrDefault((AndroidDeviceView device) => device.IsSelected)?.DeviceKey;
		_updatingDeviceList = true;
		try
		{
			_deviceList.BeginUpdate();
			_deviceList.Items.Clear();
			foreach (AndroidDeviceView device in snapshot.Devices)
			{
				int index = _deviceList.Items.Add(new DeviceListItem(device));
				if (string.Equals(device.DeviceKey, selectedKey, StringComparison.Ordinal))
				{
					_deviceList.SelectedIndex = index;
				}
			}
			_deviceList.Enabled = snapshot.Devices.Count > 0;
		}
		finally
		{
			_deviceList.EndUpdate();
			_updatingDeviceList = false;
		}
		UpdateVideoProbeButton();
		UpdatePhoneOverlayButton();
		UpdateHero(OverlayButtonProgress.None, (uint)(_phoneOverlay.Snapshot.State - 1) <= 1u);
		UpdateWirelessDetail(snapshot);
		RefreshResolutionSelector();
		UpdateQualityStatus();
	}

	private static Color ConnectionStateColor(AndroidConnectionState state)
	{
		switch (state)
		{
			case AndroidConnectionState.Ready:
				return UiPalette.Success;
			case AndroidConnectionState.AuthorizationRequired:
				return UiPalette.Warning;
			case AndroidConnectionState.Discovering:
			case AndroidConnectionState.WaitingForDevice:
			case AndroidConnectionState.Connecting:
				return UiPalette.Info;
			default:
				return UiPalette.Danger;
		}
	}

	private void OnPhoneAudioStateChanged(object? sender, PhoneAudioChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPhoneAudioStateChanged(sender, eventArgs));
			return;
		}
		ApplyAudioStatus(eventArgs.Snapshot);
	}

	private void OnPhoneControlStateChanged(object? sender, PhoneControlChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPhoneControlStateChanged(sender, eventArgs));
			return;
		}
		ApplyControlStatus(eventArgs.Snapshot);
	}

	private void ApplyAudioStatus(PhoneAudioSnapshot snapshot)
	{
		SetControlText(_audioStatus, AudioStateName(snapshot.State));
		Color color;
		if (snapshot.State == PhoneAudioState.Playing)
		{
			color = UiPalette.Success;
		}
		else if (snapshot.State == PhoneAudioState.Faulted)
		{
			color = UiPalette.Danger;
		}
		else
		{
			color = ((snapshot.State == PhoneAudioState.Degraded) ? UiPalette.Warning : UiPalette.TextSecondary);
		}
		SetControlForeColor(_audioStatus, color);
	}

	private void ApplyControlStatus(PhoneControlSnapshot snapshot)
	{
		SetControlText(_controlStatus, ControlStateName(snapshot.State));
		Color color = ((snapshot.State == PhoneControlState.Ready) ? UiPalette.Success : ((snapshot.State == PhoneControlState.Faulted) ? UiPalette.Danger : UiPalette.TextSecondary));
		SetControlForeColor(_controlStatus, color);
	}

	private void OnPhoneMediaSessionStateChanged(object? sender, PhoneMediaSessionChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPhoneMediaSessionStateChanged(sender, eventArgs));
			return;
		}
		PhoneMediaSessionState state = eventArgs.Snapshot.State;
		if ((uint)(state - 4) <= 2u)
		{
			SetControlText(_operationStatus, eventArgs.Snapshot.Message);
			SetControlForeColor(_operationStatus, UiPalette.Info);
			if (_bindingRecoveryInProgress)
			{
				_settingsPage.SetBindingNotice(eventArgs.Snapshot.Message, Tone.Warn);
			}
		}
	}

	private void OnPlayspaceDragStateChanged(object? sender, OpenVrPlayspaceDragChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPlayspaceDragStateChanged(sender, eventArgs));
			return;
		}
		OpenVrPlayspaceDragSnapshot snapshot = _playspaceDrag.Snapshot;
		bool wasUpdating = _updatingSettingsControls;
		_updatingSettingsControls = true;
		try
		{
			_playspaceDragToggle.Checked = snapshot.Enabled;
			_playspaceDragMultiplierSelector.SelectValue(checked((int)snapshot.Multiplier));
			SetControlText(_playspaceDragToggle, snapshot.Enabled ? "开启" : "关闭");
			SetControlText(_playspaceStatus, !snapshot.Enabled ? "已关闭" : ((snapshot.State == OpenVrPlayspaceDragState.Ready) ? "已启用" : "启用中"));
			SetControlForeColor(_playspaceStatus, snapshot.Enabled ? UiPalette.Success : UiPalette.TextSecondary);
			UpdatePlayspaceCoordinates(snapshot);
		}
		finally
		{
			_updatingSettingsControls = wasUpdating;
		}
		UpdateOverlayDashboard(_phoneOverlay.Snapshot);
		UpdateQualityStatus();
		if (snapshot.State == OpenVrPlayspaceDragState.Faulted)
		{
			SetControlText(_operationStatus, snapshot.Message + "\u3000[" + snapshot.ReasonCode + "]");
			SetControlForeColor(_operationStatus, UiPalette.Danger);
		}
	}

	private async void OnPhoneOverlayStateChanged(object? sender, PhoneOverlayChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnPhoneOverlayStateChanged(sender, eventArgs));
			return;
		}
		UpdatePhoneOverlayButton();
		UpdateVideoProbeButton();
		UpdateSteamVrBindingNotice(eventArgs.Snapshot);
		UpdateOverlayDashboard(eventArgs.Snapshot);
		if (eventArgs.Snapshot.Width > 0 && eventArgs.Snapshot.Height > 0 && _resolutionSelector.TryGetSelectedValue(out VideoResolutionProfile profile) && (profile.ExpectedWidth > profile.ExpectedHeight) != (eventArgs.Snapshot.Width > eventArgs.Snapshot.Height))
		{
			RefreshResolutionSelector();
		}
		UpdateQualityStatus();

		PhoneOverlayState state = eventArgs.Snapshot.State;
		if (state == PhoneOverlayState.Running || state == PhoneOverlayState.Faulted)
		{
			SetControlText(_operationStatus, FormatPhoneOverlayStatus(eventArgs.Snapshot));
			SetControlForeColor(_operationStatus, (state == PhoneOverlayState.Running) ? UiPalette.Success : UiPalette.Danger);
		}

		if (state == PhoneOverlayState.Running)
		{
			PhoneControlState controlState = _phoneControl.Snapshot.State;
			if ((controlState == PhoneControlState.Stopped || controlState == PhoneControlState.Faulted) && DateTimeOffset.UtcNow >= _nextPhoneControlStartAt)
			{
				_nextPhoneControlStartAt = DateTimeOffset.UtcNow.AddSeconds(3.0);
				await StartPhoneControlAsync().ConfigureAwait(continueOnCapturedContext: true);
			}
			return;
		}

		if ((state == PhoneOverlayState.Stopped || state == PhoneOverlayState.Faulted) && (uint)(_phoneControl.Snapshot.State - 1) <= 1u)
		{
			try
			{
				await _phoneControl.StopAsync(_formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
			}
			catch (OperationCanceledException) when (_formLifetime.IsCancellationRequested)
			{
			}
			_nextPhoneControlStartAt = DateTimeOffset.MinValue;
		}
	}

	private async Task StartPhoneControlAsync()
	{
		try
		{
			await _phoneControl.StartAsync(_phone.Snapshot.SelectedDevice?.DeviceKey, _formLifetime.Token).ConfigureAwait(continueOnCapturedContext: true);
		}
		catch (OperationCanceledException) when (_formLifetime.IsCancellationRequested)
		{
		}
		catch (PhoneControlServiceException ex)
		{
			_operationStatus.Text = ex.Message + "\u3000[" + ex.ReasonCode + "]";
			_operationStatus.ForeColor = UiPalette.Danger;
		}
	}

	private void OnSettingsChanged(object? sender, AppSettingsChangedEventArgs eventArgs)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			BeginInvoke(() => OnSettingsChanged(sender, eventArgs));
			return;
		}
		AppSettings persisted = eventArgs.Snapshot.Value;
		_pendingSettings = (_settingsDirty
			? _pendingSettings with
			{
				KeepAwakeWhileGrabbed = persisted.KeepAwakeWhileGrabbed,
				VrUnlockKeypadEnabled = persisted.VrUnlockKeypadEnabled
			}
			: persisted);
		_settingsDirty = HasPendingApplyChanges(_pendingSettings, persisted);
		_phoneOverlay.ConfigureLockScreenFeatures(persisted.KeepAwakeWhileGrabbed, persisted.VrUnlockKeypadEnabled);
		RefreshSettingsControls();
	}

	// ============================ 设置控件 ============================

	private void RefreshSettingsControls()
	{
		bool wasUpdating = _updatingSettingsControls;
		_updatingSettingsControls = true;
		try
		{
			AppSettings pending = _pendingSettings;
			_controllerHandSelector.SelectValue(pending.ControllerHand);
			_bitrateSelector.SelectValue(pending.VideoBitrateMbps);
			_frameRateSelector.SelectValue(pending.VideoMaximumFramesPerSecond);
			_keepAwakeWhileGrabbedToggle.Checked = pending.KeepAwakeWhileGrabbed;
			_keepAwakeWhileGrabbedToggle.Text = (pending.KeepAwakeWhileGrabbed ? "开启" : "关闭");
			_vrUnlockKeypadToggle.Checked = pending.VrUnlockKeypadEnabled;
			_vrUnlockKeypadToggle.Text = (pending.VrUnlockKeypadEnabled ? "开启" : "关闭");
			_playspaceDragToggle.Checked = _playspaceDrag.Snapshot.Enabled;
			_playspaceDragToggle.Text = (_playspaceDragToggle.Checked ? "开启" : "关闭");
			RefreshResolutionSelector();
			UpdateQualityStatus();
			UpdateSettingsActionButtons();
		}
		finally
		{
			_updatingSettingsControls = wasUpdating;
		}
	}

	private void RefreshResolutionSelector()
	{
		bool wasUpdating = _updatingSettingsControls;
		_updatingSettingsControls = true;
		try
		{
			int percent = _pendingSettings.VideoResolutionPercent;
			(int nativeWidth, int nativeHeight) = GetNativeDisplayDimensions();
			if (nativeWidth < 1 || nativeHeight < 1)
			{
				VideoResolutionProfile placeholder = new VideoResolutionProfile(percent, 1, 1, 0, IsApproximate: false);
				_resolutionSelector.SetNodes(new FixedChoiceNode3<VideoResolutionProfile>[1]
				{
					new FixedChoiceNode3<VideoResolutionProfile>(placeholder, "等待手机")
				}, placeholder);
				_resolutionSelector.Enabled = false;
				return;
			}
			IReadOnlyList<VideoResolutionProfile> profiles = VideoResolutionProfiles.Create(nativeWidth, nativeHeight);
			VideoResolutionProfile selected = VideoResolutionProfiles.Resolve(nativeWidth, nativeHeight, percent);
			_resolutionSelector.SetNodes(
				from profile in profiles
				orderby profile.Percent
				select new FixedChoiceNode3<VideoResolutionProfile>(profile, FormatResolutionChoice(profile)),
				profiles.First((VideoResolutionProfile profile) => profile.Percent == selected.Percent));
			_resolutionSelector.Enabled = SettingsControlPolicy.CanUseResolutionSelector(_operationInProgress, nativeWidth, nativeHeight);
		}
		finally
		{
			_updatingSettingsControls = wasUpdating;
		}
	}

	private void UpdateQualityStatus()
	{
		AppSettings pending = _pendingSettings;
		PhoneOverlaySnapshot snapshot = _phoneOverlay.Snapshot;
		if (snapshot.State == PhoneOverlayState.Running)
		{
			SetControlText(_qualityStatus, $"实际 {snapshot.Width}×{snapshot.Height} · {snapshot.FramesPerSecond:F1} FPS · {snapshot.AverageBitrateMbps:F2} Mbps" + (_settingsDirty ? " · 待应用" : string.Empty));
		}
		else
		{
			SetControlText(_qualityStatus, $"{pending.VideoResolutionPercent}% · {pending.VideoBitrateMbps} Mbps · {pending.VideoMaximumFramesPerSecond} FPS · " + (_settingsDirty ? "待应用" : "已保存"));
		}
	}

	private void UpdateSettingsActionButtons()
	{
		bool enabled = SettingsControlPolicy.CanUsePendingSettingsActions(_operationInProgress, _immediateSettingsInProgress, _settingsDirty);
		_applySettingsButton.Enabled = enabled;
		_discardSettingsButton.Enabled = enabled;
	}

	private static bool HasPendingApplyChanges(AppSettings pending, AppSettings persisted)
	{
		if (pending.ControllerHand == persisted.ControllerHand && pending.VideoResolutionPercent == persisted.VideoResolutionPercent && pending.VideoBitrateMbps == persisted.VideoBitrateMbps && pending.VideoMaximumFramesPerSecond == persisted.VideoMaximumFramesPerSecond)
		{
			return pending.UpdateChannel != persisted.UpdateChannel;
		}
		return true;
	}

	// ============================ 按钮可用性 ============================

	private void UpdatePhoneOverlayButton()
	{
		PhoneOverlayState state = _phoneOverlay.Snapshot.State;
		bool running = (uint)(state - 1) <= 1u;
		OverlayButtonProgress progress = ((_overlayButtonProgress == OverlayButtonProgress.None)
			? (state switch
			{
				PhoneOverlayState.Starting => OverlayButtonProgress.Opening,
				PhoneOverlayState.Stopping => OverlayButtonProgress.Closing,
				_ => OverlayButtonProgress.None
			})
			: _overlayButtonProgress);

		_steamVrBindingsButton.Enabled = !_operationInProgress;
		_playspaceDragToggle.Enabled = !_operationInProgress;

		if (progress != OverlayButtonProgress.None)
		{
			SetControlText(_phoneOverlayButton, (progress == OverlayButtonProgress.Opening) ? "正在打开" : "正在关闭");
			_phoneOverlayButton.Icon = Icon3.Dots;
			_phoneOverlayButton.Variant = Button3.Look.Soft;
			_phoneOverlayButton.Enabled = false;
			return;
		}
		SetControlText(_phoneOverlayButton, running ? "关闭手机浮窗" : "开启手机浮窗");
		UpdateHero(progress, running);
		_phoneOverlayButton.Icon = (running ? Icon3.Pause : Icon3.Play);
		_phoneOverlayButton.Variant = (running ? Button3.Look.Danger : Button3.Look.Ok);
		_phoneOverlayButton.Enabled = !_operationInProgress && (running || _phone.Snapshot.State == AndroidConnectionState.Ready);
	}

	/// <summary>英雄区文案：跟随浮窗状态与手机信息（此前只喂了快照，实机一直显示"未运行"）。</summary>
	private void UpdateHero(OverlayButtonProgress progress, bool running)
	{
		string title = progress switch
		{
			OverlayButtonProgress.Opening => "正在打开手机浮窗",
			OverlayButtonProgress.Closing => "正在关闭手机浮窗",
			_ => running ? "手机浮窗运行中" : "手机浮窗未运行",
		};
		AndroidConnectionSnapshot phone = _phone.Snapshot;
		AndroidDeviceDetails device = phone.SelectedDevice;
		string subtitle;
		if (device != null && !string.IsNullOrWhiteSpace(device.Model))
		{
			subtitle = device.Manufacturer + " " + device.Model.Trim() + " · " + AndroidDisplayNames.Transport(device.Transport);
		}
		else
		{
			subtitle = running ? "手机画面已提交到 SteamVR" : "连接手机后即可把屏幕搬进 SteamVR";
		}
		_home.SetHero(title, subtitle);
	}

	private void UpdateVideoProbeButton()
	{
		PhoneOverlayState state = _phoneOverlay.Snapshot.State;
		bool overlayIdle = state == PhoneOverlayState.Stopped || state == PhoneOverlayState.Faulted;
		_videoProbeButton.Enabled = !_operationInProgress && overlayIdle && _phone.Snapshot.State == AndroidConnectionState.Ready;
	}

	private void UpdateDashboardSnapshots()
	{
		ApplyAudioStatus(_phoneAudio.Snapshot);
		ApplyControlStatus(_phoneControl.Snapshot);
		OpenVrPlayspaceDragSnapshot playspace = _playspaceDrag.Snapshot;
		SetControlText(_playspaceStatus, playspace.Enabled ? "已启用" : "已关闭");
		SetControlForeColor(_playspaceStatus, playspace.Enabled ? UiPalette.Success : UiPalette.TextSecondary);
		UpdatePlayspaceCoordinates(playspace);
		UpdateOverlayDashboard(_phoneOverlay.Snapshot);
	}

	private void UpdatePlayspaceCoordinates(OpenVrPlayspaceDragSnapshot snapshot)
	{
		OpenVrPlayspaceDragState state = snapshot.State;
		if (state == OpenVrPlayspaceDragState.Stopped || state == OpenVrPlayspaceDragState.Faulted)
		{
			SetControlText(_playspaceCoordinates, "X --     Y --     Z --");
			return;
		}
		SetControlText(_playspaceCoordinates, $"X {snapshot.OffsetX:0.00}  Y {snapshot.OffsetY:0.00}  Z {snapshot.OffsetZ:0.00} m");
	}

	private void UpdateOverlayDashboard(PhoneOverlaySnapshot snapshot)
	{
		SetControlText(_videoStatus, snapshot.State switch
		{
			PhoneOverlayState.Running => "已就绪",
			PhoneOverlayState.Starting => "连接中",
			PhoneOverlayState.Stopping => "关闭中",
			PhoneOverlayState.Faulted => "异常",
			_ => "未运行"
		});
		SetControlForeColor(_videoStatus, (snapshot.State == PhoneOverlayState.Running) ? UiPalette.Success : ((snapshot.State == PhoneOverlayState.Faulted) ? UiPalette.Danger : UiPalette.TextSecondary));

		string steamVrText = snapshot.State switch
		{
			PhoneOverlayState.Running => snapshot.WorldAnchored ? "已就绪" : "等待定位",
			PhoneOverlayState.Starting => "连接中",
			PhoneOverlayState.Faulted => "连接异常",
			_ => (_playspaceDrag.Snapshot.State != OpenVrPlayspaceDragState.Ready) ? "等待连接" : "已连接"
		};
		SetControlText(_steamVrStatus, steamVrText);
		bool steamVrOk = steamVrText == "已就绪" || steamVrText == "已连接";
		SetControlForeColor(_steamVrStatus, steamVrOk ? UiPalette.Success : ((snapshot.State == PhoneOverlayState.Faulted) ? UiPalette.Danger : UiPalette.TextSecondary));

		SetControlText(_metricResolution, (snapshot.Width > 0 && snapshot.Height > 0) ? $"{snapshot.Width}×{snapshot.Height}" : "--");
		SetControlText(_metricBitrate, (snapshot.AverageBitrateMbps > 0.0) ? $"{snapshot.AverageBitrateMbps:F2} Mbps" : "--");
		SetControlText(_metricFrameRate, (snapshot.FramesPerSecond > 0.0) ? $"{snapshot.FramesPerSecond:F1} FPS" : "--");
		SetControlText(_metricLatency, (snapshot.AverageLatencyMilliseconds > 0.0) ? $"{snapshot.AverageLatencyMilliseconds:F0} ms" : "--");
	}

	private void UpdateSteamVrBindingNotice(PhoneOverlaySnapshot snapshot)
	{
		if (!_bindingRecoveryInProgress)
		{
			_settingsPage.SetBindingNotice(snapshot.ShowBindingNotice ? snapshot.BindingMessage : string.Empty, snapshot.BindingState == OpenVrBindingHealthState.Failed ? Tone.Bad : Tone.Warn);
		}
	}

	// ============================ 交互处理 ============================

	private async void OnRefreshClicked(object? sender, EventArgs eventArgs)
	{
		await RunUiOperationAsync("正在重新扫描手机…", (CancellationToken token) => _phone.RefreshAsync(token).AsTask()).ConfigureAwait(continueOnCapturedContext: true);
	}

	private async void OnDeviceSelected(object? sender, EventArgs eventArgs)
	{
		if (_updatingDeviceList)
		{
			return;
		}
		if (_deviceList.SelectedItem is DeviceListItem item)
		{
			await RunUiOperationAsync("正在切换到 " + item.Device.DisplayName + "…", (CancellationToken token) => _phone.SelectDeviceAsync(item.Device.DeviceKey, token).AsTask()).ConfigureAwait(continueOnCapturedContext: true);
		}
	}

	private async void OnVideoProbeClicked(object? sender, EventArgs eventArgs)
	{
		await RunUiOperationAsync("正在检查手机视频、解码和显卡纹理…", RunVideoProbeAsync).ConfigureAwait(continueOnCapturedContext: true);
	}

	private async void OnPhoneOverlayClicked(object? sender, EventArgs eventArgs)
	{
		PhoneOverlayState state = _phoneOverlay.Snapshot.State;
		bool running = (uint)(state - 1) <= 1u;
		_overlayButtonProgress = (running ? OverlayButtonProgress.Closing : OverlayButtonProgress.Opening);
		try
		{
			if (running)
			{
				await RunUiOperationAsync("正在关闭 SteamVR 手机浮窗…", StopPhoneOverlayAsync).ConfigureAwait(continueOnCapturedContext: true);
				return;
			}
			await RunUiOperationAsync("正在打开 SteamVR 手机浮窗…", (CancellationToken token) => StartPhoneOverlayAsync(_phone.Snapshot.SelectedDevice?.DeviceKey, token)).ConfigureAwait(continueOnCapturedContext: true);
		}
		finally
		{
			_overlayButtonProgress = OverlayButtonProgress.None;
			if (!IsDisposed && !Disposing)
			{
				UpdatePhoneOverlayButton();
			}
		}
	}

	private void OnSteamVrBindingsClicked(object? sender, EventArgs eventArgs)
	{
		OpenVrBindingResult result = _phoneOverlay.OpenBindingUi();
		_operationStatus.Text = result.Message + "\u3000[" + result.ReasonCode + "]";
		_operationStatus.ForeColor = (result.Succeeded ? UiPalette.Success : UiPalette.Danger);
	}

	private void OnSettingChoiceChanged(object? sender, EventArgs eventArgs)
	{
		if (_updatingSettingsControls)
		{
			return;
		}
		if (_controllerHandSelector.TryGetSelectedValue(out ControllerHandPreference hand) && _resolutionSelector.TryGetSelectedValue(out VideoResolutionProfile resolution) && _bitrateSelector.TryGetSelectedValue(out int bitrate) && _frameRateSelector.TryGetSelectedValue(out int frameRate))
		{
			_pendingSettings = _pendingSettings with
			{
				ControllerHand = hand,
				VideoResolutionPercent = resolution.Percent,
				VideoBitrateMbps = bitrate,
				VideoMaximumFramesPerSecond = frameRate
			};
			_settingsDirty = HasPendingApplyChanges(_pendingSettings, _settings.Snapshot.Value);
			UpdateQualityStatus();
			UpdateSettingsActionButtons();
		}
	}

	private async void OnImmediateLockScreenSettingChanged(object? sender, EventArgs eventArgs)
	{
		if (_updatingSettingsControls || _immediateSettingsInProgress)
		{
			return;
		}
		bool keepAwakeWhileGrabbed = _keepAwakeWhileGrabbedToggle.Checked;
		bool vrUnlockKeypadEnabled = _vrUnlockKeypadToggle.Checked;
		await RunUiOperationAsync("正在保存即时设置…", async (CancellationToken token) =>
		{
			AppSettings previous = _settings.Snapshot.Value;
			AppSettings candidate = previous with
			{
				KeepAwakeWhileGrabbed = keepAwakeWhileGrabbed,
				VrUnlockKeypadEnabled = vrUnlockKeypadEnabled
			};
			_phoneOverlay.ConfigureLockScreenFeatures(keepAwakeWhileGrabbed, vrUnlockKeypadEnabled);
			try
			{
				if (candidate != previous)
				{
					await _settings.SaveAsync(candidate, token);
				}
				_operationStatus.Text = "即时设置已生效";
				_operationStatus.ForeColor = UiPalette.Success;
			}
			catch
			{
				_phoneOverlay.ConfigureLockScreenFeatures(previous.KeepAwakeWhileGrabbed, previous.VrUnlockKeypadEnabled);
				_pendingSettings = _pendingSettings with
				{
					KeepAwakeWhileGrabbed = previous.KeepAwakeWhileGrabbed,
					VrUnlockKeypadEnabled = previous.VrUnlockKeypadEnabled
				};
				RefreshSettingsControls();
				throw;
			}
		}, immediateControlsOnly: true).ConfigureAwait(continueOnCapturedContext: true);
	}

	private void OnPlayspaceDragMultiplierChanged(object? sender, EventArgs eventArgs)
	{
		if (_updatingSettingsControls || !_playspaceDragMultiplierSelector.TryGetSelectedValue(out int multiplier))
		{
			return;
		}
		_playspaceDrag.SetMultiplier(multiplier);
		_operationStatus.Text = $"空间拖拽倍率已立即切换为 {multiplier}x（本次运行有效）";
		_operationStatus.ForeColor = UiPalette.Success;
		UpdateQualityStatus();
	}

	private void OnPlayspaceDragChanged(object? sender, EventArgs eventArgs)
	{
		if (_updatingSettingsControls)
		{
			return;
		}
		bool enabled = _playspaceDragToggle.Checked;
		try
		{
			_playspaceDrag.SetEnabled(enabled);
			_operationStatus.Text = enabled ? "空间拖拽已立即开启（本次运行有效）" : "空间拖拽已立即关闭并恢复空间（本次运行有效）";
			_operationStatus.ForeColor = UiPalette.Success;
		}
		catch (InvalidOperationException)
		{
			_operationStatus.Text = "空间拖拽开关切换失败，请重启程序后重试";
			_operationStatus.ForeColor = UiPalette.Danger;
		}
	}

	private async void OnApplySettingsClicked(object? sender, EventArgs eventArgs)
	{
		await RunUiOperationAsync("正在统一应用设置…", ApplyPendingSettingsAsync).ConfigureAwait(continueOnCapturedContext: true);
	}

	private void OnDiscardSettingsClicked(object? sender, EventArgs eventArgs)
	{
		_pendingSettings = _settings.Snapshot.Value;
		_settingsDirty = false;
		RefreshSettingsControls();
		_operationStatus.Text = "已撤销未应用的设置";
		_operationStatus.ForeColor = UiPalette.TextSecondary;
	}

	private async void OnReloadLocalBindingRequested(object? sender, EventArgs eventArgs)
	{
		string deviceKey = _phone.Snapshot.SelectedDevice?.DeviceKey;
		_bindingRecoveryInProgress = true;
		try
		{
			await RunUiOperationAsync("正在准备本地手柄绑定…", async (CancellationToken token) =>
			{
				await _mediaSession.RestartScreenAsync(deviceKey, BuildVideoOptions(_settings.Snapshot.Value), (CancellationToken _) => ReloadLocalBindingAsync(), token);
				_operationStatus.Text = "本地绑定已加载，手机浮窗已重新打开一次";
				_operationStatus.ForeColor = UiPalette.Success;
			}).ConfigureAwait(continueOnCapturedContext: true);
		}
		finally
		{
			_bindingRecoveryInProgress = false;
			if (!IsDisposed && !Disposing)
			{
				UpdateSteamVrBindingNotice(_phoneOverlay.Snapshot);
			}
		}
	}

	// ============================ 业务动作 ============================

	private async Task ApplyPendingSettingsAsync(CancellationToken token)
	{
		AppSettings previous = _settings.Snapshot.Value;
		AppSettings candidate = AppSettingsPolicy.Normalize(_pendingSettings);
		if (candidate == previous)
		{
			_settingsDirty = false;
			UpdateSettingsActionButtons();
			return;
		}
		bool handChanged = candidate.ControllerHand != previous.ControllerHand;
		bool videoChanged = candidate.VideoResolutionPercent != previous.VideoResolutionPercent || candidate.VideoBitrateMbps != previous.VideoBitrateMbps || candidate.VideoMaximumFramesPerSecond != previous.VideoMaximumFramesPerSecond;
		bool requiresScreenRestart = handChanged | videoChanged;

		PhoneOverlayState state = _phoneOverlay.Snapshot.State;
		bool overlayActive = (uint)(state - 1) <= 1u;
		string deviceKey = _phone.Snapshot.SelectedDevice?.DeviceKey;
		try
		{
			if (handChanged)
			{
				_playspaceDrag.SetPhoneControllerHand(MapControllerHand(candidate.ControllerHand));
			}
			await _settings.SaveAsync(candidate, token);
			if (overlayActive & requiresScreenRestart)
			{
				await _mediaSession.RestartScreenAsync(deviceKey, BuildVideoOptions(candidate), handChanged ? (Func<CancellationToken, ValueTask>)((CancellationToken _) => ReloadLocalBindingAsync()) : null, token);
				_operationStatus.Text = "设置已统一生效，手机屏幕只重启了一次；电脑音频链路未重启";
			}
			else
			{
				_operationStatus.Text = requiresScreenRestart ? "设置已保存，将在下次打开手机浮窗时统一使用" : "设置已生效，不需要重启手机屏幕";
			}
			_pendingSettings = _settings.Snapshot.Value;
			_settingsDirty = false;
			_operationStatus.ForeColor = (candidate.VideoBitrateMbps == 32) ? UiPalette.Warning : UiPalette.Success;
		}
		catch (Exception ex)
		{
			try
			{
				if (handChanged)
				{
					_playspaceDrag.SetPhoneControllerHand(MapControllerHand(previous.ControllerHand));
				}
				await _settings.SaveAsync(previous, CancellationToken.None);
				if (overlayActive & requiresScreenRestart)
				{
					await _mediaSession.RestartScreenAsync(deviceKey, BuildVideoOptions(previous), handChanged ? (Func<CancellationToken, ValueTask>)((CancellationToken _) => ReloadLocalBindingAsync()) : null, CancellationToken.None);
				}
			}
			catch (Exception ex2)
			{
				throw new SettingsApplyException("SETTINGS_BATCH_ROLLBACK_FAILED", "设置应用失败，恢复上一次设置时也遇到错误；请关闭浮窗后重试", new AggregateException(ex, ex2));
			}
			throw new SettingsApplyException("SETTINGS_BATCH_APPLY_ROLLED_BACK", "设置应用失败，已恢复上一次有效设置", ex);
		}
	}

	private async Task StopPhoneOverlayAsync(CancellationToken token)
	{
		await _mediaSession.StopAsync(token);
	}

	private async Task StartPhoneOverlayAsync(string? deviceKey, CancellationToken token)
	{
		if (_phoneOverlay.HasSavedBindingChanged())
		{
			await ReloadLocalBindingAsync();
		}
		await _mediaSession.StartAsync(deviceKey, BuildVideoOptions(_settings.Snapshot.Value), token);
	}

	private ValueTask ReloadLocalBindingAsync()
	{
		_playspaceDrag.StopService();
		OpenVrBindingResult result;
		try
		{
			result = _phoneOverlay.ReloadLocalBinding();
		}
		finally
		{
			_playspaceDrag.Start();
		}
		if (!result.Succeeded)
		{
			throw new SettingsApplyException(result.ReasonCode, result.Message);
		}
		return ValueTask.CompletedTask;
	}

	private async Task RunVideoProbeAsync(CancellationToken token)
	{
		string deviceKey = _phone.Snapshot.SelectedDevice?.DeviceKey;
		PhoneVideoDecodeProbeResult probe = await _videoProbe.ProbeAsync(deviceKey, BuildVideoOptions(_settings.Snapshot.Value), token);
		_operationStatus.Text = $"视频与显卡链路正常：{AndroidDisplayNames.Codec(probe.Codec)} → {probe.PixelFormat} → D3D11 RGBA，{probe.Width}×{probe.Height}，步幅 {probe.Stride}，{probe.EncodedPacketCount} 个编码包 / {Math.Max(1L, probe.EncodedPayloadBytes / 1024)} KiB，解码帧 {Math.Max(1, probe.DecodedFrameBytes / 1024)} KiB，纹理槽 {probe.TextureSlot}";
		_operationStatus.ForeColor = UiPalette.Success;
	}

	private AndroidVideoOptions BuildVideoOptions(AppSettings settings)
	{
		(int nativeWidth, int nativeHeight) = GetNativeDisplayDimensions();
		VideoResolutionProfile profile = ((nativeWidth > 0 && nativeHeight > 0) ? VideoResolutionProfiles.Resolve(nativeWidth, nativeHeight, settings.VideoResolutionPercent) : new VideoResolutionProfile(100, 1, 1, 0, IsApproximate: false));
		return new AndroidVideoOptions
		{
			ResolutionPercent = profile.Percent,
			MaximumSize = profile.MaximumSize,
			VideoBitrateBitsPerSecond = checked(settings.VideoBitrateMbps * 1000000),
			MaximumFramesPerSecond = settings.VideoMaximumFramesPerSecond
		};
	}

	private (int Width, int Height) GetNativeDisplayDimensions()
	{
		AndroidDeviceDetails selectedDevice = _phone.Snapshot.SelectedDevice;
		int width = selectedDevice?.NativeDisplayWidth ?? 0;
		int height = selectedDevice?.NativeDisplayHeight ?? 0;
		PhoneOverlaySnapshot snapshot = _phoneOverlay.Snapshot;
		if (width > 0 && height > 0 && snapshot.Width > 0 && snapshot.Height > 0 && (width > height) != (snapshot.Width > snapshot.Height))
		{
			(width, height) = (height, width);
		}
		return (Width: width, Height: height);
	}

	// ============================ 操作包装 ============================

	private async Task RunUiOperationAsync(string progressMessage, Func<CancellationToken, Task> operation, bool immediateControlsOnly = false)
	{
		if (immediateControlsOnly)
		{
			SetImmediateSettingControls(enabled: false);
		}
		else
		{
			SetOperationControls(enabled: false);
		}
		_operationStatus.Text = progressMessage;
		_operationStatus.ForeColor = UiPalette.Info;
		try
		{
			await operation(_formLifetime.Token);
			if (!IsDisposed && !Disposing && _operationStatus.Text == progressMessage)
			{
				_operationStatus.Text = "操作完成";
				_operationStatus.ForeColor = UiPalette.Success;
			}
		}
		catch (OperationCanceledException) when (_formLifetime.IsCancellationRequested)
		{
			if (!IsDisposed && !Disposing)
			{
				_operationStatus.Text = "操作已取消";
			}
		}
		catch (OperationCanceledException)
		{
			_operationStatus.Text = "操作已取消";
		}
		catch (AndroidConnectionException ex)
		{
			ReportOperationFailure(ex.Message, ex.ReasonCode);
		}
		catch (PhoneVideoProbeException ex)
		{
			ReportOperationFailure(ex.Message, ex.ReasonCode);
		}
		catch (PhoneOverlayServiceException ex)
		{
			ReportOperationFailure(ex.Message, ex.ReasonCode);
		}
		catch (PhoneControlServiceException ex)
		{
			ReportOperationFailure(ex.Message, ex.ReasonCode);
		}
		catch (SettingsApplyException ex)
		{
			ReportOperationFailure(ex.Message, ex.ReasonCode);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
		{
			_operationStatus.Text = "设置或连接操作失败，请检查本地设置目录权限后重试";
			_operationStatus.ForeColor = UiPalette.Danger;
		}
		catch (Exception)
		{
			_operationStatus.Text = "操作遇到未预期错误\u3000[UI_OPERATION_FAILED]";
			_operationStatus.ForeColor = UiPalette.Danger;
		}
		finally
		{
			if (!IsDisposed && !Disposing)
			{
				if (immediateControlsOnly)
				{
					SetImmediateSettingControls(enabled: true);
				}
				else
				{
					SetOperationControls(enabled: true);
				}
				UpdateQualityStatus();
				UpdateSettingsActionButtons();
			}
		}
	}

	private void ReportOperationFailure(string message, string reasonCode)
	{
		_operationStatus.Text = message + "\u3000[" + reasonCode + "]";
		_operationStatus.ForeColor = UiPalette.Danger;
	}

	private void SetOperationControls(bool enabled)
	{
		_operationInProgress = !enabled;
		_refreshButton.Enabled = enabled;
		_settingsPage.SetBindingNoticeEnabled(enabled);
		_keepAwakeWhileGrabbedToggle.Enabled = enabled;
		_vrUnlockKeypadToggle.Enabled = enabled;
		_controllerHandSelector.Enabled = enabled;
		_playspaceDragToggle.Enabled = enabled;
		(int nativeWidth, int nativeHeight) = GetNativeDisplayDimensions();
		_resolutionSelector.Enabled = SettingsControlPolicy.CanUseResolutionSelector(_operationInProgress, nativeWidth, nativeHeight);
		_bitrateSelector.Enabled = enabled;
		_frameRateSelector.Enabled = enabled;
		_playspaceDragMultiplierSelector.Enabled = enabled;
		UpdateVideoProbeButton();
		UpdatePhoneOverlayButton();
		UpdateSettingsActionButtons();
	}

	private void SetImmediateSettingControls(bool enabled)
	{
		_immediateSettingsInProgress = !enabled;
		_keepAwakeWhileGrabbedToggle.Enabled = enabled;
		_vrUnlockKeypadToggle.Enabled = enabled;
		UpdateSettingsActionButtons();
	}

	// ============================ 格式化 ============================

	private static string ConnectionStateName(AndroidConnectionState state)
	{
		return state switch
		{
			AndroidConnectionState.Ready => "已连接",
			AndroidConnectionState.Discovering => "扫描中",
			AndroidConnectionState.Connecting => "连接中",
			AndroidConnectionState.Reconnecting => "连接中",
			AndroidConnectionState.AuthorizationRequired => "等待授权",
			AndroidConnectionState.WaitingForDevice => "等待设备",
			AndroidConnectionState.Offline => "设备离线",
			AndroidConnectionState.Faulted => "连接异常",
			_ => "未连接"
		};
	}

	private static string AudioStateName(PhoneAudioState state)
	{
		return state switch
		{
			PhoneAudioState.Playing => "已就绪",
			PhoneAudioState.Starting => "连接中",
			PhoneAudioState.Degraded => "已降级",
			PhoneAudioState.Faulted => "异常",
			PhoneAudioState.Stopping => "关闭中",
			_ => "未运行"
		};
	}

	private static string ControlStateName(PhoneControlState state)
	{
		return state switch
		{
			PhoneControlState.Ready => "已就绪",
			PhoneControlState.Starting => "连接中",
			PhoneControlState.Stopping => "关闭中",
			PhoneControlState.Faulted => "异常",
			_ => "未运行"
		};
	}

	private static string FormatDisplaySize(AndroidDeviceDetails details)
	{
		if (details.NativeDisplayWidth <= 0 || details.NativeDisplayHeight <= 0)
		{
			return "分辨率待探测";
		}
		return $"{details.NativeDisplayWidth} × {details.NativeDisplayHeight}";
	}

	private static string FormatPhoneOverlayStatus(PhoneOverlaySnapshot snapshot)
	{
		if (snapshot.State != PhoneOverlayState.Running)
		{
			return snapshot.Message + "\u3000[" + snapshot.ReasonCode + "]";
		}
		string fps = (snapshot.FramesPerSecond > 0.0) ? $"，{snapshot.FramesPerSecond:F1} FPS" : string.Empty;
		string anchor = snapshot.WorldAnchored ? "固定在 VR 世界" : "等待头显定位";
		string input;
		if (snapshot.SteamVrInputReady)
		{
			input = snapshot.OverlayGrabbed ? "正在抓取" : (snapshot.ControllerHovered ? "手柄已指向" : "手柄已就绪");
		}
		else
		{
			input = "手柄未就绪 " + snapshot.InputReasonCode;
		}
		return $"{snapshot.Message}：{snapshot.Width}×{snapshot.Height}，已提交 {snapshot.SubmittedFrames} 帧{fps}；{anchor}；{input}";
	}

	private static string FormatResolutionChoice(VideoResolutionProfile profile)
	{
		if (profile.Percent == 100)
		{
			return $"原生 {profile.ExpectedWidth}×{profile.ExpectedHeight}";
		}
		return $"{(profile.IsApproximate ? "约" : string.Empty)}{profile.Percent}% {profile.ExpectedWidth}×{profile.ExpectedHeight}";
	}

	private static OpenVrControllerHand MapControllerHand(ControllerHandPreference hand)
	{
		return (hand == ControllerHandPreference.Left) ? OpenVrControllerHand.Left : OpenVrControllerHand.Right;
	}

	private static void SetControlText(Sink sink, string text)
	{
		sink.Text = text;
	}

	private static void SetControlForeColor(Sink sink, Color color)
	{
		sink.ForeColor = color;
	}

	private static void SetControlText(Control control, string text)
	{
		if (!string.Equals(control.Text, text, StringComparison.Ordinal))
		{
			control.Text = text;
		}
	}

	private static void SetControlForeColor(Control control, Color color)
	{
		if (control.ForeColor != color)
		{
			control.ForeColor = color;
		}
	}

	// ============================ 显示缩放与窗体外壳 ============================

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	/// <summary>Win11 原生圆角（失败则保持直角，不影响功能）。</summary>
	private void ApplyRoundedCorners()
	{
		try
		{
			int preference = 2; // DWMWCP_ROUND
			DwmSetWindowAttribute(Handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref preference, sizeof(int));
		}
		catch (Exception)
		{
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		ApplyRoundedCorners();
	}

	/// <summary>无边框窗口自行处理命中测试：四边/四角可拖拽缩放。</summary>
	protected override void WndProc(ref Message m)
	{
		const int WM_NCHITTEST = 132;
		const int HTCLIENT = 1;
		if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
		{
			base.WndProc(ref m);
			if (m.Result.ToInt32() == HTCLIENT)
			{
				int lp = m.LParam.ToInt32();
				Point screenPoint = new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF)));
				Point point = PointToClient(screenPoint);
				int grip = Math.Max(5, (int)(6f * _displayScale));
				bool left = point.X <= grip;
				bool right = point.X >= ClientSize.Width - grip;
				bool top = point.Y <= grip;
				bool bottom = point.Y >= ClientSize.Height - grip;
				int result = 0;
				if (top && left)
				{
					result = 13;
				}
				else if (top && right)
				{
					result = 14;
				}
				else if (bottom && left)
				{
					result = 16;
				}
				else if (bottom && right)
				{
					result = 17;
				}
				else if (left)
				{
					result = 10;
				}
				else if (right)
				{
					result = 11;
				}
				else if (top)
				{
					result = 12;
				}
				else if (bottom)
				{
					result = 15;
				}
				if (result != 0)
				{
					m.Result = (IntPtr)result;
				}
			}
			return;
		}
		base.WndProc(ref m);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		AutoCheckOnStartup();
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		_scaleEventsAttached = true;
		_scaleLayout = new Ui3ScaleLayout();
	}

	protected override void OnDpiChanged(DpiChangedEventArgs e)
	{
		base.OnDpiChanged(e);
		QueueScaleRefresh();
	}

	protected override void OnLocationChanged(EventArgs e)
	{
		base.OnLocationChanged(e);
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		QueueScaleRefresh();
	}

	private void QueueScaleRefresh()
	{
		try
		{
			if (IsHandleCreated && !IsDisposed && !Disposing)
			{
				BeginInvoke(() =>
				{
					ApplyDisplayScale(DeviceDpi / 96f);
					Location = ClampToWorkingArea(Location);
				});
			}
		}
		catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated)
		{
		}
	}

	private Point ClampToWorkingArea(Point location)
	{
		Rectangle working = Screen.FromControl(this).WorkingArea;
		return new Point(
			Math.Clamp(location.X, working.Left, Math.Max(working.Left, working.Right - Width)),
			Math.Clamp(location.Y, working.Top, Math.Max(working.Top, working.Bottom - Height)));
	}

	private void DetachScaleEvents()
	{
		if (_scaleEventsAttached)
		{
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
			_scaleEventsAttached = false;
		}
	}

	private Ui3ScaleLayout? _scaleLayout;

	private bool _scaleEventsAttached;

	private sealed class Ui3ScaleLayout
	{
	}

	private void OnFormClosed(object? sender, FormClosedEventArgs eventArgs)
	{
		_formLifetime.Cancel();
		DetachEvents();
		FormClosed -= OnFormClosed;

		Icon windowIcon = _windowIcon;
		_windowIcon = null;
		Icon = null;
		windowIcon?.Dispose();
		_formLifetime.Dispose();
	}
}
