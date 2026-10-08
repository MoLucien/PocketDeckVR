namespace PocketDeck.SteamVR;

public static class GpuVideoFramePresenterFactory
{
	public static IGpuVideoFramePresenter CreateD3D11(long? adapterLuid = null)
	{
		return new D3D11VideoFramePresenter(adapterLuid);
	}
}
