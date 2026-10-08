using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PocketDeck.App.Ui3;

/// <summary>v3 几何与绘制原语：圆角、胶囊、圆、折线、圆弧、贝塞尔、发丝线。</summary>
internal static class Sh
{
	public static GraphicsPath Round(RectangleF r, float radius)
	{
		GraphicsPath path = new GraphicsPath();
		float d = Math.Max(0f, Math.Min(radius * 2f, Math.Min(r.Width, r.Height)));
		if (d <= 0.6f)
		{
			path.AddRectangle(r);
			return path;
		}
		path.AddArc(r.X, r.Y, d, d, 180f, 90f);
		path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}

	public static GraphicsPath Capsule(RectangleF r)
	{
		return Round(r, Math.Min(r.Width, r.Height) / 2f);
	}

	public static GraphicsPath Circle(PointF center, float radius)
	{
		GraphicsPath path = new GraphicsPath();
		path.AddEllipse(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
		return path;
	}

	public static GraphicsPath Line(PointF a, PointF b)
	{
		GraphicsPath path = new GraphicsPath();
		path.AddLine(a, b);
		return path;
	}

	public static GraphicsPath Poly(params PointF[] points)
	{
		GraphicsPath path = new GraphicsPath();
		if (points.Length >= 2)
		{
			path.AddLines(points);
		}
		return path;
	}

	public static GraphicsPath PolyClosed(params PointF[] points)
	{
		GraphicsPath path = new GraphicsPath();
		if (points.Length >= 2)
		{
			path.AddLines(points);
			path.CloseFigure();
		}
		return path;
	}

	public static GraphicsPath Arc(PointF center, float radius, float startDegrees, float sweepDegrees)
	{
		GraphicsPath path = new GraphicsPath();
		path.AddArc(center.X - radius, center.Y - radius, radius * 2f, radius * 2f, startDegrees, sweepDegrees);
		return path;
	}

	public static GraphicsPath Bezier(PointF p0, PointF c1, PointF c2, PointF p1)
	{
		GraphicsPath path = new GraphicsPath();
		path.AddBezier(p0, c1, c2, p1);
		return path;
	}

	public static void Fill(Graphics g, GraphicsPath path, Color color)
	{
		using SolidBrush brush = new SolidBrush(color);
		g.FillPath(brush, path);
	}

	public static void Stroke(Graphics g, GraphicsPath path, Color color, float width)
	{
		using Pen pen = new Pen(color, width)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		g.DrawPath(pen, path);
	}

	/// <summary>1 逻辑像素发丝线（水平）。</summary>
	public static void Hair(Graphics g, float x, float y, float width, Color color, float scale)
	{
		using SolidBrush brush = new SolidBrush(color);
		g.FillRectangle(brush, x, y, width, Math.Max(1f, scale));
	}

	/// <summary>1 逻辑像素发丝线（垂直）。</summary>
	public static void HairV(Graphics g, float x, float y, float height, Color color, float scale)
	{
		using SolidBrush brush = new SolidBrush(color);
		g.FillRectangle(brush, x, y, Math.Max(1f, scale), height);
	}

	/// <summary>文本（单行，垂直居中，可选省略号）。</summary>
	public static void Text(Graphics g, string text, Font font, Color color, RectangleF bounds, ContentAlignment align = ContentAlignment.MiddleLeft, bool ellipsis = true)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		System.Windows.Forms.TextFormatFlags flags = System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.SingleLine | System.Windows.Forms.TextFormatFlags.VerticalCenter;
		flags |= align switch
		{
			ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter => System.Windows.Forms.TextFormatFlags.HorizontalCenter,
			ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight => System.Windows.Forms.TextFormatFlags.Right,
			_ => System.Windows.Forms.TextFormatFlags.Left,
		};
		if (ellipsis)
		{
			flags |= System.Windows.Forms.TextFormatFlags.EndEllipsis;
		}
		System.Windows.Forms.TextRenderer.DrawText(g, text, font, Rectangle.Round(bounds), color, flags);
	}

	/// <summary>多行文本（左上对齐，自动换行）。</summary>
	public static void TextWrap(Graphics g, string text, Font font, Color color, RectangleF bounds)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		System.Windows.Forms.TextRenderer.DrawText(g, text, font, Rectangle.Round(bounds), color, System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.Top);
	}
}
