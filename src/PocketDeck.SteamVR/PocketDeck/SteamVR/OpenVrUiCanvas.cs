using System;
using System.IO;

namespace PocketDeck.SteamVR;

internal static class OpenVrUiCanvas
{
	private const string _characters = "0123456789%×+−›X";

	private static readonly byte[] _glyphs = LoadGlyphs();

	public static void Pixel(byte[] pixels, int x, int y, uint color, float coverage)
	{
		if (x < 0 || x >= 360 || y < 0 || y >= pixels.Length / 1440)
		{
			return;
		}
		int num = checked((y * 360 + x) * 4);
		float num2 = (float)(color >> 24) / 255f * coverage;
		float num3 = (float)(int)pixels[checked(num + 3)] / 255f;
		float num4 = num2 + num3 * (1f - num2);
		checked
		{
			if (!(num4 <= 0f))
			{
				for (int i = 0; i < 3; i++)
				{
					byte b = (byte)((color >> 16 - i * 8) & 0xFF);
					pixels[num + i] = (byte)Math.Clamp(unchecked((float)(int)b * num2 + (float)(int)pixels[checked(num + i)] * num3 * (1f - num2)) / num4, 0f, 255f);
				}
				pixels[num + 3] = (byte)Math.Clamp(num4 * 255f, 0f, 255f);
			}
		}
	}

	public static void Rounded(byte[] pixels, int x, int y, int width, int height, float radius, uint color)
	{
		checked
		{
			for (int i = y; i < y + height; i++)
			{
				for (int j = x; j < x + width; j++)
				{
					float num = Math.Max(MathF.Abs((float)j + 0.5f - (float)x - (float)width / 2f) - ((float)width / 2f - radius), 0f);
					float num2 = Math.Max(MathF.Abs((float)i + 0.5f - (float)y - (float)height / 2f) - ((float)height / 2f - radius), 0f);
					float coverage = Math.Clamp(radius + 0.5f - MathF.Sqrt(num * num + num2 * num2), 0f, 1f);
					Pixel(pixels, j, i, color, coverage);
				}
			}
		}
	}

	public static void Card(byte[] pixels, int x, int y, int width, int height, uint color, int radius = 10)
	{
		Rounded(pixels, x, y, width, height, radius, 4280099629u);
		checked
		{
			Rounded(pixels, x + 1, y + 1, width - 2, height - 2, radius - 1, color);
		}
	}

	public static void Text(byte[] pixels, string text, int x, int y, float size = 1f)
	{
		checked
		{
			int num = (int)(24f * size);
			int num2 = (int)(32f * size);
			foreach (char value in text)
			{
				int num3 = "0123456789%×+−›X".IndexOf(value, StringComparison.Ordinal);
				if (num3 >= 0)
				{
					for (int j = 0; j < num2; j++)
					{
						for (int k = 0; k < num; k++)
						{
							float num4 = (float)k / size;
							float num5 = (float)j / size;
							int num6 = Math.Min(23, (int)num4);
							int num7 = Math.Min(31, (int)num5);
							int num8 = Math.Min(23, num6 + 1);
							int num9 = Math.Min(31, num7 + 1);
							float num10 = num4 - (float)num6;
							float num11 = num5 - (float)num7;
							int num12 = num3 * 32 * 24;
							float num13;
							float num14;
							unchecked
							{
								num13 = (float)(int)_glyphs[checked(num12 + num7 * 24 + num6)] * (1f - num10) + (float)(int)_glyphs[checked(num12 + num7 * 24 + num8)] * num10;
								num14 = (float)(int)_glyphs[checked(num12 + num9 * 24 + num6)] * (1f - num10) + (float)(int)_glyphs[checked(num12 + num9 * 24 + num8)] * num10;
							}
							Pixel(pixels, x + k, y + j, 4294309882u, (num13 * (1f - num11) + num14 * num11) / 255f);
						}
					}
				}
				x += (int)(18f * size);
			}
		}
	}

	private static byte[] LoadGlyphs()
	{
		using Stream stream = typeof(OpenVrUiCanvas).Assembly.GetManifestResourceStream("PocketDeck.SteamVR.ControlGlyphs") ?? throw new InvalidOperationException("Control glyph resource missing.");
		byte[] array = new byte[checked("0123456789%×+−›X".Length * 24 * 32)];
		stream.ReadExactly(array);
		return array;
	}
}
