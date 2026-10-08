using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public interface IAndroidAudioSession : IAsyncDisposable
{
	string DeviceKey { get; }

	string DeviceName { get; }

	AndroidAudioCodec Codec { get; }

	ValueTask<AndroidAudioStreamItem> ReadNextAsync(CancellationToken cancellationToken);
}
