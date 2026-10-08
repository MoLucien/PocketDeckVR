using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidVideoSessionFactory
{
	ValueTask<IAndroidVideoSession> OpenAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken);
}
