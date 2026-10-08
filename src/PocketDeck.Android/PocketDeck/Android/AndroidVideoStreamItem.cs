using System;
using System.Threading;

namespace PocketDeck.Android;

public sealed class AndroidVideoStreamItem : IDisposable
{
	private IDisposable? _owner;

	public AndroidVideoStreamItemKind Kind { get; }

	public int Width { get; }

	public int Height { get; }

	public bool ClientResized { get; }

	public long? PresentationTimeMicroseconds { get; }

	internal long? EstimatedCaptureTimestamp { get; set; }

	public bool IsKeyFrame { get; }

	public ReadOnlyMemory<byte> Payload { get; private set; }

	internal AndroidVideoStreamItem(AndroidVideoStreamItemKind kind, int width, int height, bool clientResized, long? presentationTimeMicroseconds, bool keyFrame, ReadOnlyMemory<byte> payload, IDisposable? owner)
	{
		Kind = kind;
		Width = width;
		Height = height;
		ClientResized = clientResized;
		PresentationTimeMicroseconds = presentationTimeMicroseconds;
		IsKeyFrame = keyFrame;
		Payload = payload;
		_owner = owner;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _owner, null)?.Dispose();
		Payload = ReadOnlyMemory<byte>.Empty;
	}
}
