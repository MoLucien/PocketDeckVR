using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidControlSessionFactory
{
	ValueTask<IAndroidControlSession> OpenAsync(string? deviceKey, CancellationToken cancellationToken);
}
