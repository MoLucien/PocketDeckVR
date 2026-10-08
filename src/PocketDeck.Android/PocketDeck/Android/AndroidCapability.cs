namespace PocketDeck.Android;

public readonly record struct AndroidCapability(AndroidCapabilityState State, string ReasonCode, string Message)
{
	public bool IsAvailable => State == AndroidCapabilityState.Available;
}
