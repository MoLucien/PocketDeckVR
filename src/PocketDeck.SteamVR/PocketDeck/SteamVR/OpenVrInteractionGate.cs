namespace PocketDeck.SteamVR;

internal static class OpenVrInteractionGate
{
	public static bool CanAcceptPhoneButtons(bool phoneHit, bool grabbed)
	{
		if (phoneHit)
		{
			return !grabbed;
		}
		return false;
	}

	public static bool ShouldEmitPhoneButton(bool acceptsButtons, bool currentPressed, bool previousPressed)
	{
		if (acceptsButtons & currentPressed)
		{
			return !previousPressed;
		}
		return false;
	}

	public static bool IsPhoneHit(bool rayIntersectsPhone, bool alreadyGrabbed)
	{
		return !alreadyGrabbed & rayIntersectsPhone;
	}

	public static bool IsVisiblePhoneTarget(bool rayIntersectsPhone, bool pointerVisible, bool alreadyGrabbed)
	{
		if (rayIntersectsPhone & pointerVisible)
		{
			return !alreadyGrabbed;
		}
		return false;
	}

	public static bool CanStartGrab(bool grabPressed, bool previousGrabPressed, bool alreadyGrabbed, bool visiblePhoneHit, bool controllerPoseValid)
	{
		return (grabPressed && !previousGrabPressed && !alreadyGrabbed) & visiblePhoneHit & controllerPoseValid;
	}

	public static bool ShouldEndGrab(bool grabPressed, bool alreadyGrabbed, bool controllerPoseValid)
	{
		if (alreadyGrabbed)
		{
			if (grabPressed)
			{
				return !controllerPoseValid;
			}
			return true;
		}
		return false;
	}
}
