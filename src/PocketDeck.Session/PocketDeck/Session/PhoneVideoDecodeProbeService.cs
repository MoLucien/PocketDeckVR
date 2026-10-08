using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;
using PocketDeck.Media;
using PocketDeck.SteamVR;

namespace PocketDeck.Session;

public sealed class PhoneVideoDecodeProbeService(IAndroidVideoSessionFactory videoSessions, IAndroidConnectionLogSink log) : IPhoneVideoDecodeProbeService
{
	private readonly IAndroidVideoSessionFactory _videoSessions = videoSessions;

	private readonly IAndroidConnectionLogSink _log = log;

	public async ValueTask<PhoneVideoDecodeProbeResult> ProbeAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options, "options");
		checked
		{
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(30L));
			try
			{
				PhoneVideoDecodeProbeResult result;
				await using (IAndroidVideoSession session = await _videoSessions.OpenAsync(deviceKey, options, timeout.Token))
				{
					if (session.Codec != AndroidVideoCodec.H264)
					{
						throw new PhoneVideoProbeException("VIDEO_DECODE_CODEC_UNSUPPORTED", $"当前视频编码 {session.Codec} 尚未接入解码器");
					}
					int width = 0;
					int height = 0;
					int encodedPackets = 0;
					long encodedBytes = 0L;
					byte[] configuration = null;
					IH264VideoDecoder decoder = null;
					IGpuVideoFramePresenter presenter = null;
					try
					{
						while (true)
						{
							if (encodedPackets < 120)
							{
								using AndroidVideoStreamItem androidVideoStreamItem = await session.ReadNextAsync(timeout.Token);
								if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Session)
								{
									width = androidVideoStreamItem.Width;
									height = androidVideoStreamItem.Height;
									continue;
								}
								ReadOnlyMemory<byte> readOnlyMemory;
								if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Configuration)
								{
									readOnlyMemory = androidVideoStreamItem.Payload;
									configuration = readOnlyMemory.ToArray();
									continue;
								}
								encodedPackets++;
								long num = encodedBytes;
								readOnlyMemory = androidVideoStreamItem.Payload;
								encodedBytes = num + readOnlyMemory.Length;
								if (decoder == null && width > 0 && height > 0 && configuration != null)
								{
									decoder = VideoDecoderFactory.CreateMediaFoundationH264(width, height, options.MaximumFramesPerSecond, configuration);
									presenter = GpuVideoFramePresenterFactory.CreateD3D11();
								}
								if (decoder == null)
								{
									continue;
								}
								IH264VideoDecoder iH264VideoDecoder = decoder;
								readOnlyMemory = androidVideoStreamItem.Payload;
								using DecodedVideoFrame decodedVideoFrame = iH264VideoDecoder.DecodePacket(readOnlyMemory.Span, androidVideoStreamItem.PresentationTimeMicroseconds.GetValueOrDefault(), androidVideoStreamItem.IsKeyFrame);
								if (decodedVideoFrame == null)
								{
									continue;
								}
								GpuVideoFrame gpuVideoFrame = presenter.Present(decodedVideoFrame);
								AndroidVideoCodec codec = session.Codec;
								int visibleWidth = decodedVideoFrame.VisibleWidth;
								int visibleHeight = decodedVideoFrame.VisibleHeight;
								VideoPixelFormat pixelFormat = decodedVideoFrame.PixelFormat;
								int stride = decodedVideoFrame.Stride;
								int encodedPacketCount = encodedPackets;
								long encodedPayloadBytes = encodedBytes;
								readOnlyMemory = decodedVideoFrame.Pixels;
								PhoneVideoDecodeProbeResult phoneVideoDecodeProbeResult = new PhoneVideoDecodeProbeResult(codec, visibleWidth, visibleHeight, pixelFormat, stride, encodedPacketCount, encodedPayloadBytes, readOnlyMemory.Length, decoder.BackendName, presenter.AdapterBackend, gpuVideoFrame.TextureSlot);
								IAndroidConnectionLogSink log = _log;
								DateTimeOffset utcNow = DateTimeOffset.UtcNow;
								string deviceKey2 = session.DeviceKey;
								int? videoWidth = decodedVideoFrame.VisibleWidth;
								int? videoHeight = decodedVideoFrame.VisibleHeight;
								long? packetCount = encodedPackets;
								long? payloadBytes = encodedBytes;
								log.TryWrite(new AndroidConnectionLogEntry(utcNow, "video_decode_probe", "VIDEO_DECODE_READY", "手机视频编码与解码链路正常", deviceKey2, null, null, null, null, null, null, null, null, videoWidth, videoHeight, packetCount, payloadBytes));
								result = phoneVideoDecodeProbeResult;
								break;
							}
							throw new PhoneVideoProbeException("VIDEO_DECODE_FRAME_LIMIT", "收到 120 个编码包后仍未得到解码画面");
						}
					}
					finally
					{
						presenter?.Dispose();
						decoder?.Dispose();
					}
				}
				return result;
			}
			catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
			{
				throw new PhoneVideoProbeException("VIDEO_DECODE_PROBE_TIMEOUT", "30 秒内没有得到解码画面，请确认手机屏幕已点亮");
			}
			catch (MediaDecoderException ex2)
			{
				throw new PhoneVideoProbeException(ex2.ReasonCode, ex2.Message, ex2);
			}
			catch (GpuVideoPresenterException ex3)
			{
				throw new PhoneVideoProbeException(ex3.ReasonCode, ex3.Message, ex3);
			}
		}
	}
}
