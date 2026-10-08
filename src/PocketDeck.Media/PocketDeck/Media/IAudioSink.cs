using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Media;

public interface IAudioSink : IAsyncDisposable
{
	string OutputDeviceId { get; }

	TimeSpan BufferedDuration { get; }

	ValueTask WriteAsync(ReadOnlyMemory<short> interleavedPcm, CancellationToken cancellationToken);

	void Clear();
}
