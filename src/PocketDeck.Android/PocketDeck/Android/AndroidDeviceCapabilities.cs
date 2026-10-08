namespace PocketDeck.Android;

public sealed record AndroidDeviceCapabilities(AndroidCapability Video, AndroidCapability Control, AndroidCapability InternalAudio)
{
	public static AndroidDeviceCapabilities Unknown { get; } = new AndroidDeviceCapabilities(new AndroidCapability(AndroidCapabilityState.Unknown, "ANDROID_VIDEO_CAPABILITY_UNKNOWN", "尚未读取 Android SDK 版本"), new AndroidCapability(AndroidCapabilityState.Unknown, "ANDROID_CONTROL_CAPABILITY_UNKNOWN", "尚未读取 Android SDK 版本"), new AndroidCapability(AndroidCapabilityState.Unknown, "ANDROID_AUDIO_CAPABILITY_UNKNOWN", "尚未读取 Android SDK 版本"));
}
