using System;
using System.Collections.Generic;
using System.Linq;

namespace PocketDeck.Android;

internal static class AndroidDeviceSelector
{
	public static AdbDeviceRecord? Select(IReadOnlyList<AdbDeviceRecord> devices, string? preferredDeviceKey)
	{
		ArgumentNullException.ThrowIfNull(devices, "devices");
		AdbDeviceRecord[] source = devices.Where((AdbDeviceRecord device) => device.Status == AndroidDeviceStatus.Ready && (device.Transport == AndroidTransport.Usb || device.Transport == AndroidTransport.Network)).ToArray();
		if (!string.IsNullOrWhiteSpace(preferredDeviceKey))
		{
			AdbDeviceRecord adbDeviceRecord = source.FirstOrDefault((AdbDeviceRecord device) => string.Equals(device.DeviceKey, preferredDeviceKey, StringComparison.Ordinal));
			if ((object)adbDeviceRecord != null)
			{
				return adbDeviceRecord;
			}
		}
		return source.OrderBy((AdbDeviceRecord device) => device.Model, StringComparer.OrdinalIgnoreCase).ThenBy((AdbDeviceRecord device) => device.DeviceKey, StringComparer.Ordinal).FirstOrDefault();
	}
}
