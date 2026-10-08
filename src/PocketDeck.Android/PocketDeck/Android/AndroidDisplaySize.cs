using System;
using System.Globalization;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal readonly record struct AndroidDisplaySize(int Width, int Height)
{
	public static bool TryParseWmSize(string output, out AndroidDisplaySize size)
	{
		size = default;
		AndroidDisplaySize androidDisplaySize = default;
		AndroidDisplaySize androidDisplaySize2 = default;
		string[] array = output.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			int num = text.IndexOf(':');
			if (num >= 0 && TryParseDimensions(text.Substring(checked(num + 1)), out var size2))
			{
				if (text.Contains("Override", StringComparison.OrdinalIgnoreCase))
				{
					androidDisplaySize2 = size2;
				}
				else if (text.Contains("Physical", StringComparison.OrdinalIgnoreCase))
				{
					androidDisplaySize = size2;
				}
			}
		}
		size = ((androidDisplaySize2.Width > 0) ? androidDisplaySize2 : androidDisplaySize);
		if (size.Width > 0)
		{
			return size.Height > 0;
		}
		return false;
	}

	public PhoneInputCommand Map(PhoneInputCommand command)
	{
		if (command.ScreenWidth < 1 || command.ScreenHeight < 1)
		{
			return command;
		}
		bool flag = command.ScreenWidth > command.ScreenHeight;
		int num = Math.Min(Width, Height);
		int num2 = Math.Max(Width, Height);
		return command with
		{
			ScreenWidth = (flag ? num2 : num),
			ScreenHeight = (flag ? num : num2)
		};
	}

	private static bool TryParseDimensions(string text, out AndroidDisplaySize size)
	{
		size = default;
		string text2 = text.Trim();
		int num = text2.IndexOf('x');
		int result = default;
		int result2 = default;
		bool flag = num < 1 || !int.TryParse(text2.Substring(0, num), NumberStyles.None, CultureInfo.InvariantCulture, out result) || !int.TryParse(text2.Substring(checked(num + 1)), NumberStyles.None, CultureInfo.InvariantCulture, out result2);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((result < 1 || result > 65535) ? true : false);
			flag2 = flag3;
		}
		bool flag4 = flag2;
		if (!flag4)
		{
			bool flag3 = ((result2 < 1 || result2 > 65535) ? true : false);
			flag4 = flag3;
		}
		if (flag4)
		{
			return false;
		}
		size = new AndroidDisplaySize(result, result2);
		return true;
	}
}
