namespace PocketDeck.Android;

public static class AndroidCapabilityEvaluator
{
	private const int _minimumScrcpySdk = 21;

	private const int _minimumAudioSdk = 30;

	public static AndroidDeviceCapabilities Evaluate(int? androidSdk)
	{
		if (!androidSdk.HasValue)
		{
			return AndroidDeviceCapabilities.Unknown;
		}
		AndroidCapability video = ((androidSdk >= 21) ? Available("ANDROID_VIDEO_CAPABILITY_AVAILABLE", "支持 scrcpy 视频协议") : Unavailable("ANDROID_VIDEO_REQUIRES_API_21", "视频要求 Android 5.0 / API 21 或更高版本"));
		AndroidCapability control = ((androidSdk >= 21) ? Available("ANDROID_CONTROL_CAPABILITY_AVAILABLE", "支持 Android 输入注入协议，实际权限由系统决定") : Unavailable("ANDROID_CONTROL_REQUIRES_API_21", "控制要求 Android 5.0 / API 21 或更高版本"));
		AndroidCapability internalAudio = ((androidSdk >= 30) ? Available("ANDROID_AUDIO_CAPABILITY_AVAILABLE", "支持 Android 内部音频捕获，应用仍可拒绝录音") : Unavailable("ANDROID_AUDIO_REQUIRES_API_30", "内部音频要求 Android 11 / API 30 或更高版本"));
		return new AndroidDeviceCapabilities(video, control, internalAudio);
	}

	private static AndroidCapability Available(string code, string message)
	{
		return new AndroidCapability(AndroidCapabilityState.Available, code, message);
	}

	private static AndroidCapability Unavailable(string code, string message)
	{
		return new AndroidCapability(AndroidCapabilityState.Unavailable, code, message);
	}
}
