using System;

namespace PocketDeck.Android;

public interface IAndroidConnectionLogSink : IAsyncDisposable
{
	string? CurrentLogPath { get; }

	bool TryWrite(AndroidConnectionLogEntry entry);
}
