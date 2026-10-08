using System;

namespace PocketDeck.Media;

public interface IAudioDecoder : IAsyncDisposable
{
	MediaCodec Codec { get; }

	int SampleRate { get; }

	int Channels { get; }

	DecodedAudioFrame Decode(ReadOnlySpan<byte> encodedPacket, long presentationTimeMicroseconds);
}
