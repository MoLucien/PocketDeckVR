using System;
using System.IO;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneMenuLabels
{
	private const int _width = 160;

	private const int _height = 32;

	private static readonly byte[] _alpha = Load();

	public static void Draw(byte[] pixels, int label, int x, int y)
	{
		checked
		{
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 160; j++)
				{
					int num = _alpha[(label * 32 + i) * 160 + j];
					int num2 = x + j;
					int num3 = y + i;
					if (num != 0 && num2 >= 0 && num2 < 360 && num3 >= 0 && num3 < unchecked(pixels.Length / 4 / 360))
					{
						OpenVrUiCanvas.Pixel(pixels, num2, num3, 4294309882u, (float)num / 255f);
					}
				}
			}
		}
	}

	private static byte[] Load()
	{
		using Stream stream = typeof(OpenVrPhoneMenuLabels).Assembly.GetManifestResourceStream("PocketDeck.SteamVR.PhoneMenuLabels") ?? throw new InvalidOperationException("Menu label resource missing.");
		byte[] array = new byte[46080];
		stream.ReadExactly(array);
		return array;
	}
}
