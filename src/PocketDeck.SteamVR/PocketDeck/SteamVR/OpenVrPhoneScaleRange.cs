using System;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneScaleRange
{
	public const float Minimum = 0.2f;

	public const float Maximum = 2.5f;

	public static float Clamp(float scaleFactor)
	{
		return Math.Clamp(scaleFactor, 0.2f, 2.5f);
	}
}
