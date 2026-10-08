using System;

namespace PocketDeck.Media;

public static class AudioBufferPolicy
{
	public static readonly TimeSpan MaximumBufferedDuration = TimeSpan.FromMilliseconds(160L);

	public static bool ShouldReset(TimeSpan bufferedDuration)
	{
		return bufferedDuration > MaximumBufferedDuration;
	}
}
