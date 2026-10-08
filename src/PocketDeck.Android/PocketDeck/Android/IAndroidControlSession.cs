using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

public interface IAndroidControlSession : IAsyncDisposable
{
	string DeviceKey { get; }

	ValueTask SendAsync(PhoneInputCommand command, CancellationToken cancellationToken);
}
