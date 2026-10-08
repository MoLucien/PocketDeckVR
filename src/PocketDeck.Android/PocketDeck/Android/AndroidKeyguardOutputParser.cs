using System;
using System.Linq;

namespace PocketDeck.Android;

internal static class AndroidKeyguardOutputParser
{
	private static readonly string[] _lockedMarkers = new string[6] { "showing=true", "showingandnotoccluded=true", "mshowinglockscreen=true", "isstatusbarkeyguard=true", "keyguardshowing=true", "devicelocked=true" };

	private static readonly string[] _unlockedMarkers = new string[6] { "showing=false", "showingandnotoccluded=false", "mshowinglockscreen=false", "isstatusbarkeyguard=false", "keyguardshowing=false", "devicelocked=false" };

	public static AndroidKeyguardState Parse(string output)
	{
		ArgumentNullException.ThrowIfNull(output, "output");
		bool flag = false;
		string[] array = output.Split('\r', '\n');
		foreach (string text in array)
		{
			string line = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
			if (IsKeyguardLine(line))
			{
				if (_lockedMarkers.Any((string marker) => line.Contains(marker, StringComparison.Ordinal)))
				{
					return AndroidKeyguardState.Locked;
				}
				flag |= _unlockedMarkers.Any((string marker) => line.Contains(marker, StringComparison.Ordinal));
			}
		}
		if (!flag)
		{
			return AndroidKeyguardState.Unknown;
		}
		return AndroidKeyguardState.Unlocked;
	}

	private static bool IsKeyguardLine(string line)
	{
		if (!line.Contains("keyguard", StringComparison.Ordinal) && !line.Contains("lockscreen", StringComparison.Ordinal) && !line.StartsWith("showing=", StringComparison.Ordinal) && !line.StartsWith("showingandnotoccluded=", StringComparison.Ordinal))
		{
			return line.StartsWith("devicelocked=", StringComparison.Ordinal);
		}
		return true;
	}
}
