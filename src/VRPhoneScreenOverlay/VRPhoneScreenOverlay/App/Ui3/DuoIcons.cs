using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PocketDeck.App.Ui3;

/// <summary>
/// v3 双色调（duotone）图标渲染器。每枚图标固定两层：
///   1) 柔和剪影层 —— 用 soft 色填充的圆角体/圆盘/胶囊等，提供现代、厚实的体量；
///   2) 清晰细节层 —— 用 color 色以 1.5 网格单位描边的轮廓与细节。
/// 全部为 GDI+ 路径，不使用 SVG、外部资源或第三方库；颜色一律由调用方传入，
/// 因此本类不依赖 <see cref="Tok"/>（调用方通常传 Tok.Accent 与 Tok.Tint(Tok.Accent, Tok.Surface, 0.18f)）。
/// 设计网格为 20x20（原点左上，x 向右，y 向下），绘制前统一缩放到 bounds，
/// 描边宽度同样是网格单位，不做额外缩放换算。
/// </summary>
internal static class DuoIcons
{
	/// <summary>细节层标准描边宽度（设计网格单位）。</summary>
	private const float StrokeWidth = 1.5f;

	private const float Grid = 20f;

	/// <summary>把一枚图标绘制到 bounds（通常为正方形）内。</summary>
	/// <param name="g">目标画布。</param>
	/// <param name="icon">图标标识。</param>
	/// <param name="bounds">绘制区域；按短边等比缩放并居中。</param>
	/// <param name="color">清晰描边/强调色。</param>
	/// <param name="soft">柔和剪影填充色。</param>
	public static void Draw(Graphics g, Icon3 icon, RectangleF bounds, Color color, Color soft)
	{
		if (g == null || icon == Icon3.None)
		{
			return;
		}
		float size = Math.Min(bounds.Width, bounds.Height);
		if (float.IsNaN(size) || size <= 0f)
		{
			return;
		}
		float s = size / Grid;
		GraphicsState state = g.Save();
		try
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.TranslateTransform(bounds.X + (bounds.Width - Grid * s) / 2f, bounds.Y + (bounds.Height - Grid * s) / 2f);
			g.ScaleTransform(s, s);
			switch (icon)
			{
			case Icon3.Dashboard:
				DrawDashboard(g, color, soft);
				break;
			case Icon3.Sliders:
				DrawSliders(g, color, soft);
				break;
			case Icon3.Info:
				DrawInfo(g, color, soft);
				break;
			case Icon3.Wifi:
				DrawWifi(g, color, soft);
				break;
			case Icon3.Plug:
				DrawPlug(g, color, soft);
				break;
			case Icon3.Phone:
				DrawPhone(g, color, soft);
				break;
			case Icon3.Monitor:
				DrawMonitor(g, color, soft);
				break;
			case Icon3.Speaker:
				DrawSpeaker(g, color, soft);
				break;
			case Icon3.Gamepad:
				DrawGamepad(g, color, soft);
				break;
			case Icon3.Headset:
				DrawHeadset(g, color, soft);
				break;
			case Icon3.Wand:
				DrawWand(g, color, soft);
				break;
			case Icon3.Undo:
				DrawUndo(g, color);
				break;
			case Icon3.Check:
				DrawCheck(g, color);
				break;
			case Icon3.Minus:
				DrawMinus(g, color);
				break;
			case Icon3.Plus:
				DrawPlus(g, color);
				break;
			case Icon3.Close:
				DrawClose(g, color);
				break;
			case Icon3.Play:
				DrawPlay(g, color, soft);
				break;
			case Icon3.Pause:
				DrawPause(g, color, soft);
				break;
			case Icon3.Dots:
				DrawDots(g, color);
				break;
			case Icon3.Steam:
				DrawSteam(g, color, soft);
				break;
			case Icon3.Gauge:
				DrawGauge(g, color, soft);
				break;
			case Icon3.Clock:
				DrawClock(g, color, soft);
				break;
			case Icon3.Link:
				DrawLink(g, color, soft);
				break;
			case Icon3.Shield:
				DrawShield(g, color, soft);
				break;
			case Icon3.Refresh:
				DrawRefresh(g, color);
				break;
			case Icon3.ChevronRight:
				DrawChevronRight(g, color);
				break;
			case Icon3.Sparkle:
				DrawSparkle(g, color, soft);
				break;
			}
		}
		finally
		{
			g.Restore(state);
		}
	}

	// ——— 图标 ———

	/// <summary>2x2 四个圆角方块，右上角以 color 实心填充作为整套图标的强调动机。</summary>
	private static void DrawDashboard(Graphics g, Color color, Color soft)
	{
		const float start = 2.25f;
		const float box = 7f;
		const float gap = 1.5f;
		float second = start + box + gap;
		for (int i = 0; i < 4; i++)
		{
			float x = ((i % 2) == 0) ? start : second;
			float y = (i < 2) ? start : second;
			using GraphicsPath cell = Sh.Round(new RectangleF(x, y, box, box), 2.1f);
			Sh.Fill(g, cell, (i == 1) ? color : soft);
			Sh.Stroke(g, cell, color, StrokeWidth);
		}
	}

	/// <summary>两条轨道 + 两个旋钮。</summary>
	private static void DrawSliders(Graphics g, Color color, Color soft)
	{
		PointF knobA = new PointF(6.8f, 6.5f);
		PointF knobB = new PointF(13.2f, 13.5f);
		using (GraphicsPath trackA = Sh.Capsule(new RectangleF(3f, 5.5f, 14f, 2f)))
		{
			Sh.Fill(g, trackA, soft);
		}
		using (GraphicsPath trackB = Sh.Capsule(new RectangleF(3f, 12.5f, 14f, 2f)))
		{
			Sh.Fill(g, trackB, soft);
		}
		using (GraphicsPath ringA = Sh.Circle(knobA, 2.5f))
		{
			Sh.Fill(g, ringA, soft);
			Sh.Stroke(g, ringA, color, StrokeWidth);
		}
		using (GraphicsPath ringB = Sh.Circle(knobB, 2.5f))
		{
			Sh.Fill(g, ringB, soft);
			Sh.Stroke(g, ringB, color, StrokeWidth);
		}
		using (GraphicsPath dotA = Sh.Circle(knobA, 0.95f))
		{
			Sh.Fill(g, dotA, color);
		}
		using (GraphicsPath dotB = Sh.Circle(knobB, 0.95f))
		{
			Sh.Fill(g, dotB, color);
		}
	}

	/// <summary>柔和圆盘 + 描边圆环 + 字母 i。</summary>
	private static void DrawInfo(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath disc = Sh.Circle(new PointF(10f, 10f), 7f))
		{
			Sh.Fill(g, disc, soft);
			Sh.Stroke(g, disc, color, StrokeWidth);
		}
		using (GraphicsPath dot = Sh.Circle(new PointF(10f, 6.7f), 0.95f))
		{
			Sh.Fill(g, dot, color);
		}
		using (GraphicsPath stem = Sh.Line(new PointF(10f, 9.4f), new PointF(10f, 13.5f)))
		{
			Sh.Stroke(g, stem, color, StrokeWidth);
		}
	}

	/// <summary>柔和扇形波束 + 三条描边弧 + 底部圆点。</summary>
	private static void DrawWifi(Graphics g, Color color, Color soft)
	{
		PointF origin = new PointF(10f, 15.5f);
		using (GraphicsPath fan = Pie(origin, 8.8f, 210f, 120f))
		{
			Sh.Fill(g, fan, soft);
		}
		using (GraphicsPath dot = Sh.Circle(origin, 1.4f))
		{
			Sh.Fill(g, dot, soft);
			Sh.Stroke(g, dot, color, StrokeWidth);
		}
		using (GraphicsPath arc1 = Sh.Arc(origin, 8.8f, 210f, 120f))
		{
			Sh.Stroke(g, arc1, color, StrokeWidth);
		}
		using (GraphicsPath arc2 = Sh.Arc(origin, 6.2f, 210f, 120f))
		{
			Sh.Stroke(g, arc2, color, StrokeWidth);
		}
		using (GraphicsPath arc3 = Sh.Arc(origin, 3.6f, 210f, 120f))
		{
			Sh.Stroke(g, arc3, color, StrokeWidth);
		}
	}

	/// <summary>柔和插头主体 + 两根插脚 + 下方线缆。</summary>
	private static void DrawPlug(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath body = Sh.Round(new RectangleF(6.4f, 7.6f, 7.2f, 6.9f), 2.2f))
		{
			Sh.Fill(g, body, soft);
			Sh.Stroke(g, body, color, StrokeWidth);
		}
		using (GraphicsPath prongA = Sh.Line(new PointF(8.3f, 3.1f), new PointF(8.3f, 7.9f)))
		{
			Sh.Stroke(g, prongA, color, StrokeWidth);
		}
		using (GraphicsPath prongB = Sh.Line(new PointF(11.7f, 3.1f), new PointF(11.7f, 7.9f)))
		{
			Sh.Stroke(g, prongB, color, StrokeWidth);
		}
		using (GraphicsPath cable = Sh.Line(new PointF(10f, 14.2f), new PointF(10f, 17.4f)))
		{
			Sh.Stroke(g, cable, color, StrokeWidth);
		}
	}

	/// <summary>柔和机身 + 描边轮廓 + 听筒线 + 主页点。</summary>
	private static void DrawPhone(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath body = Sh.Round(new RectangleF(5.6f, 2.6f, 8.8f, 14.8f), 2.4f))
		{
			Sh.Fill(g, body, soft);
			Sh.Stroke(g, body, color, StrokeWidth);
		}
		using (GraphicsPath speaker = Sh.Line(new PointF(8.6f, 5.2f), new PointF(11.4f, 5.2f)))
		{
			Sh.Stroke(g, speaker, color, StrokeWidth);
		}
		using (GraphicsPath home = Sh.Circle(new PointF(10f, 14.8f), 1f))
		{
			Sh.Fill(g, home, color);
		}
	}

	/// <summary>柔和屏幕 + 描边轮廓 + 支架与底座。</summary>
	private static void DrawMonitor(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath screen = Sh.Round(new RectangleF(2.6f, 3.6f, 14.8f, 10.6f), 1.8f))
		{
			Sh.Fill(g, screen, soft);
			Sh.Stroke(g, screen, color, StrokeWidth);
		}
		using (GraphicsPath stem = Sh.Line(new PointF(10f, 14f), new PointF(10f, 16.9f)))
		{
			Sh.Stroke(g, stem, color, StrokeWidth);
		}
		using (GraphicsPath baseLine = Sh.Line(new PointF(6.4f, 17.3f), new PointF(13.6f, 17.3f)))
		{
			Sh.Stroke(g, baseLine, color, StrokeWidth);
		}
	}

	/// <summary>柔和喇叭箱体（梯形）+ 描边 + 右侧两道声波弧。</summary>
	private static void DrawSpeaker(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath body = Sh.PolyClosed(
			new PointF(3.2f, 7.6f),
			new PointF(6.6f, 7.6f),
			new PointF(10.4f, 4.4f),
			new PointF(10.4f, 15.6f),
			new PointF(6.6f, 12.4f),
			new PointF(3.2f, 12.4f)))
		{
			Sh.Fill(g, body, soft);
			Sh.Stroke(g, body, color, StrokeWidth);
		}
		PointF center = new PointF(11.1f, 10f);
		using (GraphicsPath waveA = Sh.Arc(center, 2.7f, -55f, 110f))
		{
			Sh.Stroke(g, waveA, color, StrokeWidth);
		}
		using (GraphicsPath waveB = Sh.Arc(center, 5.5f, -55f, 110f))
		{
			Sh.Stroke(g, waveB, color, StrokeWidth);
		}
	}

	/// <summary>柔和宽体手柄 + 描边 + 左侧十字方向键 + 右侧两个按键点。</summary>
	private static void DrawGamepad(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath body = Sh.Round(new RectangleF(2.6f, 6.1f, 14.8f, 7.8f), 3.9f))
		{
			Sh.Fill(g, body, soft);
			Sh.Stroke(g, body, color, StrokeWidth);
		}
		using (GraphicsPath padV = Sh.Line(new PointF(6.7f, 8.3f), new PointF(6.7f, 11.7f)))
		{
			Sh.Stroke(g, padV, color, StrokeWidth);
		}
		using (GraphicsPath padH = Sh.Line(new PointF(5f, 10f), new PointF(8.4f, 10f)))
		{
			Sh.Stroke(g, padH, color, StrokeWidth);
		}
		using (GraphicsPath dotA = Sh.Circle(new PointF(13.2f, 8.9f), 1.05f))
		{
			Sh.Fill(g, dotA, color);
		}
		using (GraphicsPath dotB = Sh.Circle(new PointF(13.2f, 11.1f), 1.05f))
		{
			Sh.Fill(g, dotB, color);
		}
	}

	/// <summary>柔和头显面罩 + 描边 + 两枚镜片 + 头顶头带弧。</summary>
	private static void DrawHeadset(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath visor = Sh.Round(new RectangleF(2.9f, 7.1f, 14.2f, 7.4f), 3f))
		{
			Sh.Fill(g, visor, soft);
			Sh.Stroke(g, visor, color, StrokeWidth);
		}
		using (GraphicsPath lensA = Sh.Circle(new PointF(7.2f, 10.8f), 2.05f))
		{
			Sh.Stroke(g, lensA, color, StrokeWidth);
		}
		using (GraphicsPath lensB = Sh.Circle(new PointF(12.8f, 10.8f), 2.05f))
		{
			Sh.Stroke(g, lensB, color, StrokeWidth);
		}
		using (GraphicsPath strap = Sh.Arc(new PointF(10f, 12.5f), 7.6f, 225f, 90f))
		{
			Sh.Stroke(g, strap, color, StrokeWidth);
		}
	}

	/// <summary>柔和斜置手柄 + 描边 + 尖端三点星光。</summary>
	private static void DrawWand(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath handle = RotatedCapsule(new PointF(9.1f, 11.1f), 45f, 3.2f, 12f, 1.6f))
		{
			Sh.Fill(g, handle, soft);
			Sh.Stroke(g, handle, color, StrokeWidth);
		}
		using (GraphicsPath tip = Star(new PointF(15.1f, 5.1f), 2.5f, 0.95f))
		{
			Sh.Fill(g, tip, soft);
			Sh.Stroke(g, tip, color, StrokeWidth);
		}
	}

	/// <summary>约 270° 描边圆弧 + 左上方箭头（逆时针，撤销）。</summary>
	private static void DrawUndo(Graphics g, Color color)
	{
		PointF center = new PointF(10f, 10.6f);
		const float radius = 6f;
		using (GraphicsPath arc = Sh.Arc(center, radius, 200f, -270f))
		{
			Sh.Stroke(g, arc, color, StrokeWidth);
		}
		ArrowHead(g, Polar(center, radius, 200f), 110f, 2.9f, color, StrokeWidth);
	}

	/// <summary>单笔粗对勾（略粗于标准描边）。</summary>
	private static void DrawCheck(Graphics g, Color color)
	{
		using GraphicsPath check = Sh.Poly(
			new PointF(4.6f, 10.6f),
			new PointF(8.4f, 14.4f),
			new PointF(15.6f, 6f));
		Sh.Stroke(g, check, color, 1.8f);
	}

	/// <summary>减号。</summary>
	private static void DrawMinus(Graphics g, Color color)
	{
		using GraphicsPath bar = Sh.Line(new PointF(4.6f, 10f), new PointF(15.4f, 10f));
		Sh.Stroke(g, bar, color, 1.7f);
	}

	/// <summary>加号。</summary>
	private static void DrawPlus(Graphics g, Color color)
	{
		using (GraphicsPath barH = Sh.Line(new PointF(4.6f, 10f), new PointF(15.4f, 10f)))
		{
			Sh.Stroke(g, barH, color, 1.7f);
		}
		using (GraphicsPath barV = Sh.Line(new PointF(10f, 4.6f), new PointF(10f, 15.4f)))
		{
			Sh.Stroke(g, barV, color, 1.7f);
		}
	}

	/// <summary>叉号。</summary>
	private static void DrawClose(Graphics g, Color color)
	{
		using (GraphicsPath diagA = Sh.Line(new PointF(5.4f, 5.4f), new PointF(14.6f, 14.6f)))
		{
			Sh.Stroke(g, diagA, color, 1.6f);
		}
		using (GraphicsPath diagB = Sh.Line(new PointF(14.6f, 5.4f), new PointF(5.4f, 14.6f)))
		{
			Sh.Stroke(g, diagB, color, 1.6f);
		}
	}

	/// <summary>柔和三角形 + 描边。</summary>
	private static void DrawPlay(Graphics g, Color color, Color soft)
	{
		using GraphicsPath tri = Sh.PolyClosed(
			new PointF(7f, 4.6f),
			new PointF(16.2f, 10f),
			new PointF(7f, 15.4f));
		Sh.Fill(g, tri, soft);
		Sh.Stroke(g, tri, color, StrokeWidth);
	}

	/// <summary>两根柔和圆角竖条 + 描边。</summary>
	private static void DrawPause(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath left = Sh.Round(new RectangleF(5.5f, 4.6f, 3f, 10.8f), 1.2f))
		{
			Sh.Fill(g, left, soft);
			Sh.Stroke(g, left, color, StrokeWidth);
		}
		using (GraphicsPath right = Sh.Round(new RectangleF(11.5f, 4.6f, 3f, 10.8f), 1.2f))
		{
			Sh.Fill(g, right, soft);
			Sh.Stroke(g, right, color, StrokeWidth);
		}
	}

	/// <summary>三个实心圆点。</summary>
	private static void DrawDots(Graphics g, Color color)
	{
		using (GraphicsPath dotA = Sh.Circle(new PointF(4.5f, 10f), 1.5f))
		{
			Sh.Fill(g, dotA, color);
		}
		using (GraphicsPath dotB = Sh.Circle(new PointF(10f, 10f), 1.5f))
		{
			Sh.Fill(g, dotB, color);
		}
		using (GraphicsPath dotC = Sh.Circle(new PointF(15.5f, 10f), 1.5f))
		{
			Sh.Fill(g, dotC, color);
		}
	}

	/// <summary>柔和圆盘 + 描边圆环 + 两个小圆 + 连杆（Steam 风格）。</summary>
	private static void DrawSteam(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath disc = Sh.Circle(new PointF(10f, 10f), 6.9f))
		{
			Sh.Fill(g, disc, soft);
			Sh.Stroke(g, disc, color, StrokeWidth);
		}
		using (GraphicsPath rod = Sh.Line(new PointF(8.7f, 11.3f), new PointF(11.3f, 8.7f)))
		{
			Sh.Stroke(g, rod, color, StrokeWidth);
		}
		using (GraphicsPath small = Sh.Circle(new PointF(7.4f, 12.6f), 1.75f))
		{
			Sh.Fill(g, small, soft);
			Sh.Stroke(g, small, color, StrokeWidth);
		}
		using (GraphicsPath small2 = Sh.Circle(new PointF(12.6f, 7.4f), 1.75f))
		{
			Sh.Fill(g, small2, soft);
			Sh.Stroke(g, small2, color, StrokeWidth);
		}
	}

	/// <summary>柔和表盘 + 240° 描边弧 + 指针 + 轴心。</summary>
	private static void DrawGauge(Graphics g, Color color, Color soft)
	{
		PointF center = new PointF(10f, 10.6f);
		using (GraphicsPath disc = Sh.Circle(center, 6.6f))
		{
			Sh.Fill(g, disc, soft);
		}
		using (GraphicsPath arc = Sh.Arc(center, 6.6f, 160f, 220f))
		{
			Sh.Stroke(g, arc, color, StrokeWidth);
		}
		using (GraphicsPath needle = Sh.Line(center, new PointF(6.2f, 7.4f)))
		{
			Sh.Stroke(g, needle, color, StrokeWidth);
		}
		using (GraphicsPath hub = Sh.Circle(center, 1.15f))
		{
			Sh.Fill(g, hub, color);
		}
	}

	/// <summary>柔和表盘 + 描边圆环 + 两根指针。</summary>
	private static void DrawClock(Graphics g, Color color, Color soft)
	{
		PointF center = new PointF(10f, 10f);
		using (GraphicsPath disc = Sh.Circle(center, 6.9f))
		{
			Sh.Fill(g, disc, soft);
			Sh.Stroke(g, disc, color, StrokeWidth);
		}
		using (GraphicsPath handA = Sh.Line(center, new PointF(10f, 5.6f)))
		{
			Sh.Stroke(g, handA, color, StrokeWidth);
		}
		using (GraphicsPath handB = Sh.Line(center, new PointF(13.4f, 11.9f)))
		{
			Sh.Stroke(g, handB, color, StrokeWidth);
		}
		using (GraphicsPath hub = Sh.Circle(center, 0.95f))
		{
			Sh.Fill(g, hub, color);
		}
	}

	/// <summary>两枚斜置柔和链环胶囊 + 描边。</summary>
	private static void DrawLink(Graphics g, Color color, Color soft)
	{
		// 两枚链环沿对角线平行错开，中段交叠，读起来是相扣的两环而不是一根长条。
		using (GraphicsPath linkA = RotatedCapsule(new PointF(11.76f, 6.96f), 45f, 3.2f, 9.5f, 1.6f))
		{
			Sh.Fill(g, linkA, soft);
			Sh.Stroke(g, linkA, color, StrokeWidth);
		}
		using (GraphicsPath linkB = RotatedCapsule(new PointF(8.24f, 13.04f), 45f, 3.2f, 9.5f, 1.6f))
		{
			Sh.Fill(g, linkB, soft);
			Sh.Stroke(g, linkB, color, StrokeWidth);
		}
	}

	/// <summary>柔和盾牌剪影 + 描边 + 内部对勾。</summary>
	private static void DrawShield(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath shield = Sh.PolyClosed(
			new PointF(4.6f, 5.3f),
			new PointF(5.6f, 4.3f),
			new PointF(14.4f, 4.3f),
			new PointF(15.4f, 5.3f),
			new PointF(15.4f, 9.9f),
			new PointF(10f, 16.4f),
			new PointF(4.6f, 9.9f)))
		{
			Sh.Fill(g, shield, soft);
			Sh.Stroke(g, shield, color, StrokeWidth);
		}
		using (GraphicsPath check = Sh.Poly(
			new PointF(7.4f, 10f),
			new PointF(9.4f, 12f),
			new PointF(12.9f, 7.6f)))
		{
			Sh.Stroke(g, check, color, StrokeWidth);
		}
	}

	/// <summary>约 270° 描边圆弧 + 右上方箭头（顺时针，刷新），方向与撤销镜像。</summary>
	private static void DrawRefresh(Graphics g, Color color)
	{
		PointF center = new PointF(10f, 10.6f);
		const float radius = 6f;
		using (GraphicsPath arc = Sh.Arc(center, radius, 340f, 270f))
		{
			Sh.Stroke(g, arc, color, StrokeWidth);
		}
		ArrowHead(g, Polar(center, radius, 340f), 70f, 2.9f, color, StrokeWidth);
	}

	/// <summary>右向尖括号。</summary>
	private static void DrawChevronRight(Graphics g, Color color)
	{
		using (GraphicsPath upper = Sh.Line(new PointF(7.2f, 5.2f), new PointF(13.4f, 10f)))
		{
			Sh.Stroke(g, upper, color, 1.7f);
		}
		using (GraphicsPath lower = Sh.Line(new PointF(13.4f, 10f), new PointF(7.2f, 14.8f)))
		{
			Sh.Stroke(g, lower, color, 1.7f);
		}
	}

	/// <summary>柔和四角星 + 描边 + 右上角小星。</summary>
	private static void DrawSparkle(Graphics g, Color color, Color soft)
	{
		using (GraphicsPath star = Star(new PointF(9f, 10.6f), 6f, 1.9f))
		{
			Sh.Fill(g, star, soft);
			Sh.Stroke(g, star, color, StrokeWidth);
		}
		using (GraphicsPath tiny = Star(new PointF(15f, 5.1f), 2.4f, 0.85f))
		{
			Sh.Fill(g, tiny, soft);
			Sh.Stroke(g, tiny, color, StrokeWidth);
		}
	}

	// ——— 私有几何辅助（全部基于 Sh，不复制其实现）———

	/// <summary>以 center 为中心的旋转圆角矩形。</summary>
	private static GraphicsPath RotatedCapsule(PointF center, float angleDegrees, float width, float height, float radius)
	{
		GraphicsPath path = Sh.Round(
			new RectangleF(center.X - width / 2f, center.Y - height / 2f, width, height),
			radius);
		using Matrix matrix = new Matrix();
		matrix.RotateAt(angleDegrees, center);
		path.Transform(matrix);
		return path;
	}

	/// <summary>由圆弧 + 两条半径围成的扇形（用于柔和波束），复用 Sh.Arc。</summary>
	private static GraphicsPath Pie(PointF center, float radius, float startDegrees, float sweepDegrees)
	{
		GraphicsPath path = Sh.Arc(center, radius, startDegrees, sweepDegrees);
		path.AddLine(Polar(center, radius, startDegrees + sweepDegrees), center);
		path.CloseFigure();
		return path;
	}

	/// <summary>箭头头部：由两条短线组成，tip 为尖端，directionDegrees 为指向。</summary>
	private static void ArrowHead(Graphics g, PointF tip, float directionDegrees, float size, Color color, float width)
	{
		using (GraphicsPath barbA = Sh.Line(tip, Polar(tip, size, directionDegrees + 150f)))
		{
			Sh.Stroke(g, barbA, color, width);
		}
		using (GraphicsPath barbB = Sh.Line(tip, Polar(tip, size, directionDegrees - 150f)))
		{
			Sh.Stroke(g, barbB, color, width);
		}
	}

	/// <summary>四角星（凹多边形）。</summary>
	private static GraphicsPath Star(PointF center, float outer, float inner)
	{
		PointF[] points = new PointF[8];
		for (int i = 0; i < points.Length; i++)
		{
			points[i] = Polar(center, ((i % 2) == 0) ? outer : inner, -90f + (i * 45f));
		}
		return Sh.PolyClosed(points);
	}

	/// <summary>极坐标取点（0° 指向右，角度顺时针，与 GDI+ 一致）。</summary>
	private static PointF Polar(PointF origin, float radius, float degrees)
	{
		float radians = degrees * MathF.PI / 180f;
		return new PointF(
			origin.X + (radius * MathF.Cos(radians)),
			origin.Y + (radius * MathF.Sin(radians)));
	}
}
