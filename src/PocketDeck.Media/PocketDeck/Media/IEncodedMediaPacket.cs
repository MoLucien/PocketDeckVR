using System;

namespace PocketDeck.Media;

public interface IEncodedMediaPacket : IDisposable
{
	MediaCodec Codec { get; }

	MediaTimestamp Timestamp { get; }

	long Sequence { get; }

	ReadOnlyMemory<byte> Payload { get; }
}
