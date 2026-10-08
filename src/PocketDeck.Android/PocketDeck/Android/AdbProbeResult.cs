using System;

namespace PocketDeck.Android;

internal sealed record AdbProbeResult(bool Succeeded, string ReasonCode, string Message, string Manufacturer, string Brand, string Model, string DeviceCodeName, string AndroidVersion, int? AndroidSdk, string CpuAbi, int DisplayWidth, int DisplayHeight, TimeSpan Duration);
