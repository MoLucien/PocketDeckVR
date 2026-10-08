namespace PocketDeck.SteamVR;

internal sealed class OpenVrGrabAwakePolicy
{
	private const long _userActivityIntervalMilliseconds = 3000L;

	private bool _previouslyGrabbed;

	private long _nextUserActivityAt;

	public OpenVrGrabAwakeAction NextAction(bool grabbed, bool keepAwakeWhileGrabbed, long now)
	{
		bool flag = grabbed && !_previouslyGrabbed;
		bool flag2 = (grabbed & keepAwakeWhileGrabbed) && !flag && now >= _nextUserActivityAt;
		if (flag | flag2)
		{
			_nextUserActivityAt = checked(now + 3000);
		}
		else if (!grabbed)
		{
			_nextUserActivityAt = 0L;
		}
		_previouslyGrabbed = grabbed;
		if (!flag)
		{
			if (!flag2)
			{
				return OpenVrGrabAwakeAction.None;
			}
			return OpenVrGrabAwakeAction.UserActivity;
		}
		return OpenVrGrabAwakeAction.Wake;
	}
}
