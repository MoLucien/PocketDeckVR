namespace PocketDeck.Android;

public sealed record AndroidKeyguardSnapshot(AndroidKeyguardState State, string ReasonCode, string Message);
