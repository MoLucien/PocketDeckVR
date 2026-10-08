using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpyVideoSessionFactory(AndroidConnectionService connection, ScrcpySessionLauncher launcher, IAndroidConnectionLogSink log) : IAndroidVideoSessionFactory
{
	private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(15L);

	private readonly AndroidConnectionService _connection = connection;

	private readonly ScrcpySessionLauncher _launcher = launcher;

	private readonly IAndroidConnectionLogSink _log = log;

	public async ValueTask<IAndroidVideoSession> OpenAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options, "options");
		options.Validate();
		if (!_connection.TryResolveReadyDevice(deviceKey, out var device))
		{
			throw new AndroidConnectionException("ANDROID_VIDEO_DEVICE_NOT_READY", "没有已授权且可用的安卓手机");
		}
		OperationDeadline deadline = OperationDeadline.Start(_startupTimeout);
		if (options.PowerOnDevice)
		{
			await WakeDeviceBeforeVideoAsync(device, deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		AndroidVideoClockSynchronizer videoClock = await TrySynchronizeVideoClockAsync(device, deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ScrcpyTransportLease transport = await _launcher.LaunchAsync(new ScrcpySessionLaunchRequest(device, ScrcpySessionKind.Video, (int scid) => BuildServerArguments(device.Serial, scid, options), deadline), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			ScrcpyVideoProtocolReader protocol = new ScrcpyVideoProtocolReader(transport.Stream);
			ScrcpyVideoHandshake scrcpyVideoHandshake = await ReadHandshakeAsync(protocol, deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			WriteLog("video_connected", "ANDROID_VIDEO_CONNECTED", $"手机编码视频流已连接，编码 {scrcpyVideoHandshake.Codec}", device.DeviceKey);
			return new ScrcpyVideoSession(device.DeviceKey, scrcpyVideoHandshake.DeviceName, scrcpyVideoHandshake.Codec, transport, protocol, videoClock, _log);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	private async ValueTask<AndroidVideoClockSynchronizer?> TrySynchronizeVideoClockAsync(ResolvedAndroidDevice device, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		string marker = $"vps-{Guid.NewGuid():N}";
		long startedTimestamp = Stopwatch.GetTimestamp();
		try
		{
			AdbCommandResult adbCommandResult = await _launcher.RunDeviceCommandAsync(device, new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "shell", "log", "-t", "VRPhoneScreenClock", marker }), TimeSpan.FromSeconds(1L), deadline, "ANDROID_VIDEO_CLOCK_SYNC_TIMEOUT", "同步手机视频时钟超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			long completedTimestamp = Stopwatch.GetTimestamp();
			if (!adbCommandResult.Succeeded)
			{
				return LogVideoClockUnavailable(device.DeviceKey);
			}
			AdbCommandResult adbCommandResult2 = await _launcher.RunDeviceCommandAsync(device, new _003C_003Ez__ReadOnlyArray<string>(new string[14]
			{
				"shell", "logcat", "-v", "threadtime", "-v", "monotonic", "-v", "usec", "-d", "-t",
				"20", "-s", "VRPhoneScreenClock:I", "*:S"
			}), TimeSpan.FromSeconds(1L), deadline, "ANDROID_VIDEO_CLOCK_SYNC_TIMEOUT", "同步手机视频时钟超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (adbCommandResult2.Succeeded && TryReadMonotonicSeconds(adbCommandResult2.StandardOutput, marker, out string monotonicSeconds) && AndroidVideoClockSynchronizer.TryCreate(monotonicSeconds, startedTimestamp, completedTimestamp, out AndroidVideoClockSynchronizer synchronizer))
			{
				WriteLog("video_clock_synchronized", "ANDROID_VIDEO_CLOCK_SYNCHRONIZED", $"手机视频时钟已同步，往返 {synchronizer.RoundTripDuration.TotalMilliseconds:F0} ms", device.DeviceKey);
				return synchronizer;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (AndroidConnectionException)
		{
		}
		return LogVideoClockUnavailable(device.DeviceKey);
	}

	private AndroidVideoClockSynchronizer? LogVideoClockUnavailable(string deviceKey)
	{
		WriteLog("video_clock_unavailable", "ANDROID_VIDEO_CLOCK_UNAVAILABLE", "手机视频时钟暂不可同步，延迟数据不可用", deviceKey);
		return null;
	}

	private static bool TryReadMonotonicSeconds(string logcatOutput, string marker, out string monotonicSeconds)
	{
		string[] array = logcatOutput.Split('\r', '\n');
		foreach (string text in array)
		{
			if (!text.Contains(marker, StringComparison.Ordinal))
			{
				continue;
			}
			string[] array2 = text.Split(new char[4] { ' ', '\t', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text2 in array2)
			{
				if (double.TryParse(text2, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) && result > 0.0)
				{
					monotonicSeconds = text2;
					return true;
				}
			}
		}
		monotonicSeconds = string.Empty;
		return false;
	}

	private async ValueTask WakeDeviceBeforeVideoAsync(ResolvedAndroidDevice device, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		AdbCommandResult adbCommandResult = await _launcher.RunDeviceCommandAsync(device, new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "shell", "input", "keyevent", "224" }), TimeSpan.FromSeconds(2L), deadline, "ANDROID_VIDEO_WAKE_TIMEOUT", "启动视频前唤醒手机超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!adbCommandResult.Succeeded)
		{
			WriteLog("video_wake_failed", adbCommandResult.TimedOut ? "ANDROID_VIDEO_WAKE_TIMEOUT" : "ANDROID_VIDEO_WAKE_FAILED", "启动视频前唤醒手机失败，将继续尝试建立视频链路", device.DeviceKey);
			return;
		}
		WriteLog("video_wake_sent", "ANDROID_VIDEO_WAKE_SENT", "启动视频前已发送手机唤醒指令", device.DeviceKey);
		TimeSpan remainingUpTo = deadline.GetRemainingUpTo(TimeSpan.FromMilliseconds(300L));
		if (remainingUpTo > TimeSpan.Zero)
		{
			await Task.Delay(remainingUpTo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static async ValueTask<ScrcpyVideoHandshake> ReadHandshakeAsync(ScrcpyVideoProtocolReader protocol, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		using CancellationTokenSource deadlineSource = deadline.CreateCancellationSource(cancellationToken);
		try
		{
			return await protocol.ReadHandshakeAsync(deadlineSource.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new AndroidConnectionException("ANDROID_VIDEO_HANDSHAKE_TIMEOUT", "等待手机视频协议握手超时");
		}
	}

	private static string[] BuildServerArguments(string serial, int scid, AndroidVideoOptions options)
	{
		int num = 17;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "-s";
		span[1] = serial;
		span[2] = "shell";
		span[3] = "CLASSPATH=/data/local/tmp/vrphonescreen-scrcpy-server-v4.1.jar";
		span[4] = "app_process";
		span[5] = "/";
		span[6] = "com.genymobile.scrcpy.Server";
		span[7] = "4.1";
		span[8] = $"scid={scid:x8}";
		span[9] = "log_level=info";
		span[10] = "video_codec=h264";
		span[11] = "video_bit_rate=" + options.VideoBitrateBitsPerSecond.ToString(CultureInfo.InvariantCulture);
		span[12] = "audio=false";
		span[13] = "max_fps=" + options.MaximumFramesPerSecond.ToString(CultureInfo.InvariantCulture);
		span[14] = "tunnel_forward=true";
		span[15] = "control=false";
		span[16] = "clipboard_autosync=false";
		List<string> list2 = list;
		if (options.MaximumSize > 0)
		{
			list2.Add("max_size=" + options.MaximumSize.ToString(CultureInfo.InvariantCulture));
		}
		if (!options.PowerOnDevice)
		{
			list2.Add("power_on=false");
		}
		return list2.ToArray();
	}

	private void WriteLog(string eventName, string reasonCode, string message, string deviceKey)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, reasonCode, message, deviceKey));
	}
}
