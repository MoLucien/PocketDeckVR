using System;

namespace PocketDeck.Android;

public sealed record AndroidVideoOptions
{
	public int ResolutionPercent { get; init; } = 100;

	public int MaximumFramesPerSecond { get; init; } = 60;

	public int VideoBitrateBitsPerSecond { get; init; } = 16000000;

	public int MaximumSize { get; init; }

	public bool PowerOnDevice { get; init; } = true;

	internal void Validate()
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(ResolutionPercent, 1, "ResolutionPercent");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(ResolutionPercent, 100, "ResolutionPercent");
		ArgumentOutOfRangeException.ThrowIfLessThan(MaximumFramesPerSecond, 1, "MaximumFramesPerSecond");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumFramesPerSecond, 120, "MaximumFramesPerSecond");
		ArgumentOutOfRangeException.ThrowIfLessThan(VideoBitrateBitsPerSecond, 1000000, "VideoBitrateBitsPerSecond");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(VideoBitrateBitsPerSecond, 100000000, "VideoBitrateBitsPerSecond");
		ArgumentOutOfRangeException.ThrowIfNegative(MaximumSize, "MaximumSize");
	}
}
