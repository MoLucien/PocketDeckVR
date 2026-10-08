using System;

namespace PocketDeck.Media;

public interface IH264VideoDecoder : IDisposable
{
	string BackendName { get; }

	DecodedVideoFrame? DecodePacket(ReadOnlySpan<byte> annexBPayload, long presentationTimeMicroseconds, bool keyFrame);

	DecodedVideoFrame? Drain();
}
