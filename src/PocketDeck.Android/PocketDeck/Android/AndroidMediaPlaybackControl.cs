using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AndroidMediaPlaybackControl(AndroidConnectionService connection, AdbCommandRunner commandRunner, IAndroidConnectionLogSink log) : IAndroidMediaPlaybackControl
{
	private readonly AndroidConnectionService _connection = connection;

	private readonly AdbCommandRunner _commandRunner = commandRunner;

	private readonly IAndroidConnectionLogSink _log = log;

	public async ValueTask<AndroidMediaPauseLease> PauseForVrInterruptionAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		if (!_connection.TryResolveReadyDevice(deviceKey, out var device))
		{
			return new AndroidMediaPauseLease(deviceKey, ResumeRequired: false, "ANDROID_MEDIA_DEVICE_NOT_READY", "没有可暂停媒体播放的手机");
		}
		AdbCommandResult adbCommandResult = await _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "-s", device.Serial, "shell", "dumpsys", "media_session" }), TimeSpan.FromSeconds(3L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!adbCommandResult.Succeeded || !IsPlaybackActive(adbCommandResult.StandardOutput))
		{
			string reasonCode = (adbCommandResult.Succeeded ? "ANDROID_MEDIA_ALREADY_PAUSED" : "ANDROID_MEDIA_STATE_UNAVAILABLE");
			string message = (adbCommandResult.Succeeded ? "手机媒体当前未播放，无需暂停" : "无法确认手机媒体播放状态，将继续重启且不会误启动媒体");
			WriteLog(reasonCode, message, device.DeviceKey);
			return new AndroidMediaPauseLease(device.DeviceKey, ResumeRequired: false, reasonCode, message);
		}
		AdbCommandResult adbCommandResult2 = await SendMediaKeyAsync(device, 127, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!adbCommandResult2.Succeeded)
		{
			throw new AndroidConnectionException(adbCommandResult2.TimedOut ? "ANDROID_MEDIA_PAUSE_TIMEOUT" : "ANDROID_MEDIA_PAUSE_FAILED", "关闭或重启 VR 手机画面前无法暂停手机媒体播放");
		}
		await Task.Delay(TimeSpan.FromMilliseconds(40L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		WriteLog("ANDROID_MEDIA_PAUSED_FOR_VR_INTERRUPTION", "手机媒体已暂停，等待 VR 画面恢复", device.DeviceKey);
		return new AndroidMediaPauseLease(device.DeviceKey, ResumeRequired: true, "ANDROID_MEDIA_PAUSED_FOR_VR_INTERRUPTION", "手机媒体已暂停，等待 VR 画面恢复");
	}

	public async ValueTask ResumeAfterVrReadyAsync(AndroidMediaPauseLease lease, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease, "lease");
		if (lease.ResumeRequired)
		{
			if (!_connection.TryResolveReadyDevice(lease.DeviceKey, out var device))
			{
				throw new AndroidConnectionException("ANDROID_MEDIA_RESUME_DEVICE_NOT_READY", "VR 画面已恢复，但手机连接不可用，无法继续原来的媒体播放");
			}
			AdbCommandResult adbCommandResult = await SendMediaKeyAsync(device, 126, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!adbCommandResult.Succeeded)
			{
				throw new AndroidConnectionException(adbCommandResult.TimedOut ? "ANDROID_MEDIA_RESUME_TIMEOUT" : "ANDROID_MEDIA_RESUME_FAILED", "VR 画面已恢复，但手机媒体继续播放失败");
			}
			WriteLog("ANDROID_MEDIA_RESUMED_AFTER_VR_READY", "VR 画面恢复后已继续手机媒体播放", device.DeviceKey);
		}
	}

	internal static bool IsPlaybackActive(string output)
	{
		if (string.IsNullOrWhiteSpace(output))
		{
			return false;
		}
		bool flag = false;
		bool flag2 = false;
		string[] array = output.Split('\n');
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.StartsWith("Sessions Stack", StringComparison.OrdinalIgnoreCase))
			{
				flag = true;
				flag2 = false;
			}
			else if (flag)
			{
				if (text2.StartsWith("active=", StringComparison.OrdinalIgnoreCase))
				{
					flag2 = text2.Contains("active=true", StringComparison.OrdinalIgnoreCase);
				}
				else if (flag2 && IsPlayingStateLine(text2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsPlayingStateLine(string line)
	{
		if (!line.StartsWith("state=PlaybackState", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		ReadOnlySpan<char> span = line.AsSpan("state=PlaybackState".Length).TrimStart();
		if (span.IsEmpty || span[0] != '{')
		{
			return false;
		}
		span = span.Slice(1).TrimStart();
		if (!span.StartsWith("state=", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		ReadOnlySpan<char> span2 = span.Slice("state=".Length);
		int num = span2.IndexOfAny(',', '}');
		if (num < 0)
		{
			return false;
		}
		span2 = span2.Slice(0, num).Trim();
		if (!MemoryExtensions.Equals(span2, "3", StringComparison.Ordinal))
		{
			return MemoryExtensions.Equals(span2, "PLAYING(3)", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private ValueTask<AdbCommandResult> SendMediaKeyAsync(ResolvedAndroidDevice device, int keyCode, CancellationToken cancellationToken)
	{
		return _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[6]
		{
			"-s",
			device.Serial,
			"shell",
			"input",
			"keyevent",
			keyCode.ToString(CultureInfo.InvariantCulture)
		}), TimeSpan.FromSeconds(3L), cancellationToken);
	}

	private void WriteLog(string reasonCode, string message, string deviceKey)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "android_media_playback", reasonCode, message, deviceKey));
	}
}
