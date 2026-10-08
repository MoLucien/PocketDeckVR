using System;
using System.Threading;

namespace PocketDeck.Android;

public sealed class AndroidLatestVideoFrame(int generation, int width, int height, byte[] configuration, AndroidVideoStreamItem item) : IDisposable
{
	private AndroidVideoStreamItem? _item = item;

	public int Generation { get; } = generation;

	public int Width { get; } = width;

	public int Height { get; } = height;

	public ReadOnlyMemory<byte> Configuration { get; } = configuration;

	public long PresentationTimeMicroseconds => (_item ?? throw new ObjectDisposedException("AndroidLatestVideoFrame")).PresentationTimeMicroseconds.GetValueOrDefault();

	public long? EstimatedCaptureTimestamp => (_item ?? throw new ObjectDisposedException("AndroidLatestVideoFrame")).EstimatedCaptureTimestamp;

	public bool IsKeyFrame => (_item ?? throw new ObjectDisposedException("AndroidLatestVideoFrame")).IsKeyFrame;

	public ReadOnlyMemory<byte> Payload => (_item ?? throw new ObjectDisposedException("AndroidLatestVideoFrame")).Payload;

	public void Dispose()
	{
		Interlocked.Exchange(ref _item, null)?.Dispose();
	}
}
