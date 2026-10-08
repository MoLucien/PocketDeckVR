using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidVideoProbeService
{
	ValueTask<AndroidVideoProbeResult> ProbeAsync(string? deviceKey, CancellationToken cancellationToken);
}
