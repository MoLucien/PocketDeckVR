using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Contracts;

public interface ISharingSession : IAsyncDisposable
{
	SharingSessionSnapshot Snapshot { get; }

	ValueTask StartAsync(SharingMode mode, SharingRole role, CancellationToken cancellationToken);

	ValueTask StopAsync(CancellationToken cancellationToken);
}
