using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal interface IAdbServerShutdown
{
	ValueTask<AdbServerShutdownResult> StopAsync(CancellationToken cancellationToken);
}
