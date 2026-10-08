using System;
using System.Globalization;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneMenuPixels
{
	public static byte[] Dot()
	{
		byte[] array = new byte[16384];
		checked
		{
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = MathF.Sqrt(((float)j - 31.5f) * ((float)j - 31.5f) + ((float)i - 31.5f) * ((float)i - 31.5f));
					int num2 = (i * 64 + j) * 4;
					array[num2] = 70;
					array[num2 + 1] = 142;
					array[num2 + 2] = byte.MaxValue;
					array[num2 + 3] = (byte)(Math.Clamp(31.5f - num, 0f, 1f) * 255f);
					float num3 = MathF.Atan2((float)i - 31.5f, (float)j - 31.5f);
					float num4 = 16f + 4f * Math.Clamp((MathF.Cos(num3 * 8f) - 0.2f) * 2f, 0f, 1f);
					float num5 = Math.Clamp(num4 - num, 0f, 1f) * Math.Clamp(num - 7.5f, 0f, 1f);
					array[num2] = (byte)((float)unchecked((int)array[num2]) * (1f - num5) + 245f * num5);
					array[num2 + 1] = (byte)((float)unchecked((int)array[checked(num2 + 1)]) * (1f - num5) + 247f * num5);
					array[num2 + 2] = (byte)((float)unchecked((int)array[checked(num2 + 2)]) * (1f - num5) + 250f * num5);
				}
			}
			return array;
		}
	}

	public static byte[] Panel(OpenVrMenuVisual value)
	{
		byte[] array = new byte[921600];
		PaintPanel(array, value);
		return array;
	}

	public static void PaintPanel(byte[] pixels, OpenVrMenuVisual value)
	{
		Array.Clear(pixels);
		OpenVrUiCanvas.Card(pixels, 0, 0, 360, value.Keypad ? 376 : 312, 4279046166u, 14);
		OpenVrPhoneMenuLabels.Draw(pixels, 0, 20, 28);
		Toggle(pixels, 28, !value.Hidden);
		OpenVrPhoneMenuLabels.Draw(pixels, 1, 20, 87);
		Text(pixels, value.Opacity.ToString(CultureInfo.InvariantCulture) + "%", 266, 87, 3);
		OpenVrUiCanvas.Rounded(pixels, 24, 140, 312, 6, 3f, 4281152835u);
		int num = checked(Math.Clamp(value.Opacity, 0, 100) * 312) / 100;
		if (num > 0)
		{
			OpenVrUiCanvas.Rounded(pixels, 24, 140, num, 6, 3f, 4282814207u);
		}
		OpenVrUiCanvas.Rounded(pixels, checked(16 + num), 135, 16, 16, 8f, 4294309882u);
		OpenVrPhoneMenuLabels.Draw(pixels, 2, 20, 188);
		Toggle(pixels, 188, value.DragEnabled);
		OpenVrPhoneMenuLabels.Draw(pixels, 3, 20, 258);
		OpenVrUiCanvas.Card(pixels, 176, 248, 48, 50, 4279375390u);
		OpenVrUiCanvas.Card(pixels, 292, 248, 48, 50, 4279375390u);
		Text(pixels, "−", 188, 258, 3);
		Text(pixels, value.Multiplier.ToString("0", CultureInfo.InvariantCulture) + "×", 232, 258, 3);
		Text(pixels, "+", 304, 258, 3);
		if (value.Keypad)
		{
			OpenVrUiCanvas.Card(pixels, 12, 318, 336, 48, value.KeypadExpanded ? 4279840334u : 4279375390u);
			OpenVrPhoneMenuLabels.Draw(pixels, 4, 20, 326);
			Text(pixels, "›", 315, 326, 3);
		}
	}

	public static void PaintKeypad(byte[] pixels, int digitCount)
	{
		Array.Clear(pixels);
		OpenVrUiCanvas.Card(pixels, 0, 0, 360, 320, 4279046166u, 14);
		OpenVrPhoneMenuLabels.Draw(pixels, 4, 20, 8);
		Text(pixels, "X", 326, 8, 3);
		checked
		{
			for (int i = 0; i < digitCount; i++)
			{
				OpenVrUiCanvas.Rounded(pixels, 142 + i * 10, 21, 5, 5, 2.5f, 4282814207u);
			}
			for (int j = 0; j < 4; j++)
			{
				for (int k = 0; k < 3; k++)
				{
					int num = 20 + unchecked(checked(k * 320) / 3);
					int num2 = 50 + j * 62;
					OpenVrUiCanvas.Card(pixels, num + 2, num2 + 2, 102, 58, (j == 3 && k == 2) ? 4280245684u : 4279375390u);
					string text = ((j != 3) ? (j * 3 + k + 1).ToString(CultureInfo.InvariantCulture) : (k switch
					{
						0 => "DEL", 
						1 => "0", 
						_ => "OK", 
					}));
					string text2 = text;
					if (j == 3 && k != 1)
					{
						OpenVrPhoneMenuLabels.Draw(pixels, (k == 0) ? 7 : 8, num + 26, num2 + 14);
					}
					else
					{
						Text(pixels, text2, num + 39, num2 + 9, 4);
					}
				}
			}
		}
	}

	private static void Toggle(byte[] pixels, int top, bool enabled)
	{
		OpenVrUiCanvas.Rounded(pixels, 256, top, 84, 36, 18f, enabled ? 4280122962u : 4279375390u);
		checked
		{
			OpenVrPhoneMenuLabels.Draw(pixels, enabled ? 5 : 6, enabled ? 268 : 290, top + 2);
			OpenVrUiCanvas.Rounded(pixels, enabled ? 308 : 260, top + 4, 28, 28, 14f, 4294309882u);
		}
	}

	private static void Text(byte[] pixels, string text, int x, int y, int scale)
	{
		OpenVrUiCanvas.Text(pixels, text, x, y, (float)scale / 3f);
	}
}
