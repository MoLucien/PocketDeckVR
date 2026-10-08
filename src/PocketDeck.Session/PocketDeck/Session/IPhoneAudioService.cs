using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Session;

public interface IPhoneAudioService : IAsyncDisposable
{
	PhoneAudioSnapshot Snapshot { get; }

	PhoneAudioSnapshot DiagnosticSnapshot { get; }

	event EventHandler<PhoneAudioChangedEventArgs>? StateChanged;

	ValueTask StartAsync(string? deviceKey, CancellationToken cancellationToken);

	ValueTask StopAsync(CancellationToken cancellationToken);
}
