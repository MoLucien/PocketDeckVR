namespace PocketDeck.Android;

internal sealed record AdbServerShutdownResult(bool Succeeded, string ReasonCode, string Message);
