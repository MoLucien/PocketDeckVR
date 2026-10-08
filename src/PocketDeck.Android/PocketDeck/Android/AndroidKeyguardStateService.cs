using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AndroidKeyguardStateService(AndroidConnectionService connection, AdbCommandRunner commandRunner) : IAndroidKeyguardStateService
{
	private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(2L);

	private readonly AndroidConnectionService _connection = connection;

	private readonly AdbCommandRunner _commandRunner = commandRunner;

	public async ValueTask<AndroidKeyguardSnapshot> ProbeAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		if (!_connection.TryResolveReadyDevice(deviceKey, out var resolved))
		{
			return new AndroidKeyguardSnapshot(AndroidKeyguardState.Unknown, "ANDROID_KEYGUARD_DEVICE_NOT_READY", "手机尚未连接，无法检测锁屏状态");
		}
		AdbCommandResult adbCommandResult = await _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[6] { "-s", resolved.Serial, "shell", "dumpsys", "window", "policy" }), _probeTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!adbCommandResult.Succeeded)
		{
			return new AndroidKeyguardSnapshot(AndroidKeyguardState.Unknown, adbCommandResult.TimedOut ? "ANDROID_KEYGUARD_PROBE_TIMEOUT" : "ANDROID_KEYGUARD_PROBE_FAILED", "暂时无法读取手机锁屏状态");
		}
		AndroidKeyguardState androidKeyguardState = AndroidKeyguardOutputParser.Parse(adbCommandResult.StandardOutput);
		return androidKeyguardState switch
		{
			AndroidKeyguardState.Locked => new AndroidKeyguardSnapshot(androidKeyguardState, "ANDROID_KEYGUARD_LOCKED", "检测到手机处于密码锁屏状态"), 
			AndroidKeyguardState.Unlocked => new AndroidKeyguardSnapshot(androidKeyguardState, "ANDROID_KEYGUARD_UNLOCKED", "手机未处于密码锁屏状态"), 
			_ => new AndroidKeyguardSnapshot(androidKeyguardState, "ANDROID_KEYGUARD_UNKNOWN", "当前手机无法可靠识别密码锁屏界面"), 
		};
	}
}
