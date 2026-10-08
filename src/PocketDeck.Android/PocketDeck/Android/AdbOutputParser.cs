using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

namespace PocketDeck.Android;

internal static partial class AdbOutputParser
{
	public static IReadOnlyList<AdbDeviceRecord> ParseDevices(string output)
	{
		ArgumentNullException.ThrowIfNull(output, "output");
		List<AdbDeviceRecord> list = new List<AdbDeviceRecord>();
		string[] array = output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.Length == 0 || text2.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) || text2.StartsWith('*'))
			{
				continue;
			}
			int num = IndexOfWhitespace(text2);
			if (num > 0)
			{
				string serial = text2.Substring(0, num);
				string text3 = text2.Substring(num).TrimStart();
				string text4 = ReadState(text3);
				Dictionary<string, string> properties = PropertyPattern().Matches(text3).ToDictionary((Match match) => match.Groups["name"].Value, (Match match) => match.Groups["value"].Value, StringComparer.OrdinalIgnoreCase);
				AndroidTransport transport = ClassifyTransport(serial, properties);
				list.Add(new AdbDeviceRecord(serial, CreateDeviceKey(serial), text4, ReadProperty(properties, "product"), ReadProperty(properties, "model").Replace('_', ' '), ReadProperty(properties, "device"), ClassifyStatus(text4), transport));
			}
		}
		return list;
	}

	public static IReadOnlyDictionary<string, string> ParseProperties(string output)
	{
		ArgumentNullException.ThrowIfNull(output, "output");
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string input = output.Replace("\r", string.Empty, StringComparison.Ordinal);
		foreach (Match item in GetPropertyPattern().Matches(input))
		{
			dictionary[item.Groups["name"].Value] = item.Groups["value"].Value;
		}
		return dictionary;
	}

	public static string CreateDeviceKey(string serial)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serial, "serial");
		byte[] array = SHA256.HashData(Encoding.UTF8.GetBytes(serial));
		return Convert.ToHexStringLower(array.AsSpan(0, 6));
	}

	private static int IndexOfWhitespace(string value)
	{
		for (int i = 0; i < value.Length; i = checked(i + 1))
		{
			if (char.IsWhiteSpace(value[i]))
			{
				return i;
			}
		}
		return -1;
	}

	private static string ReadState(string remainder)
	{
		if (remainder.StartsWith("no permissions", StringComparison.OrdinalIgnoreCase))
		{
			return "no permissions";
		}
		int num = IndexOfWhitespace(remainder);
		if (num >= 0)
		{
			return remainder.Substring(0, num);
		}
		return remainder;
	}

	private static AndroidDeviceStatus ClassifyStatus(string state)
	{
		return state.ToLowerInvariant() switch
		{
			"device" => AndroidDeviceStatus.Ready, 
			"unauthorized" => AndroidDeviceStatus.Unauthorized, 
			"offline" => AndroidDeviceStatus.Offline, 
			"no permissions" => AndroidDeviceStatus.NoPermissions, 
			_ => AndroidDeviceStatus.Unknown, 
		};
	}

	private static AndroidTransport ClassifyTransport(string serial, Dictionary<string, string> properties)
	{
		if (serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
		{
			return AndroidTransport.Emulator;
		}
		if (properties.ContainsKey("usb") || (!serial.Contains(':') && !serial.Contains("._adb-tls-", StringComparison.OrdinalIgnoreCase)))
		{
			return AndroidTransport.Usb;
		}
		if (serial.Contains(':') || serial.Contains("._adb-tls-", StringComparison.OrdinalIgnoreCase))
		{
			return AndroidTransport.Network;
		}
		return AndroidTransport.Unknown;
	}

	private static string ReadProperty(Dictionary<string, string> properties, string name)
	{
		if (!properties.TryGetValue(name, out string value))
		{
			return string.Empty;
		}
		return value;
	}

	[GeneratedRegex("(?<name>[A-Za-z0-9_.-]+):(?<value>[^\\s]+)", RegexOptions.CultureInvariant)]
	private static partial Regex PropertyPattern();

	[GeneratedRegex("^\\[(?<name>[^\\]]+)\\]: \\[(?<value>.*)\\]$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
	private static partial Regex GetPropertyPattern();
}
