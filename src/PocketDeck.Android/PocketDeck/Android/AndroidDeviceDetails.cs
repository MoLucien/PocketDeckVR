namespace PocketDeck.Android;

public sealed record AndroidDeviceDetails(string DeviceKey, string Manufacturer, string Brand, string Model, string DeviceCodeName, string AndroidVersion, int? AndroidSdk, string CpuAbi, AndroidTransport Transport)
{
	public AndroidDeviceCapabilities Capabilities { get; init; } = AndroidDeviceCapabilities.Unknown;

	public int NativeDisplayWidth { get; init; }

	public int NativeDisplayHeight { get; init; }
}
