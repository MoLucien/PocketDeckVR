namespace PocketDeck.Android;

public sealed record AndroidMediaPauseLease(string? DeviceKey, bool ResumeRequired, string ReasonCode, string Message);
