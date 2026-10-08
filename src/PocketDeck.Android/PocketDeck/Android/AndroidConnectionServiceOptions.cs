using System;

namespace PocketDeck.Android;

internal sealed record AndroidConnectionServiceOptions
{
	public TimeSpan ReadyScanInterval { get; init; } = TimeSpan.FromSeconds(3L);

	public TimeSpan SearchingScanInterval { get; init; } = TimeSpan.FromSeconds(1L);

	public TimeSpan FaultedScanInterval { get; init; } = TimeSpan.FromSeconds(5L);

	public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(15L);
}
