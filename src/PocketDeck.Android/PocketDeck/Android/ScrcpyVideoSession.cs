using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class ScrcpyVideoSession(string deviceKey, string deviceName, AndroidVideoCodec codec, ScrcpyTransportLease transport, ScrcpyVideoProtocolReader protocol, AndroidVideoClockSynchronizer? videoClock, IAndroidConnectionLogSink log) : IAndroidVideoSession, IAsyncDisposable
{
	private readonly ScrcpyTransportLease _transport = transport;

	private readonly ScrcpyVideoProtocolReader _protocol = protocol;

	private readonly AndroidVideoClockSynchronizer? _videoClock = videoClock;

	private readonly IAndroidConnectionLogSink _log = log;

	private long _packetCount;

	private long _payloadBytes;

	private int _width;

	private int _height;

	private bool _disposed;

	public string DeviceKey { get; } = deviceKey;

	public string DeviceName { get; } = deviceName;

	public AndroidVideoCodec Codec { get; } = codec;

	public async ValueTask<AndroidVideoStreamItem> ReadNextAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		AndroidVideoStreamItem androidVideoStreamItem = await _protocol.ReadNextAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		checked
		{
			if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Session)
			{
				_width = androidVideoStreamItem.Width;
				_height = androidVideoStreamItem.Height;
				WriteLog("video_session", "ANDROID_VIDEO_SESSION", $"视频会话尺寸 {androidVideoStreamItem.Width}x{androidVideoStreamItem.Height}", androidVideoStreamItem.Width, androidVideoStreamItem.Height);
			}
			else
			{
				_packetCount++;
				_payloadBytes += androidVideoStreamItem.Payload.Length;
				long? presentationTimeMicroseconds = androidVideoStreamItem.PresentationTimeMicroseconds;
				if (presentationTimeMicroseconds.HasValue)
				{
					long valueOrDefault = presentationTimeMicroseconds.GetValueOrDefault();
					if (_videoClock != null)
					{
						androidVideoStreamItem.EstimatedCaptureTimestamp = _videoClock.EstimateLocalCaptureTimestamp(valueOrDefault);
					}
				}
			}
			return androidVideoStreamItem;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _transport.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			WriteLog("video_stopped", "ANDROID_VIDEO_STOPPED", "手机编码视频流已停止", _width, _height, _packetCount, _payloadBytes);
		}
	}

	private void WriteLog(string eventName, string reasonCode, string message, int? width = null, int? height = null, long? packetCount = null, long? payloadBytes = null)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, reasonCode, message, DeviceKey, null, null, null, null, null, null, null, null, width, height, packetCount, payloadBytes));
	}
}
