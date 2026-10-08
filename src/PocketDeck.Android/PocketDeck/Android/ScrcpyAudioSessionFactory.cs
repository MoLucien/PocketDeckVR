using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpyAudioSessionFactory(AndroidConnectionService connection, ScrcpySessionLauncher launcher, IAndroidConnectionLogSink log) : IAndroidAudioSessionFactory
{
	private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(15L);

	private readonly AndroidConnectionService _connection = connection;

	private readonly ScrcpySessionLauncher _launcher = launcher;

	private readonly IAndroidConnectionLogSink _log = log;

	public async ValueTask<IAndroidAudioSession> OpenAsync(string? deviceKey, AndroidAudioOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options, "options");
		if (options.Codec != AndroidAudioCodec.Opus)
		{
			throw new AndroidConnectionException("ANDROID_AUDIO_CODEC_UNSUPPORTED", "当前版本只启用低延迟 Opus 音频");
		}
		if (!_connection.TryResolveReadyDevice(deviceKey, out var device))
		{
			throw new AndroidConnectionException("ANDROID_AUDIO_DEVICE_NOT_READY", "没有已授权且可用的 USB 安卓手机");
		}
		AndroidCapability androidCapability = _connection.Snapshot.SelectedDevice?.Capabilities.InternalAudio ?? AndroidDeviceCapabilities.Unknown.InternalAudio;
		if (androidCapability.State == AndroidCapabilityState.Unavailable)
		{
			throw new AndroidConnectionException(androidCapability.ReasonCode, androidCapability.Message);
		}
		OperationDeadline deadline = OperationDeadline.Start(_startupTimeout);
		ScrcpyTransportLease transport = await _launcher.LaunchAsync(new ScrcpySessionLaunchRequest(device, ScrcpySessionKind.Audio, (int scid) => BuildServerArguments(device.Serial, scid, options), deadline), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			ScrcpyAudioProtocolReader protocol = new ScrcpyAudioProtocolReader(transport.Stream);
			ScrcpyAudioHandshake scrcpyAudioHandshake = await ReadHandshakeAsync(protocol, deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			WriteLog("audio_connected", "ANDROID_AUDIO_CONNECTED", $"手机内部音频流已连接，编码 {scrcpyAudioHandshake.Codec}", device.DeviceKey);
			return new ScrcpyAudioSession(device.DeviceKey, scrcpyAudioHandshake.DeviceName, scrcpyAudioHandshake.Codec, transport, protocol, _log);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	private static async ValueTask<ScrcpyAudioHandshake> ReadHandshakeAsync(ScrcpyAudioProtocolReader protocol, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		using CancellationTokenSource deadlineSource = deadline.CreateCancellationSource(cancellationToken);
		try
		{
			return await protocol.ReadHandshakeAsync(deadlineSource.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new AndroidConnectionException("ANDROID_AUDIO_HANDSHAKE_TIMEOUT", "等待手机音频协议握手超时");
		}
	}

	private static string[] BuildServerArguments(string serial, int scid, AndroidAudioOptions options)
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
		span[10] = "video=false";
		span[11] = "audio=true";
		span[12] = "audio_codec=opus";
		span[13] = "audio_source=output";
		span[14] = "control=false";
		span[15] = "clipboard_autosync=false";
		span[16] = "tunnel_forward=true";
		List<string> list2 = list;
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
