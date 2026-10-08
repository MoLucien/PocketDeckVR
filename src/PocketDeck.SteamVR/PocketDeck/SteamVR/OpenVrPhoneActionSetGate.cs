namespace PocketDeck.SteamVR;

internal sealed class OpenVrPhoneActionSetGate
{
	private const int _releasedPollsRequired = 2;

	private const int _inactivePollsBeforeFallback = 3;

	private int _releasedPolls;

	private int _inactivePolls;

	public bool ShouldEnable(bool targetVisible, bool rawStateActive, bool rawPressed)
	{
		if (!targetVisible)
		{
			Reset();
			return false;
		}
		checked
		{
			if (!rawStateActive)
			{
				_releasedPolls = 0;
				_inactivePolls++;
				return _inactivePolls >= 3;
			}
			_inactivePolls = 0;
			if (rawPressed)
			{
				_releasedPolls = 0;
				return false;
			}
			_releasedPolls++;
			return _releasedPolls >= 2;
		}
	}

	public void Reset()
	{
		_releasedPolls = 0;
		_inactivePolls = 0;
	}
}
