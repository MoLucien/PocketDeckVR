using System;
using System.Collections.Generic;

namespace PocketDeck.Settings;

public static class AppSettingsPolicy
{
	private static readonly int[] _bitrates = new int[5] { 8, 12, 16, 24, 32 };

	private static readonly int[] _frameRates = new int[4] { 25, 30, 45, 60 };

	public static IReadOnlyList<int> VideoBitratesMbps => _bitrates;

	public static IReadOnlyList<int> VideoMaximumFrameRates => _frameRates;

	public static AppSettings Normalize(AppSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings, "settings");
		AppSettings appSettings = AppSettings.Default;
		int videoResolutionPercent = settings.VideoResolutionPercent;
		int videoBitrateMbps = settings.VideoBitrateMbps;
		int videoMaximumFramesPerSecond = settings.VideoMaximumFramesPerSecond;
		return settings with
		{
			SchemaVersion = 3,
			ControllerHand = (Enum.IsDefined(settings.ControllerHand) ? settings.ControllerHand : appSettings.ControllerHand),
			VideoResolutionPercent = ((videoResolutionPercent >= 1 && videoResolutionPercent <= 100) ? videoResolutionPercent : appSettings.VideoResolutionPercent),
			VideoBitrateMbps = (_bitrates.Contains(videoBitrateMbps) ? videoBitrateMbps : appSettings.VideoBitrateMbps),
			VideoMaximumFramesPerSecond = (_frameRates.Contains(videoMaximumFramesPerSecond) ? videoMaximumFramesPerSecond : appSettings.VideoMaximumFramesPerSecond),
			UpdateChannel = UpdateChannel.Beta
		};
	}
}
