using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal interface IAdbClient
{
	ValueTask<AdbListResult> ListDevicesAsync(CancellationToken cancellationToken);

	ValueTask<AdbProbeResult> ProbeAsync(string serial, CancellationToken cancellationToken);

	ValueTask<AdbCommandResult> ConnectWirelessAsync(string endpoint, CancellationToken cancellationToken);

	ValueTask<AdbCommandResult> PairWirelessAsync(string endpoint, string pairingCode, CancellationToken cancellationToken);

	ValueTask<AdbCommandResult> EnableTcpipAsync(string serial, int port, CancellationToken cancellationToken);

	ValueTask<AdbCommandResult> ReadWlanInfoAsync(string serial, CancellationToken cancellationToken);
}
