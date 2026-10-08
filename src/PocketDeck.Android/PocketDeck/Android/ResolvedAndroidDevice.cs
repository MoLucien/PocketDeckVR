namespace PocketDeck.Android;

internal readonly record struct ResolvedAndroidDevice(string DeviceKey, string Serial, string DisplayName, long SessionEpoch);
