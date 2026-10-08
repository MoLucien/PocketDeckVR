namespace PocketDeck.SteamVR;

internal static class OpenVrTouchGestureGate
{
	private const float _dragStartThreshold = 0.006f;

	private const float _moveThreshold = 0.001f;

	private const long _minimumMoveIntervalMilliseconds = 12L;

	public static bool ShouldStartDrag(float startX, float startY, float currentX, float currentY)
	{
		float num = currentX - startX;
		float num2 = currentY - startY;
		return num * num + num2 * num2 >= 3.6E-05f;
	}

	public static bool ShouldSendMove(float lastX, float lastY, float currentX, float currentY, long elapsedMilliseconds)
	{
		if (elapsedMilliseconds < 12)
		{
			return false;
		}
		float num = currentX - lastX;
		float num2 = currentY - lastY;
		return num * num + num2 * num2 >= 1.0000001E-06f;
	}
}
