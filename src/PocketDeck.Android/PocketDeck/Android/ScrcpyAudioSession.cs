using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class ScrcpyAudioSession(string deviceKey, string deviceName, AndroidAudioCodec codec, ScrcpyTransportLease transport, ScrcpyAudioProtocolReader protocol, IAndroidConnectionLogSink log) : IAndroidAudioSession, IAsyncDisposable
{
	private readonly ScrcpyTransportLease _transport = transport;

	private readonly ScrcpyAudioProtocolReader _protocol = protocol;

	private readonly IAndroidConnectionLogSink _log = log;

	private long _packetCount;

	private long _payloadBytes;

	private bool _disposed;

	public string DeviceKey { get; } = deviceKey;

	public string DeviceName { get; } = deviceName;

	public AndroidAudioCodec Codec { get; } = codec;

	public async ValueTask<AndroidAudioStreamItem> ReadNextAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		AndroidAudioStreamItem androidAudioStreamItem = await _protocol.ReadNextAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		checked
		{
			_packetCount++;
			_payloadBytes += androidAudioStreamItem.Payload.Length;
			return androidAudioStreamItem;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _transport.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			IAndroidConnectionLogSink log = _log;
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			string deviceKey = DeviceKey;
			long? packetCount = _packetCount;
			long? payloadBytes = _payloadBytes;
			log.TryWrite(new AndroidConnectionLogEntry(utcNow, "audio_stopped", "ANDROID_AUDIO_STOPPED", "手机内部音频流已停止", deviceKey, null, null, null, null, null, null, null, null, null, null, packetCount, payloadBytes));
		}
	}
}
