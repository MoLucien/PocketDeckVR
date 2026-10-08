using System;

namespace PocketDeck.Media;

public static class VideoDecoderFactory
{
	public static IH264VideoDecoder CreateMediaFoundationH264(int width, int height, int framesPerSecond, ReadOnlySpan<byte> codecConfiguration)
	{
		return new MediaFoundationH264Decoder(width, height, framesPerSecond, codecConfiguration);
	}
}
