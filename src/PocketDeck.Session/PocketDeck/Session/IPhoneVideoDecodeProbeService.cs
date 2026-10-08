using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;

namespace PocketDeck.Session;

public interface IPhoneVideoDecodeProbeService
{
	ValueTask<PhoneVideoDecodeProbeResult> ProbeAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken);
}
