namespace PocketDeck.Android;

public enum AndroidConnectionState
{
	Created,
	Discovering,
	WaitingForDevice,
	AuthorizationRequired,
	Offline,
	Connecting,
	Ready,
	Reconnecting,
	Faulted,
	Stopping,
	Stopped
}
