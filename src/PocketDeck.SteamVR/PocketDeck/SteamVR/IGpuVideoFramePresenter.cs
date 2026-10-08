using System;
using PocketDeck.Media;

namespace PocketDeck.SteamVR;

public interface IGpuVideoFramePresenter : IDisposable
{
	string AdapterBackend { get; }

	GpuVideoFrame Present(DecodedVideoFrame frame);
}
