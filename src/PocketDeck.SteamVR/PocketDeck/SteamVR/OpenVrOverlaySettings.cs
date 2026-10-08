namespace PocketDeck.SteamVR;

public sealed record OpenVrOverlaySettings(string Key, string Name, float WidthMeters = 0.65f, float DistanceMeters = 1.1f)
{
	public static OpenVrOverlaySettings PhoneDefault { get; } = new OpenVrOverlaySettings("io.github.vrphonescreen.overlay.phone", "VRPhoneScreen Overlay - Phone");
}
