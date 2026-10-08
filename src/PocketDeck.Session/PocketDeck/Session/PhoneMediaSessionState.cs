namespace PocketDeck.Session;

public enum PhoneMediaSessionState
{
	Stopped,
	Starting,
	Running,
	WaitingForDevice,
	PausingForScreenRestart,
	ScreenRestartPaused,
	ResumingAfterScreenRestart,
	Stopping,
	Faulted
}
