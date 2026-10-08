using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidVideoSession : IAsyncDisposable
{
	string DeviceKey { get; }

	string DeviceName { get; }

	AndroidVideoCodec Codec { get; }

	ValueTask<AndroidVideoStreamItem> ReadNextAsync(CancellationToken cancellationToken);
}
