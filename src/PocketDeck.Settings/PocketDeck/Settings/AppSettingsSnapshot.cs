namespace PocketDeck.Settings;

public sealed record AppSettingsSnapshot(AppSettings Value, string ReasonCode, string Message, bool IsPersisted);
