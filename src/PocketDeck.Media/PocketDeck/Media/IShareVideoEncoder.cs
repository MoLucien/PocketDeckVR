using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Media;

public interface IShareVideoEncoder : IAsyncDisposable
{
	ShareVideoProfile Profile { get; }

	ValueTask RequestKeyFrameAsync(CancellationToken cancellationToken);
}
