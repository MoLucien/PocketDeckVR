namespace PocketDeck.Android;

public readonly record struct AndroidOperationResult(bool Succeeded, string ReasonCode, string Message);
