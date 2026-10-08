using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidAudioSessionFactory
{
	ValueTask<IAndroidAudioSession> OpenAsync(string? deviceKey, AndroidAudioOptions options, CancellationToken cancellationToken);
}
