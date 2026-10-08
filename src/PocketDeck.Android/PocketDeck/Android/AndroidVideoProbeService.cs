using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AndroidVideoProbeService(IAndroidVideoSessionFactory sessions) : IAndroidVideoProbeService
{
	private readonly IAndroidVideoSessionFactory _sessions = sessions;

	public async ValueTask<AndroidVideoProbeResult> ProbeAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		checked
		{
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(20L));
			try
			{
				AndroidVideoProbeResult result;
				await using (IAndroidVideoSession session = await _sessions.OpenAsync(deviceKey, new AndroidVideoOptions
				{
					PowerOnDevice = false
				}, timeout.Token))
				{
					int width = 0;
					int height = 0;
					int mediaPackets = 0;
					long payloadBytes = 0L;
					bool configurationReceived = false;
					bool keyFrameReceived = false;
					while (mediaPackets < 10)
					{
						using AndroidVideoStreamItem androidVideoStreamItem = await session.ReadNextAsync(timeout.Token);
						if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Session)
						{
							width = androidVideoStreamItem.Width;
							height = androidVideoStreamItem.Height;
						}
						else if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Configuration)
						{
							configurationReceived = androidVideoStreamItem.Payload.Length > 0;
						}
						else
						{
							mediaPackets++;
							payloadBytes += androidVideoStreamItem.Payload.Length;
							keyFrameReceived |= androidVideoStreamItem.IsKeyFrame;
						}
					}
					result = new AndroidVideoProbeResult(session.Codec, width, height, mediaPackets, payloadBytes, configurationReceived, keyFrameReceived);
				}
				return result;
			}
			catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
			{
				throw new AndroidConnectionException("ANDROID_VIDEO_PROBE_TIMEOUT", "视频自检在 20 秒内没有收到足够的数据");
			}
		}
	}
}
