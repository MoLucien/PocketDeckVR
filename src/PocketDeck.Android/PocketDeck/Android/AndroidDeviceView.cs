namespace PocketDeck.Android;

public sealed record AndroidDeviceView(string DeviceKey, string DisplayName, string Model, AndroidDeviceStatus Status, AndroidTransport Transport, bool IsSelected);
