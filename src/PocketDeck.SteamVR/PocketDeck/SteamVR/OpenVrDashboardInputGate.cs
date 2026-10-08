namespace PocketDeck.SteamVR;

internal sealed class OpenVrDashboardInputGate
{
	private bool _waitingForRelease;

	public void Apply(bool dashboard, OpenVrPlayspaceInputSample sample, bool sampleIsFresh, out bool drag, out bool reset)
	{
		drag = sample.DragPressed;
		reset = sample.ResetPressed;
		if (dashboard || sample.DashboardVisible)
		{
			_waitingForRelease = true;
		}
		else if (sampleIsFresh && sample.IsValid && !drag && !reset)
		{
			_waitingForRelease = false;
		}
		if (dashboard || sample.DashboardVisible || _waitingForRelease || !sample.IsValid)
		{
			drag = false;
			reset = false;
		}
	}
}
