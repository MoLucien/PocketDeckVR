namespace PocketDeck.Session;

public sealed record PhoneMediaSessionSnapshot(PhoneMediaSessionState State, string ReasonCode, string Message);
