namespace PocketDeck.Contracts;

public enum AppLifecycleState
{
	Created,
	Starting,
	Ready,
	Degraded,
	Stopping,
	Stopped
}
