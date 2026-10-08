namespace PocketDeck.Android;

internal sealed record AdbDeviceRecord(string Serial, string DeviceKey, string StateText, string Product, string Model, string DeviceCodeName, AndroidDeviceStatus Status, AndroidTransport Transport);
