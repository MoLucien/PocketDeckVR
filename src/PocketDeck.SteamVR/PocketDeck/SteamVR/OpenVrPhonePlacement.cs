using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhonePlacement
{
	public static HmdMatrix34_t InFrontOf(HmdMatrix34_t head, float distance)
	{
		HmdMatrix34_t right = OpenVrTransformMath.Identity();
		right.m11 = 0f - distance;
		return OpenVrTransformMath.Multiply(head, right);
	}
}
