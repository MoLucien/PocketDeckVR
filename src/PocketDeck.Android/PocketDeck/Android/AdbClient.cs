using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AdbClient(AdbCommandRunner runner, TimeSpan commandTimeout) : IAdbClient
{
	private readonly AdbCommandRunner _runner = runner;

	private readonly TimeSpan _commandTimeout = commandTimeout;

	private string? _toolVersion;

	public async ValueTask<AdbListResult> ListDevicesAsync(CancellationToken cancellationToken)
	{
		string toolVersion = _toolVersion;
		string text = toolVersion;
		if (text == null)
		{
			_toolVersion = await ReadToolVersionAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		AdbCommandResult adbCommandResult = await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "devices", "-l" }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		EnsureSuccess(adbCommandResult, "ANDROID_SCAN");
		return new AdbListResult(AdbOutputParser.ParseDevices(adbCommandResult.StandardOutput), adbCommandResult.Duration, _toolVersion);
	}

	public async ValueTask<AdbProbeResult> ProbeAsync(string serial, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serial, "serial");
		AdbCommandResult result = await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "-s", serial, "shell", "getprop" }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.Succeeded)
		{
			return new AdbProbeResult(Succeeded: false, result.TimedOut ? "ANDROID_PROBE_TIMEOUT" : "ANDROID_PROBE_FAILED", result.TimedOut ? "读取手机信息超时" : "手机已发现，但暂时无法读取系统信息", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, string.Empty, 0, 0, result.Duration);
		}
		IReadOnlyDictionary<string, string> properties = AdbOutputParser.ParseProperties(result.StandardOutput);
		string s = ReadProperty(properties, "ro.build.version.sdk");
		int? sdk = (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var result2) ? new int?(result2) : ((int?)null));
		AdbCommandResult adbCommandResult = await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "-s", serial, "shell", "wm", "size" }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		AndroidDisplaySize androidDisplaySize = ((adbCommandResult.Succeeded && AndroidDisplaySize.TryParseWmSize(adbCommandResult.StandardOutput, out var size)) ? size : default(AndroidDisplaySize));
		return new AdbProbeResult(Succeeded: true, "ANDROID_PROBE_READY", "手机系统信息已读取", ReadProperty(properties, "ro.product.manufacturer"), ReadProperty(properties, "ro.product.brand"), ReadProperty(properties, "ro.product.model"), ReadProperty(properties, "ro.product.device"), ReadProperty(properties, "ro.build.version.release"), sdk, ReadProperty(properties, "ro.product.cpu.abi"), androidDisplaySize.Width, androidDisplaySize.Height, result.Duration + adbCommandResult.Duration);
	}

	public async ValueTask<AdbCommandResult> ConnectWirelessAsync(string endpoint, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, "endpoint");
		return await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "connect", endpoint }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async ValueTask<AdbCommandResult> PairWirelessAsync(string endpoint, string pairingCode, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, "endpoint");
		ArgumentException.ThrowIfNullOrWhiteSpace(pairingCode, "pairingCode");
		return await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "pair", endpoint, pairingCode }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async ValueTask<AdbCommandResult> EnableTcpipAsync(string serial, int port, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serial, "serial");
		return await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "-s", serial, "tcpip", port.ToString(CultureInfo.InvariantCulture) }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async ValueTask<AdbCommandResult> ReadWlanInfoAsync(string serial, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serial, "serial");
		string compound = "ip -f inet addr show wlan0 ; ip route show dev wlan0 ; getprop dhcp.wlan0.ipaddress ; ifconfig wlan0 2>/dev/null ; ifconfig 2>/dev/null";
		return await _runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "-s", serial, "shell", compound }), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async ValueTask<string> ReadToolVersionAsync(CancellationToken cancellationToken)
	{
		AdbCommandResult adbCommandResult = await _runner.RunAsync(new _003C_003Ez__ReadOnlySingleElementList<string>("version"), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		EnsureSuccess(adbCommandResult, "ANDROID_TOOL_VERSION");
		string[] source = adbCommandResult.StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return source.FirstOrDefault((string line) => line.StartsWith("Version ", StringComparison.OrdinalIgnoreCase)) ?? source.FirstOrDefault() ?? "unknown";
	}

	private static void EnsureSuccess(AdbCommandResult result, string operation)
	{
		if (result.TimedOut)
		{
			throw new AndroidConnectionException(operation + "_TIMEOUT", "手机连接工具响应超时");
		}
		if (!result.Succeeded)
		{
			throw new AndroidConnectionException(operation + "_FAILED", "手机连接工具执行失败");
		}
	}

	private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string name)
	{
		if (!properties.TryGetValue(name, out string value))
		{
			return string.Empty;
		}
		return value.Trim();
	}
}
