namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrMenuChange(bool Consumed = false, bool VisibilityChanged = false, bool OpacityChanged = false, OpenVrPlayspaceControlRequest? Playspace = null, OpenVrPhoneInputCommand? PhoneCommand = null);
