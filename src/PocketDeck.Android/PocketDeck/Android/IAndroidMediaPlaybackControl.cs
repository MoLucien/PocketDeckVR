using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidMediaPlaybackControl
{
	ValueTask<AndroidMediaPauseLease> PauseForVrInterruptionAsync(string? deviceKey, CancellationToken cancellationToken);

	ValueTask ResumeAfterVrReadyAsync(AndroidMediaPauseLease lease, CancellationToken cancellationToken);
}
