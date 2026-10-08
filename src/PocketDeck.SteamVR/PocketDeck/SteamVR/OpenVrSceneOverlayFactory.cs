namespace PocketDeck.SteamVR;

public static class OpenVrSceneOverlayFactory
{
	public static IOpenVrSceneOverlay Create(OpenVrOverlaySettings? settings = null)
	{
		return new OpenVrSceneOverlay(settings ?? OpenVrOverlaySettings.PhoneDefault);
	}
}
