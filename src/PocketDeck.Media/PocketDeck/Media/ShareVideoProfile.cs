using System;

namespace PocketDeck.Media;

public sealed record ShareVideoProfile(int MaximumWidth, int MaximumHeight, int TargetFramesPerSecond, int TargetBitrateBitsPerSecond, int MaximumBitrateBitsPerSecond)
{
	public static ShareVideoProfile InitialLandscape { get; } = new ShareVideoProfile(1920, 1080, 60, 6000000, 8000000);

	public static ShareVideoProfile InitialPortrait { get; } = new ShareVideoProfile(1080, 1920, 60, 6000000, 8000000);

	public void Validate()
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(MaximumWidth, 1, "MaximumWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(MaximumHeight, 1, "MaximumHeight");
		ArgumentOutOfRangeException.ThrowIfLessThan(TargetFramesPerSecond, 1, "TargetFramesPerSecond");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(TargetFramesPerSecond, 60, "TargetFramesPerSecond");
		ArgumentOutOfRangeException.ThrowIfLessThan(TargetBitrateBitsPerSecond, 1, "TargetBitrateBitsPerSecond");
		ArgumentOutOfRangeException.ThrowIfLessThan(MaximumBitrateBitsPerSecond, TargetBitrateBitsPerSecond, "MaximumBitrateBitsPerSecond");
	}
}
