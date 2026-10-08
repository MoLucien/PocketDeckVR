namespace PocketDeck.Settings;

public sealed record AppSettings(int SchemaVersion, ControllerHandPreference ControllerHand, int VideoResolutionPercent, int VideoBitrateMbps, int VideoMaximumFramesPerSecond, UpdateChannel UpdateChannel, bool KeepAwakeWhileGrabbed, bool VrUnlockKeypadEnabled)
{
	public static AppSettings Default { get; } = new AppSettings(3, ControllerHandPreference.Right, 100, 16, 60, UpdateChannel.Beta, KeepAwakeWhileGrabbed: false, VrUnlockKeypadEnabled: false);

	public const int CurrentSchemaVersion = 3;
}
