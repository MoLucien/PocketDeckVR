using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AndroidConnectionService : IAndroidConnectionService, IAsyncDisposable
{
	private readonly string _resourceDirectory;

	private readonly IAndroidConnectionLogSink _log;

	private readonly AndroidConnectionServiceOptions _options;

	private readonly IAdbServerShutdown _adbServerShutdown;

	private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);

	private readonly object _snapshotGate = new object();

	private CancellationTokenSource? _stopSource;

	private Task? _discoveryTask;

	private IAdbClient? _client;

	private AndroidConnectionSnapshot _snapshot;

	private Dictionary<string, AdbDeviceRecord> _devicesByKey = new Dictionary<string, AdbDeviceRecord>(StringComparer.Ordinal);

	private string? _preferredDeviceKey;

	private string? _probedDeviceKey;

	private AndroidDeviceDetails? _selectedDetails;

	private string? _lastScanSignature;

	private DateTimeOffset _lastScanLogAt;

	private string? _activeDeviceKey;

	private string? _activeSerial;

	private long _sessionEpoch;

	private bool _disposed;

	public AndroidConnectionSnapshot Snapshot
	{
		get
		{
			lock (_snapshotGate)
			{
				return _snapshot;
			}
		}
	}

	public event EventHandler<AndroidConnectionChangedEventArgs>? StateChanged;

	public AndroidConnectionService(string resourceDirectory, IAndroidConnectionLogSink log, AndroidConnectionServiceOptions options)
		: this(resourceDirectory, log, options, new BundledAdbServerShutdown(resourceDirectory, (options ?? throw new ArgumentNullException("options")).CommandTimeout))
	{
	}

	internal AndroidConnectionService(string resourceDirectory, IAndroidConnectionLogSink log, AndroidConnectionServiceOptions options, IAdbServerShutdown adbServerShutdown)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceDirectory, "resourceDirectory");
		_resourceDirectory = Path.GetFullPath(resourceDirectory);
		_log = log ?? throw new ArgumentNullException("log");
		_options = options ?? throw new ArgumentNullException("options");
		_adbServerShutdown = adbServerShutdown ?? throw new ArgumentNullException("adbServerShutdown");
		_snapshot = new AndroidConnectionSnapshot(0L, AndroidConnectionState.Created, "ANDROID_CREATED", "手机连接服务已创建", Array.Empty<AndroidDeviceView>(), null, DateTimeOffset.UtcNow, null);
	}

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();
		if (_discoveryTask != null)
		{
			return ValueTask.CompletedTask;
		}
		_stopSource = new CancellationTokenSource();
		Publish(AndroidConnectionState.Discovering, "ANDROID_DISCOVERING", "正在检查内置连接组件并查找手机", Array.Empty<AndroidDeviceView>(), null, null);
		_discoveryTask = RunDiscoveryLoopAsync(_stopSource.Token);
		return ValueTask.CompletedTask;
	}

	public async ValueTask RefreshAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await ScanOnceAsync(manualRefresh: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async ValueTask SelectDeviceAsync(string deviceKey, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(deviceKey, "deviceKey");
		cancellationToken.ThrowIfCancellationRequested();
		if (Snapshot.Devices.Any((AndroidDeviceView device) => string.Equals(device.DeviceKey, deviceKey, StringComparison.Ordinal)))
		{
			_preferredDeviceKey = deviceKey;
			_probedDeviceKey = null;
			await ScanOnceAsync(manualRefresh: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async ValueTask<AndroidWirelessConnectResult> ConnectWirelessAsync(string input, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (!TryParseWirelessInput(input, out string endpoint, out string? pairingCode))
		{
			return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_ENDPOINT_INVALID", "请输入手机“无线调试”页面显示的 IP:端口（首次配对请加配对码，如 192.168.1.5:37847 123456）");
		}
		await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			IAdbClient adbClient = await EnsureClientAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (adbClient == null)
			{
				return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_RESOURCES_UNAVAILABLE", "内置手机连接组件不可用，请先修复后再尝试无线连接");
			}
			if (pairingCode != null)
			{
				AdbCommandResult pairResult = await adbClient.PairWirelessAsync(endpoint, pairingCode, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				bool pairOk = pairResult.Succeeded && (pairResult.StandardOutput.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase) || pairResult.StandardOutput.Contains("already paired", StringComparison.OrdinalIgnoreCase) || pairResult.StandardOutput.Contains("成功", StringComparison.Ordinal));
				WriteLog("wireless_pair", pairOk ? "ANDROID_WIRELESS_PAIR_OK" : "ANDROID_WIRELESS_PAIR_FAILED", pairOk ? ("已与 " + endpoint + " 完成配对") : ("配对失败：" + FirstOutputLine(pairResult)), null, pairOk ? AndroidConnectionState.Discovering : null);
				if (!pairOk)
				{
					return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_PAIR_FAILED", "无线配对失败，请确认手机停留在“使用配对码配对设备”界面且配对码正确");
				}
			}
			AdbCommandResult connectResult = await adbClient.ConnectWirelessAsync(endpoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string output = FirstOutputLine(connectResult);
			bool already = connectResult.Succeeded && output.Contains("already connected", StringComparison.OrdinalIgnoreCase);
			bool connected = connectResult.Succeeded && (already || output.Contains("connected to", StringComparison.OrdinalIgnoreCase) || output.Contains("connected", StringComparison.OrdinalIgnoreCase));
			WriteLog("wireless_connect", connected ? (already ? "ANDROID_WIRELESS_ALREADY_CONNECTED" : "ANDROID_WIRELESS_CONNECTED") : "ANDROID_WIRELESS_CONNECT_FAILED", connected ? ("已连接 " + endpoint) : ("无线连接失败：" + output), null, connected ? AndroidConnectionState.Discovering : null);
			if (!connected)
			{
				return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_CONNECT_FAILED", "无线连接失败，请确认手机与电脑在同一网络、无线调试已开启（" + output + "）");
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (AndroidConnectionException ex)
		{
			return new AndroidWirelessConnectResult(Succeeded: false, ex.ReasonCode, ex.Message);
		}
		finally
		{
			_operationGate.Release();
		}
		await ScanOnceAsync(manualRefresh: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new AndroidWirelessConnectResult(Succeeded: true, "ANDROID_WIRELESS_CONNECTED", "无线连接已建立：" + endpoint, endpoint);
	}

	public async ValueTask<AndroidWirelessConnectResult> EnableWirelessViaUsbAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		AdbDeviceRecord usbDevice = default;
		bool hasUsbDevice = false;
		string? selectedDeviceKey = Snapshot.SelectedDevice?.DeviceKey;
		if (selectedDeviceKey != null)
		{
			lock (_snapshotGate)
			{
				hasUsbDevice = _devicesByKey.TryGetValue(selectedDeviceKey, out usbDevice);
			}
		}
		if (!hasUsbDevice || usbDevice.Transport != AndroidTransport.Usb || usbDevice.Status != AndroidDeviceStatus.Ready)
		{
			return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_USB_REQUIRED", "首次开通请先用 USB 数据线连接手机，再点击无线连接");
		}
		IAdbClient adbClient = await EnsureClientAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (adbClient == null)
		{
			return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_RESOURCES_UNAVAILABLE", "内置手机连接组件不可用，请先修复后再尝试无线连接");
		}
		AdbCommandResult wlanResult = await adbClient.ReadWlanInfoAsync(usbDevice.Serial, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string wlanIp = ParseWlanIpAddress(wlanResult.StandardOutput);
		if (string.IsNullOrEmpty(wlanIp))
		{
			string preview = (wlanResult.StandardOutput ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
			if (preview.Length > 120)
			{
				preview = preview.Substring(0, 120);
			}
			WriteLog("wireless_tcpip", "ANDROID_WIRELESS_IP_UNAVAILABLE", "未能读取手机 WiFi 地址；原始输出：" + preview);
			return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_IP_UNAVAILABLE", "未能读取手机的 WiFi 地址，请确认手机已连接 WiFi 后重试");
		}
		bool tcpipOk = false;
		string tcpipFailure = string.Empty;
		for (int tcpipAttempt = 0; tcpipAttempt < 3 && !tcpipOk; tcpipAttempt++)
		{
			if (tcpipAttempt > 0)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			AdbCommandResult tcpipResult = await adbClient.EnableTcpipAsync(usbDevice.Serial, 5555, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			tcpipOk = tcpipResult.Succeeded && tcpipResult.StandardOutput.Contains("restarting in TCP mode", StringComparison.OrdinalIgnoreCase);
			tcpipFailure = FirstOutputLine(tcpipResult);
		}
		WriteLog("wireless_tcpip", tcpipOk ? "ANDROID_WIRELESS_TCPIP_OK" : "ANDROID_WIRELESS_TCPIP_FAILED", tcpipOk ? "手机已切换到 TCP 调试模式" : ("切换 TCP 模式失败：" + tcpipFailure));
		if (!tcpipOk)
		{
			return new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_TCPIP_FAILED", "无法把手机切换到无线调试模式（" + tcpipFailure + "）");
		}
		string endpoint = wlanIp + ":5555";
		AndroidWirelessConnectResult connectResult = null;
		for (int connectAttempt = 0; connectAttempt < 4; connectAttempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(connectAttempt == 0 ? 2500 : 1800), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			connectResult = await ConnectWirelessAsync(endpoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (connectResult.Succeeded)
			{
				break;
			}
		}
		if (connectResult != null && connectResult.Succeeded)
		{
			return connectResult with
			{
				Message = "无线连接已开通：" + endpoint + "，现在可以拔掉 USB 数据线；之后同一网络下直接点击无线连接即可",
				Endpoint = endpoint
			};
		}
		return connectResult ?? new AndroidWirelessConnectResult(Succeeded: false, "ANDROID_WIRELESS_CONNECT_FAILED", "无线连接失败");
	}

	private static string? ParseWlanIpAddress(string output)
	{
		if (string.IsNullOrWhiteSpace(output))
		{
			return null;
		}
		string[] patterns = new string[4]
		{
			@"inet\s+(\d{1,3}(?:\.\d{1,3}){3})\s*/",
			@"inet\s+addr\s*:\s*(\d{1,3}(?:\.\d{1,3}){3})",
			@"\bsrc\s+(\d{1,3}(?:\.\d{1,3}){3})",
			@"^\s*(\d{1,3}(?:\.\d{1,3}){3})\s*$"
		};
		foreach (string pattern in patterns)
		{
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(output, pattern, System.Text.RegularExpressions.RegexOptions.Multiline))
			{
				string candidate = match.Groups[1].Value;
				if (IsUsableWlanAddress(candidate))
				{
					return candidate;
				}
			}
		}
		foreach (System.Text.RegularExpressions.Match match2 in System.Text.RegularExpressions.Regex.Matches(output, @"inet\s+(\d{1,3}(?:\.\d{1,3}){3})"))
		{
			string candidate2 = match2.Groups[1].Value;
			if (IsUsableWlanAddress(candidate2))
			{
				return candidate2;
			}
		}
		return null;
	}

	private static bool IsUsableWlanAddress(string candidate)
	{
		if (candidate.StartsWith("127.", StringComparison.Ordinal) || candidate.EndsWith(".255", StringComparison.Ordinal) || candidate.StartsWith("0.", StringComparison.Ordinal))
		{
			return false;
		}
		string[] parts = candidate.Split('.');
		foreach (string part in parts)
		{
			if (!int.TryParse(part, out var value) || value > 255)
			{
				return false;
			}
		}
		return true;
	}

	private async ValueTask<IAdbClient?> EnsureClientAsync(CancellationToken cancellationToken)
	{
		if (_client != null)
		{
			return _client;
		}
		AndroidResourceValidationResult androidResourceValidationResult = await AndroidResourceValidator.ValidateAsync(_resourceDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		WriteLog("resource_validation", androidResourceValidationResult.ReasonCode, androidResourceValidationResult.Message, null, androidResourceValidationResult.Succeeded ? AndroidConnectionState.Discovering : AndroidConnectionState.Faulted);
		if (!androidResourceValidationResult.Succeeded)
		{
			return null;
		}
		return _client = new AdbClient(new AdbCommandRunner(androidResourceValidationResult.ExecutablePath), _options.CommandTimeout);
	}

	private static bool TryParseWirelessInput(string input, out string endpoint, out string? pairingCode)
	{
		endpoint = string.Empty;
		pairingCode = null;
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}
		string[] array = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (array.Length == 0 || array.Length > 2)
		{
			return false;
		}
		if (!IsValidEndpoint(array[0]))
		{
			return false;
		}
		endpoint = array[0];
		if (array.Length == 2)
		{
			pairingCode = array[1];
		}
		return true;
	}

	private static bool IsValidEndpoint(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		int num = value.LastIndexOf(':');
		if (num <= 0 || num == value.Length - 1)
		{
			return false;
		}
		if (!int.TryParse(value.AsSpan(num + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int port) || port < 1 || port > 65535)
		{
			return false;
		}
		string host = value.Substring(0, num);
		if (host.Contains(':'))
		{
			return host.Contains("::");
		}
		foreach (string part in host.Split('.'))
		{
			if (part.Length == 0 || !int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int octet) || octet > 255)
			{
				return host.Length > 0 && host.All((char c) => char.IsLetterOrDigit(c) || c == '-');
			}
		}
		return true;
	}

	private static string FirstOutputLine(AdbCommandResult result)
	{
		string text = ((result.TimedOut || result.Succeeded) ? result.StandardOutput : result.StandardError);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = (result.TimedOut ? "命令超时" : "adb 返回码 " + result.ExitCode.ToString());
		}
		string[] array = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return (array.Length != 0) ? array[0] : text.Trim();
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		PublishFromCurrent(AndroidConnectionState.Stopping, "ANDROID_STOPPING", "正在停止手机连接服务");
		if (_stopSource != null)
		{
			await _stopSource.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		if (_discoveryTask != null)
		{
			try
			{
				await _discoveryTask.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
			}
		}
		await _operationGate.WaitAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		_operationGate.Release();
		AdbServerShutdownResult adbServerShutdownResult;
		try
		{
			adbServerShutdownResult = await _adbServerShutdown.StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex2) when ((ex2 is IOException || ex2 is UnauthorizedAccessException || ex2 is InvalidOperationException) ? true : false)
		{
			adbServerShutdownResult = new AdbServerShutdownResult(Succeeded: false, "ANDROID_ADB_SERVER_STOP_FAILED", "内置 ADB 后台服务退出失败（" + ex2.GetType().Name + "）");
		}
		WriteLog("adb_server_shutdown", adbServerShutdownResult.ReasonCode, adbServerShutdownResult.Message);
		PublishFromCurrent(AndroidConnectionState.Stopped, "ANDROID_STOPPED", "手机连接服务已停止");
		_stopSource?.Dispose();
		_operationGate.Dispose();
	}

	internal async ValueTask ScanOnceForTestAsync(CancellationToken cancellationToken)
	{
		await ScanOnceAsync(manualRefresh: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal void SetClientForTest(IAdbClient client)
	{
		_client = client;
	}

	internal bool TryResolveReadyDevice(string? requestedDeviceKey, out ResolvedAndroidDevice resolved)
	{
		lock (_snapshotGate)
		{
			string text = ((!string.IsNullOrWhiteSpace(requestedDeviceKey)) ? requestedDeviceKey : _snapshot.SelectedDevice?.DeviceKey);
			if (_snapshot.State == AndroidConnectionState.Ready && text != null && _sessionEpoch > 0 && _devicesByKey.TryGetValue(text, out AdbDeviceRecord value) && value.Status == AndroidDeviceStatus.Ready && string.Equals(_activeDeviceKey, value.DeviceKey, StringComparison.Ordinal) && string.Equals(_activeSerial, value.Serial, StringComparison.Ordinal))
			{
				resolved = new ResolvedAndroidDevice(value.DeviceKey, value.Serial, DisplayName(value), _sessionEpoch);
				return true;
			}
		}
		resolved = default;
		return false;
	}

	private async Task RunDiscoveryLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await ScanOnceAsync(manualRefresh: false, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await Task.Delay(Snapshot.State switch
			{
				AndroidConnectionState.Ready => _options.ReadyScanInterval, 
				AndroidConnectionState.Faulted => _options.FaultedScanInterval, 
				_ => _options.SearchingScanInterval, 
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async ValueTask ScanOnceAsync(bool manualRefresh, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}
		await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (_client == null)
			{
				AndroidResourceValidationResult androidResourceValidationResult = await AndroidResourceValidator.ValidateAsync(_resourceDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				WriteLog("resource_validation", androidResourceValidationResult.ReasonCode, androidResourceValidationResult.Message, null, androidResourceValidationResult.Succeeded ? AndroidConnectionState.Discovering : AndroidConnectionState.Faulted);
				if (!androidResourceValidationResult.Succeeded)
				{
					MarkTransportUnavailable();
					Publish(AndroidConnectionState.Faulted, androidResourceValidationResult.ReasonCode, androidResourceValidationResult.Message, Array.Empty<AndroidDeviceView>(), null, null);
					return;
				}
				_client = new AdbClient(new AdbCommandRunner(androidResourceValidationResult.ExecutablePath), _options.CommandTimeout);
			}
			if (manualRefresh && Snapshot.State != AndroidConnectionState.Ready)
			{
				PublishFromCurrent(AndroidConnectionState.Discovering, "ANDROID_REFRESHING", "正在重新扫描手机");
			}
			AdbListResult adbListResult = await _client.ListDevicesAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			AdbDeviceRecord[] usbDevices = adbListResult.Devices.Where((AdbDeviceRecord device) => device.Transport == AndroidTransport.Usb || device.Transport == AndroidTransport.Network).ToArray();
			DateTimeOffset scanTime = DateTimeOffset.UtcNow;
			lock (_snapshotGate)
			{
				_devicesByKey = usbDevices.ToDictionary((AdbDeviceRecord device) => device.DeviceKey, StringComparer.Ordinal);
			}
			string text = string.Join(';', usbDevices.Select((AdbDeviceRecord device) => $"{device.DeviceKey}|{device.Status}|{device.Transport}|{device.Model}"));
			if (!string.Equals(text, _lastScanSignature, StringComparison.Ordinal) || scanTime - _lastScanLogAt >= TimeSpan.FromMinutes(1L))
			{
				_lastScanSignature = text;
				_lastScanLogAt = scanTime;
				AndroidConnectionService androidConnectionService = this;
				int? deviceCount = usbDevices.Length;
				TimeSpan? duration = adbListResult.Duration;
				string toolVersion = adbListResult.ToolVersion;
				androidConnectionService.WriteLog("device_scan", "ANDROID_SCAN_OK", "手机设备扫描完成", null, null, deviceCount, duration, toolVersion);
			}
			AdbDeviceRecord selected = AndroidDeviceSelector.Select(usbDevices, _preferredDeviceKey);
			if ((object)selected == null)
			{
				MarkTransportUnavailable();
				_selectedDetails = null;
				_probedDeviceKey = null;
				PublishNoReadyDevice(usbDevices, scanTime);
				return;
			}
			if (_preferredDeviceKey == null)
			{
				_preferredDeviceKey = selected.DeviceKey;
			}
			if (!string.Equals(_probedDeviceKey, selected.DeviceKey, StringComparison.Ordinal))
			{
				Publish(AndroidConnectionState.Connecting, "ANDROID_PROBING", "正在读取 " + DisplayName(selected) + " 的系统信息", CreateViews(usbDevices, selected.DeviceKey), null, scanTime);
				AdbProbeResult adbProbeResult = await _client.ProbeAsync(selected.Serial, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				AndroidConnectionService androidConnectionService2 = this;
				string reasonCode = adbProbeResult.ReasonCode;
				string message = adbProbeResult.Message;
				string deviceKey = selected.DeviceKey;
				AndroidConnectionState? state = (adbProbeResult.Succeeded ? AndroidConnectionState.Ready : AndroidConnectionState.Reconnecting);
				TimeSpan? duration = adbProbeResult.Duration;
				string toolVersion = adbProbeResult.Model;
				string androidVersion = adbProbeResult.AndroidVersion;
				int? deviceCount = adbProbeResult.AndroidSdk;
				string cpuAbi = adbProbeResult.CpuAbi;
				androidConnectionService2.WriteLog("device_probe", reasonCode, message, deviceKey, state, null, duration, null, toolVersion, androidVersion, deviceCount, cpuAbi);
				if (!adbProbeResult.Succeeded)
				{
					MarkTransportUnavailable();
					Publish(AndroidConnectionState.Reconnecting, adbProbeResult.ReasonCode, adbProbeResult.Message, CreateViews(usbDevices, selected.DeviceKey), null, scanTime);
					return;
				}
				_probedDeviceKey = selected.DeviceKey;
				_selectedDetails = new AndroidDeviceDetails(selected.DeviceKey, adbProbeResult.Manufacturer, adbProbeResult.Brand, string.IsNullOrWhiteSpace(adbProbeResult.Model) ? selected.Model : adbProbeResult.Model, adbProbeResult.DeviceCodeName, adbProbeResult.AndroidVersion, adbProbeResult.AndroidSdk, adbProbeResult.CpuAbi, selected.Transport)
				{
					Capabilities = AndroidCapabilityEvaluator.Evaluate(adbProbeResult.AndroidSdk),
					NativeDisplayWidth = adbProbeResult.DisplayWidth,
					NativeDisplayHeight = adbProbeResult.DisplayHeight
				};
			}
			ActivateTransport(selected);
			int num = usbDevices.Count((AdbDeviceRecord device) => device.Status == AndroidDeviceStatus.Ready);
			string message2 = ((num > 1) ? $"已连接 {DisplayName(selected)}，共发现 {num} 台可用设备" : ("已连接 " + DisplayName(selected) + " · " + AndroidDisplayNames.Transport(selected.Transport)));
			Publish(AndroidConnectionState.Ready, (num > 1) ? "ANDROID_READY_MULTIPLE" : "ANDROID_READY", message2, CreateViews(usbDevices, selected.DeviceKey), _selectedDetails, scanTime);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (AndroidConnectionException ex2)
		{
			MarkTransportUnavailable();
			AndroidConnectionState androidConnectionState = (Snapshot.IsReady ? AndroidConnectionState.Reconnecting : AndroidConnectionState.Faulted);
			WriteLog("scan_error", ex2.ReasonCode, ex2.Message, null, androidConnectionState);
			PublishFromCurrent(androidConnectionState, ex2.ReasonCode, ex2.Message);
		}
		catch (Exception ex3) when ((ex3 is IOException || ex3 is UnauthorizedAccessException || ex3 is InvalidOperationException) ? true : false)
		{
			MarkTransportUnavailable();
			string text2 = "手机连接扫描失败，程序将自动重试";
			AndroidConnectionState androidConnectionState2 = (Snapshot.IsReady ? AndroidConnectionState.Reconnecting : AndroidConnectionState.Faulted);
			WriteLog("scan_error", "ANDROID_SCAN_UNEXPECTED", text2 + " (" + ex3.GetType().Name + ")", null, androidConnectionState2);
			PublishFromCurrent(androidConnectionState2, "ANDROID_SCAN_UNEXPECTED", text2);
		}		finally
		{
			_operationGate.Release();
		}
	}

	private void PublishNoReadyDevice(IReadOnlyList<AdbDeviceRecord> devices, DateTimeOffset scanTime)
	{
		AdbDeviceRecord adbDeviceRecord = devices.FirstOrDefault((AdbDeviceRecord device) => device.Status == AndroidDeviceStatus.Unauthorized);
		if ((object)adbDeviceRecord != null)
		{
			Publish(AndroidConnectionState.AuthorizationRequired, "ANDROID_AUTHORIZATION_REQUIRED", "已发现手机：请解锁手机，勾选始终允许，然后允许 USB 调试", CreateViews(devices, null), null, scanTime);
			return;
		}
		AdbDeviceRecord adbDeviceRecord2 = devices.FirstOrDefault((AdbDeviceRecord device) => device.Status == AndroidDeviceStatus.NoPermissions);
		if ((object)adbDeviceRecord2 != null)
		{
			Publish(AndroidConnectionState.Faulted, "ANDROID_DRIVER_PERMISSION_REQUIRED", "电脑没有访问手机的驱动权限，请检查 USB 驱动", CreateViews(devices, null), null, scanTime);
			return;
		}
		AdbDeviceRecord adbDeviceRecord3 = devices.FirstOrDefault((AdbDeviceRecord device) => device.Status == AndroidDeviceStatus.Offline);
		if ((object)adbDeviceRecord3 != null)
		{
			Publish(AndroidConnectionState.Offline, "ANDROID_DEVICE_OFFLINE", "手机连接离线，请重新插拔 USB 并确认 USB 调试仍已开启", CreateViews(devices, null), null, scanTime);
		}
		else
		{
			Publish(AndroidConnectionState.WaitingForDevice, "ANDROID_WAITING_FOR_DEVICE", "等待安卓手机连接（USB 数据线或 WiFi 无线）", CreateViews(devices, null), null, scanTime);
		}
	}

	private void ActivateTransport(AdbDeviceRecord device)
	{
		checked
		{
			lock (_snapshotGate)
			{
				if (!string.Equals(_activeDeviceKey, device.DeviceKey, StringComparison.Ordinal) || !string.Equals(_activeSerial, device.Serial, StringComparison.Ordinal))
				{
					_sessionEpoch++;
					_activeDeviceKey = device.DeviceKey;
					_activeSerial = device.Serial;
				}
			}
		}
	}

	private void MarkTransportUnavailable()
	{
		lock (_snapshotGate)
		{
			_activeDeviceKey = null;
			_activeSerial = null;
			_probedDeviceKey = null;
			_selectedDetails = null;
		}
	}

	private static AndroidDeviceView[] CreateViews(IReadOnlyList<AdbDeviceRecord> devices, string? selectedDeviceKey)
	{
		return devices.Select((AdbDeviceRecord device) => new AndroidDeviceView(device.DeviceKey, DisplayName(device), device.Model, device.Status, device.Transport, string.Equals(device.DeviceKey, selectedDeviceKey, StringComparison.Ordinal))).ToArray();
	}

	private void PublishFromCurrent(AndroidConnectionState state, string reasonCode, string message)
	{
		AndroidConnectionSnapshot snapshot = Snapshot;
		Publish(state, reasonCode, message, snapshot.Devices, snapshot.SelectedDevice, snapshot.LastSuccessfulScanAt);
	}

	private void Publish(AndroidConnectionState state, string reasonCode, string message, IReadOnlyList<AndroidDeviceView> devices, AndroidDeviceDetails? selectedDevice, DateTimeOffset? lastSuccessfulScanAt)
	{
		AndroidConnectionSnapshot snapshot;
		lock (_snapshotGate)
		{
			if (IsEquivalent(_snapshot, state, reasonCode, message, devices, selectedDevice))
			{
				if (_snapshot.LastSuccessfulScanAt != lastSuccessfulScanAt)
				{
					_snapshot = _snapshot with
					{
						LastSuccessfulScanAt = lastSuccessfulScanAt
					};
				}
				return;
			}
			snapshot = (_snapshot = new AndroidConnectionSnapshot(checked(_snapshot.Revision + 1), state, reasonCode, message, devices, selectedDevice, DateTimeOffset.UtcNow, lastSuccessfulScanAt));
		}
		WriteLog("state_changed", reasonCode, message, selectedDevice?.DeviceKey, state, devices.Count);
		StateChanged?.Invoke(this, new AndroidConnectionChangedEventArgs(snapshot));
	}

	private static bool IsEquivalent(AndroidConnectionSnapshot current, AndroidConnectionState state, string reasonCode, string message, IReadOnlyList<AndroidDeviceView> devices, AndroidDeviceDetails? selectedDevice)
	{
		if (current.State == state && string.Equals(current.ReasonCode, reasonCode, StringComparison.Ordinal) && string.Equals(current.Message, message, StringComparison.Ordinal) && current.Devices.SequenceEqual(devices))
		{
			return current.SelectedDevice == selectedDevice;
		}
		return false;
	}

	private void WriteLog(string eventName, string reasonCode, string message, string? deviceKey = null, AndroidConnectionState? state = null, int? deviceCount = null, TimeSpan? duration = null, string? toolVersion = null, string? deviceModel = null, string? androidVersion = null, int? androidSdk = null, string? cpuAbi = null)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, reasonCode, message, deviceKey, state, deviceCount, (!duration.HasValue) ? ((long?)null) : new long?(checked((long)duration.Value.TotalMilliseconds)), toolVersion, deviceModel, androidVersion, androidSdk, cpuAbi));
	}

	private static string DisplayName(AdbDeviceRecord device)
	{
		if (!string.IsNullOrWhiteSpace(device.Model))
		{
			return device.Model;
		}
		return "安卓设备 " + device.DeviceKey;
	}
}
