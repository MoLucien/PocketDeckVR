using System;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class NullAndroidConnectionLog : IAndroidConnectionLogSink, IAsyncDisposable
{
	public static NullAndroidConnectionLog Instance { get; } = new NullAndroidConnectionLog();

	public string? CurrentLogPath => null;

	private NullAndroidConnectionLog()
	{
	}

	public bool TryWrite(AndroidConnectionLogEntry entry)
	{
		return false;
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}
}
