namespace PocketDeck.Android;

internal sealed record AndroidResourceValidationResult(bool Succeeded, string ReasonCode, string Message, string ExecutablePath);
