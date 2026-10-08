using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidKeyguardStateService
{
	ValueTask<AndroidKeyguardSnapshot> ProbeAsync(string? deviceKey, CancellationToken cancellationToken);
}
