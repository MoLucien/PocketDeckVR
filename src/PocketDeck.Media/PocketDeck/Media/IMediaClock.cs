using System;

namespace PocketDeck.Media;

public interface IMediaClock
{
	MediaTimestamp Current { get; }

	TimeSpan GetDelayUntil(MediaTimestamp timestamp);
}
