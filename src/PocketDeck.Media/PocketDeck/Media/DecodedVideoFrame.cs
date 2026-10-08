using System;
using System.Buffers;
using System.Threading;

namespace PocketDeck.Media;

public sealed class DecodedVideoFrame : IDisposable
{
	private IMemoryOwner<byte>? _owner;

	private readonly int _length;

	public int Width { get; }

	public int Height { get; }

	public int Stride { get; }

	public int VisibleWidth { get; }

	public int VisibleHeight { get; }

	public VideoPixelFormat PixelFormat { get; }

	public long PresentationTimeMicroseconds { get; }

	public ReadOnlyMemory<byte> Pixels => (_owner ?? throw new ObjectDisposedException("DecodedVideoFrame")).Memory.Slice(0, _length);

	internal DecodedVideoFrame(int width, int height, int stride, int visibleWidth, int visibleHeight, VideoPixelFormat pixelFormat, long presentationTimeMicroseconds, IMemoryOwner<byte> owner, int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(width, 1, "width");
		ArgumentOutOfRangeException.ThrowIfLessThan(height, 1, "height");
		ArgumentOutOfRangeException.ThrowIfLessThan(stride, width, "stride");
		ArgumentOutOfRangeException.ThrowIfLessThan(visibleWidth, 1, "visibleWidth");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(visibleWidth, width, "visibleWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(visibleHeight, 1, "visibleHeight");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(visibleHeight, height, "visibleHeight");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentOutOfRangeException.ThrowIfLessThan(length, 1, "length");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, owner.Memory.Length, "length");
		Width = width;
		Height = height;
		Stride = stride;
		VisibleWidth = visibleWidth;
		VisibleHeight = visibleHeight;
		PixelFormat = pixelFormat;
		PresentationTimeMicroseconds = presentationTimeMicroseconds;
		_owner = owner;
		_length = length;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _owner, null)?.Dispose();
	}
}
