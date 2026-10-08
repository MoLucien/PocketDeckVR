using System;
using System.Collections.Generic;

namespace PocketDeck.Android;

internal sealed record AdbListResult(IReadOnlyList<AdbDeviceRecord> Devices, TimeSpan Duration, string ToolVersion);
