using System;
using System.Drawing;

namespace PocketDeck.App.Ui3;

/// <summary>
/// v3 设计令牌：近黑画布 + 分层抬升面板 + 玫瑰主色。
/// 全部取值集中于此，控件只允许引用令牌，禁止就地写死颜色/字号。
/// </summary>
internal static class Tok
{
	// ——— 画布与层级 ———
	public static Color Canvas { get; } = Color.FromArgb(255, 10, 10, 13);

	public static Color Surface { get; } = Color.FromArgb(255, 20, 20, 26);

	public static Color SurfaceAlt { get; } = Color.FromArgb(255, 27, 27, 34);

	public static Color SurfaceHover { get; } = Color.FromArgb(255, 34, 34, 43);

	public static Color SurfacePressed { get; } = Color.FromArgb(255, 40, 40, 50);

	public static Color Well { get; } = Color.FromArgb(255, 15, 15, 20);

	public static Color Line { get; } = Color.FromArgb(255, 38, 38, 47);

	public static Color LineSoft { get; } = Color.FromArgb(255, 29, 29, 37);

	/// <summary>面板顶部 1px 高光（仅抬升面板使用，替代满框描边）。</summary>
	public static Color TopLight { get; } = Color.FromArgb(20, 255, 255, 255);

	// ——— 文本 ———
	public static Color Text1 { get; } = Color.FromArgb(255, 246, 246, 248);

	public static Color Text2 { get; } = Color.FromArgb(255, 163, 163, 176);

	public static Color Text3 { get; } = Color.FromArgb(255, 110, 110, 124);

	public static Color TextOff { get; } = Color.FromArgb(255, 84, 84, 97);

	// ——— 主色与语义色 ———
	public static Color Accent { get; } = Color.FromArgb(255, 255, 77, 109);

	public static Color AccentHi { get; } = Color.FromArgb(255, 255, 107, 133);

	public static Color AccentLo { get; } = Color.FromArgb(255, 224, 58, 91);

	public static Color AccentInk { get; } = Color.FromArgb(255, 255, 255, 255);

	public static Color Ok { get; } = Color.FromArgb(255, 61, 214, 140);

	public static Color Warn { get; } = Color.FromArgb(255, 245, 165, 36);

	public static Color Bad { get; } = Color.FromArgb(255, 255, 92, 108);

	public static Color Info { get; } = Color.FromArgb(255, 90, 169, 255);

	public static Color OnOk { get; } = Color.FromArgb(255, 6, 26, 17);

	// ——— 圆角 ———
	public const int RShell = 14;

	public const int RPanel = 12;

	public const int RTile = 10;

	public const int RCtl = 9;

	public const int RChip = 999;

	// ——— 间距栅格（逻辑像素） ———
	public const int S1 = 4;

	public const int S2 = 8;

	public const int S3 = 12;

	public const int S4 = 16;

	public const int S5 = 20;

	public const int S6 = 24;

	public const int S7 = 32;

	// ——— 结构尺寸 ———
	public const int HeaderHeight = 56;

	public const int TabBarHeight = 46;

	public const int StatusBarHeight = 34;

	public const int ContentPad = 20;

	public const int Gutter = 16;

	public const int RightColumnWidth = 300;

	/// <summary>内容区窄于该宽度时折叠为单列。</summary>
	public const int TwoColumnMinWidth = 880;

	public const int DefaultWidth = 920;

	public const int DefaultHeight = 620;

	public const int MinWidth = 780;

	public const int MinHeight = 540;

	// ——— 字号（磅） ———
	private const string Ui = "Microsoft YaHei UI";

	private const string Mono = "Consolas";

	public static Font Hero(bool bold = true)
	{
		return new Font(Ui, 16f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
	}

	public static Font Title()
	{
		return new Font(Ui, 11f, FontStyle.Bold, GraphicsUnit.Point);
	}

	public static Font Body(bool bold = false)
	{
		return new Font(Ui, 9.25f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
	}

	public static Font Caption(bool bold = false)
	{
		return new Font(Ui, 8.25f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
	}

	public static Font Micro()
	{
		return new Font(Ui, 7.25f, FontStyle.Bold, GraphicsUnit.Point);
	}

	public static Font Readout(bool bold = false)
	{
		return new Font(Mono, 9f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
	}

	public static Font Nav()
	{
		return new Font(Ui, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
	}

	/// <summary>按比例混合到画布上（用于淡底、半透明叠加）。</summary>
	public static Color Tint(Color color, Color background, float alpha)
	{
		float a = Math.Clamp(alpha, 0f, 1f);
		return Color.FromArgb(
			255,
			(int)MathF.Round(color.R * a + background.R * (1f - a)),
			(int)MathF.Round(color.G * a + background.G * (1f - a)),
			(int)MathF.Round(color.B * a + background.B * (1f - a)));
	}

	public static Color Mix(Color from, Color to, float t)
	{
		float k = Math.Clamp(t, 0f, 1f);
		return Color.FromArgb(
			255,
			(int)MathF.Round(from.R + (to.R - from.R) * k),
			(int)MathF.Round(from.G + (to.G - from.G) * k),
			(int)MathF.Round(from.B + (to.B - from.B) * k));
	}

	/// <summary>语义色调 → 颜色。</summary>
	public static Color Tone(Tone tone)
	{
		return tone switch
		{
			Ui3.Tone.Ok => Ok,
			Ui3.Tone.Warn => Warn,
			Ui3.Tone.Bad => Bad,
			Ui3.Tone.Info => Info,
			Ui3.Tone.Accent => Accent,
			Ui3.Tone.Muted => Text3,
			_ => Text2,
		};
	}
}

/// <summary>语义色调。</summary>
internal enum Tone
{
	Neutral,
	Muted,
	Accent,
	Ok,
	Warn,
	Bad,
	Info,
}
