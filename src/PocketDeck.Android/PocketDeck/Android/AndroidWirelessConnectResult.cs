namespace PocketDeck.Android;

public sealed record AndroidWirelessConnectResult(bool Succeeded, string ReasonCode, string Message, string? Endpoint = null);
