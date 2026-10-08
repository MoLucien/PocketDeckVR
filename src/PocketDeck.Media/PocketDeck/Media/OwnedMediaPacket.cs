using System;
using System.Buffers;
using System.Threading;

namespace PocketDeck.Media;

internal sealed class OwnedMediaPacket : IEncodedMediaPacket, IDisposable
{
	private IMemoryOwner<byte>? _owner;

	private readonly int _length;

	public MediaCodec Codec { get; }

	public MediaTimestamp Timestamp { get; }

	public long Sequence { get; }

	public ReadOnlyMemory<byte> Payload => (_owner ?? throw new ObjectDisposedException("OwnedMediaPacket")).Memory.Slice(0, _length);

	public OwnedMediaPacket(MediaCodec codec, MediaTimestamp timestamp, long sequence, IMemoryOwner<byte> owner, int length)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentOutOfRangeException.ThrowIfNegative(length, "length");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, owner.Memory.Length, "length");
		Codec = codec;
		Timestamp = timestamp;
		Sequence = sequence;
		_owner = owner;
		_length = length;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _owner, null)?.Dispose();
	}
}
