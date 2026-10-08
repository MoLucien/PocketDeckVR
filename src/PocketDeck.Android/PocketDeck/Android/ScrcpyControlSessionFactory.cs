using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpyControlSessionFactory(AndroidConnectionService connection, ScrcpySessionLauncher launcher, IAndroidConnectionLogSink log) : IAndroidControlSessionFactory
{
	private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(15L);

	private readonly AndroidConnectionService _connection = connection;

	private readonly ScrcpySessionLauncher _launcher = launcher;

	private readonly IAndroidConnectionLogSink _log = log;

	public async ValueTask<IAndroidControlSession> OpenAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		if (!_connection.TryResolveReadyDevice(deviceKey, out var device))
		{
			throw new AndroidConnectionException("ANDROID_CONTROL_DEVICE_NOT_READY", "没有已授权且可用的安卓手机");
		}
		OperationDeadline deadline = OperationDeadline.Start(_startupTimeout);
		AndroidDisplaySize displaySize = await ReadDisplaySizeAsync(device, deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ScrcpyTransportLease scrcpyTransportLease = await _launcher.LaunchAsync(new ScrcpySessionLaunchRequest(device, ScrcpySessionKind.Control, (int scid) => BuildServerArguments(device.Serial, scid), deadline), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			ScrcpyControlProtocolWriter protocol = new ScrcpyControlProtocolWriter(scrcpyTransportLease.Stream);
			_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "control_connected", "ANDROID_CONTROL_CONNECTED", "独立手机控制通道已连接", device.DeviceKey));
			return new ScrcpyControlSession(device.DeviceKey, scrcpyTransportLease, protocol, displaySize, _log);
		}
		catch
		{
			await scrcpyTransportLease.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	private async ValueTask<AndroidDisplaySize> ReadDisplaySizeAsync(ResolvedAndroidDevice device, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		AdbCommandResult adbCommandResult = await _launcher.RunDeviceCommandAsync(device, new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "shell", "wm", "size" }), TimeSpan.FromSeconds(3L), deadline, "ANDROID_CONTROL_DISPLAY_SIZE_TIMEOUT", "读取手机原生显示尺寸超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (adbCommandResult.TimedOut)
		{
			throw new AndroidConnectionException("ANDROID_CONTROL_DISPLAY_SIZE_TIMEOUT", "读取手机原生显示尺寸超时，未启动触控以避免坐标偏移");
		}
		if (!adbCommandResult.Succeeded || !AndroidDisplaySize.TryParseWmSize(adbCommandResult.StandardOutput, out var size))
		{
			throw new AndroidConnectionException("ANDROID_CONTROL_DISPLAY_SIZE_FAILED", "无法读取手机原生显示尺寸，未启动触控以避免坐标偏移");
		}
		IAndroidConnectionLogSink log = _log;
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		string message = $"手机触控使用原生显示尺寸 {size.Width}x{size.Height}";
		string deviceKey = device.DeviceKey;
		int? videoWidth = size.Width;
		int? videoHeight = size.Height;
		log.TryWrite(new AndroidConnectionLogEntry(utcNow, "control_display_size", "ANDROID_CONTROL_DISPLAY_SIZE_READY", message, deviceKey, null, null, null, null, null, null, null, null, videoWidth, videoHeight));
		return size;
	}

	private static string[] BuildServerArguments(string serial, int scid)
	{
		return new string[18]
		{
			"-s",
			serial,
			"shell",
			"CLASSPATH=/data/local/tmp/vrphonescreen-scrcpy-server-v4.1.jar",
			"app_process",
			"/",
			"com.genymobile.scrcpy.Server",
			"4.1",
			$"scid={scid:x8}",
			"log_level=info",
			"video=false",
			"audio=false",
			"control=true",
			"tunnel_forward=true",
			"clipboard_autosync=false",
			"send_device_meta=false",
			"send_frame_meta=false",
			"send_stream_meta=false"
		};
	}
}
