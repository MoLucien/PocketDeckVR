using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Settings;

public interface IAppSettingsService : IDisposable
{
	AppSettingsSnapshot Snapshot { get; }

	event EventHandler<AppSettingsChangedEventArgs>? Changed;

	ValueTask InitializeAsync(CancellationToken cancellationToken);

	ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
