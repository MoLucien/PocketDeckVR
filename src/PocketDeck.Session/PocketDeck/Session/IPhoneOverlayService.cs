using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;
using PocketDeck.SteamVR;

namespace PocketDeck.Session;

public interface IPhoneOverlayService : IAsyncDisposable
{
	PhoneOverlaySnapshot Snapshot { get; }

	PhoneOverlaySnapshot DiagnosticSnapshot { get; }

	event EventHandler<PhoneOverlayChangedEventArgs>? StateChanged;

	void ConfigureLockScreenFeatures(bool keepAwakeWhileGrabbed, bool unlockKeypadEnabled);

	ValueTask StartAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken);

	OpenVrBindingResult OpenBindingUi();

	bool HasSavedBindingChanged();

	OpenVrBindingResult ReloadLocalBinding();

	ValueTask StopAsync(CancellationToken cancellationToken);
}
