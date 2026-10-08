using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrControllerHandRouting
{
	public static OpenVrControllerHand Opposite(OpenVrControllerHand hand)
	{
		if (hand == OpenVrControllerHand.Left)
		{
			return OpenVrControllerHand.Right;
		}
		return OpenVrControllerHand.Left;
	}

	public static string InputSourcePath(OpenVrControllerHand hand)
	{
		if (hand == OpenVrControllerHand.Left)
		{
			return "/user/hand/left";
		}
		return "/user/hand/right";
	}

	public static ETrackedControllerRole TrackedRole(OpenVrControllerHand hand)
	{
		if (hand == OpenVrControllerHand.Left)
		{
			return ETrackedControllerRole.LeftHand;
		}
		return ETrackedControllerRole.RightHand;
	}
}
