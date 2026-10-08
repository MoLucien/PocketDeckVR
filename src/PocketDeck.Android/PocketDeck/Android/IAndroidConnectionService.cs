using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidConnectionService : IAsyncDisposable
{
	AndroidConnectionSnapshot Snapshot { get; }

	event EventHandler<AndroidConnectionChangedEventArgs>? StateChanged;

	ValueTask StartAsync(CancellationToken cancellationToken);

	ValueTask RefreshAsync(CancellationToken cancellationToken);

	ValueTask SelectDeviceAsync(string deviceKey, CancellationToken cancellationToken);

	ValueTask<AndroidWirelessConnectResult> ConnectWirelessAsync(string input, CancellationToken cancellationToken);

	ValueTask<AndroidWirelessConnectResult> EnableWirelessViaUsbAsync(CancellationToken cancellationToken);
}
