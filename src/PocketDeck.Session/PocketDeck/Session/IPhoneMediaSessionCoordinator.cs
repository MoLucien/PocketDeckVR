using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;

namespace PocketDeck.Session;

public interface IPhoneMediaSessionCoordinator : IAsyncDisposable
{
	PhoneMediaSessionSnapshot Snapshot { get; }

	event EventHandler<PhoneMediaSessionChangedEventArgs>? StateChanged;

	ValueTask StartAsync(string? deviceKey, AndroidVideoOptions videoOptions, CancellationToken cancellationToken);

	ValueTask RestartScreenAsync(string? deviceKey, AndroidVideoOptions videoOptions, Func<CancellationToken, ValueTask>? whilePaused, CancellationToken cancellationToken);

	ValueTask StopAsync(CancellationToken cancellationToken);
}
