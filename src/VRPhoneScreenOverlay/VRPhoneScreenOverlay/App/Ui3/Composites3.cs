using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>布局叶节点：一块由页面自己绘制的区域（测量尺寸由调用方给定，0 表示拉伸）。</summary>
internal sealed class Slot3 : Node3
{
	private readonly Layout3 _ctx;

	private readonly float _width;

	private readonly float _height;

	public Slot3(Layout3 ctx, string id, float widthLogical = 0f, float heightLogical = 0f)
	{
		_ctx = ctx;
		Id = id;
		_width = widthLogical;
		_height = heightLogical;
	}

	public string Id { get; }

	public override SizeF Measure(Graphics g, SizeF available)
	{
		float w = _width > 0f ? _ctx.U(_width) : available.Width;
		float h = _height > 0f ? _ctx.U(_height) : available.Height;
		return new SizeF(w, h);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
	}
}

/// <summary>
/// 页面基类：构建布局树 → 布局 → 绘制自己负责的区域。
/// 交互控件（按钮/开关/步进器/指标）是真实子控件，静态内容由页面在 OnPaint 里画到各自的 Slot 上。
/// </summary>
internal abstract class Page3 : Base3
{
	private readonly Dictionary<string, Slot3> _slots = new Dictionary<string, Slot3>(StringComparer.Ordinal);

	protected Layout3 Ctx = null!;

	protected Node3? Root { get; private set; }

	public float ContentHeight { get; private set; }

	public abstract void Build(Layout3 ctx);

	protected void SetRoot(Node3 root)
	{
		Root = root;
	}

	/// <summary>登记一个绘制区域。</summary>
	protected Slot3 Slot(string id, float widthLogical = 0f, float heightLogical = 0f)
	{
		Slot3 slot = new Slot3(Ctx, id, widthLogical, heightLogical);
		_slots[id] = slot;
		return slot;
	}

	protected Slot3? S(string id)
	{
		return _slots.TryGetValue(id, out Slot3 slot) ? slot : null;
	}

	protected RectangleF R(string id)
	{
		return S(id)?.Bounds ?? RectangleF.Empty;
	}

	/// <summary>诊断：导出各槽位矩形。</summary>
	public string DumpSlots()
	{
		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		builder.Append("page=").Append(Width).Append('x').Append(Height).Append(" content=").Append(ContentHeight).AppendLine();
		foreach (KeyValuePair<string, Slot3> pair in _slots)
		{
			RectangleF b = pair.Value.Bounds;
			builder.Append("  ").Append(pair.Key.PadRight(10)).Append(" x=").Append((int)b.X).Append(" y=").Append((int)b.Y).Append(" w=").Append((int)b.Width).Append(" h=").Append((int)b.Height).AppendLine();
		}
		return builder.ToString();
	}

	public void RunLayout(Graphics g, float width, float height)
	{
		if (Root == null)
		{
			return;
		}
		SizeF measured = Root.Measure(g, new SizeF(width, height));
		ContentHeight = measured.Height;
		Root.Arrange(g, new RectangleF(0f, 0f, width, Math.Max(measured.Height, height)));
		Root.ApplyTree();
		PlaceControls();
	}

	/// <summary>把真实子控件摆到已算好的区域里。</summary>
	protected virtual void PlaceControls()
	{
	}
}

/// <summary>v3 绘制库：卡片、标题、行、管线、步骤、信息块。</summary>
internal static class Draw3
{
	public static void Card(Graphics g, RectangleF rect, float scale, Color? fill = null, bool clickable = false, float radiusLogical = Tok.RPanel)
	{
		if (rect.Width <= 1f || rect.Height <= 1f)
		{
			return;
		}
		RectangleF b = new RectangleF(rect.X, rect.Y, rect.Width - 1f, rect.Height - 1f);
		float radius = radiusLogical * scale;
		using (GraphicsPath path = Sh.Round(b, radius))
		{
			Sh.Fill(g, path, fill ?? Tok.Surface);
		}
		float inset = radius * 0.72f;
		using (GraphicsPath top = Sh.Round(new RectangleF(b.X + inset, b.Y, Math.Max(1f, b.Width - inset * 2f), 1.6f * scale), 1f * scale))
		{
			Sh.Fill(g, top, Tok.TopLight);
		}
		using GraphicsPath edge = Sh.Round(b, radius);
		Sh.Stroke(g, edge, Tok.LineSoft, MathF.Max(1f, scale));
	}

	/// <summary>卡片标题行 + 分隔线，返回内容区起点 Y。</summary>
	public static float CardTitle(Graphics g, RectangleF rect, float scale, string title, string? rightText = null, Tone rightTone = Tone.Muted, string? badge = null, Tone badgeTone = Tone.Accent)
	{
		float pad = 18f * scale;
		float headerH = 46f * scale;
		Sh.Text(g, title, Tok.Title(), Tok.Text1, new RectangleF(rect.X + pad, rect.Y + 6f * scale, rect.Width - pad * 2f - (rightText != null ? 150f * scale : 0f), headerH - 10f * scale), ContentAlignment.MiddleLeft);
		float rightEdge = rect.Right - pad;
		if (!string.IsNullOrEmpty(badge))
		{
			SizeF size = MeasureChip(g, badge, scale);
			RectangleF chip = new RectangleF(rightEdge - size.Width, rect.Y + (headerH - size.Height) / 2f, size.Width, size.Height);
			Chip(g, chip, scale, badge, badgeTone, dot: false);
			rightEdge = chip.X - 8f * scale;
		}
		if (!string.IsNullOrEmpty(rightText))
		{
			Sh.Text(g, rightText, Tok.Caption(), Tok.Tone(rightTone), new RectangleF(rect.X + rect.Width * 0.45f, rect.Y + 6f * scale, Math.Max(10f, rightEdge - rect.X - rect.Width * 0.45f), headerH - 10f * scale), ContentAlignment.MiddleRight);
		}
		Sh.Hair(g, rect.X + pad, rect.Y + headerH - 1f * scale, Math.Max(1f, rect.Width - pad * 2f), Tok.LineSoft, scale);
		return rect.Y + headerH;
	}

	public static SizeF MeasureChip(Graphics g, string text, float scale, bool dot = true)
	{
		Size size = TextRenderer.MeasureText(g, text, Tok.Caption(true), Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
		float extra = dot ? 3.1f * 2f + 6f : 0f;
		return new SizeF(size.Width + 22f * scale + extra * scale, 22f * scale);
	}

	public static void Chip(Graphics g, RectangleF rect, float scale, string text, Tone tone, bool dot = true, bool live = false, float phase = 0f)
	{
		Color color = Tok.Tone(tone);
		using (GraphicsPath path = Sh.Capsule(rect))
		{
			Sh.Fill(g, path, Tok.Tint(color, Tok.Surface, 0.16f));
			Sh.Stroke(g, path, Tok.Tint(color, Tok.Surface, 0.32f), MathF.Max(1f, scale));
		}
		float left = rect.X + 11f * scale;
		if (dot)
		{
			float r = 3.1f * scale;
			float alpha = live ? (0.45f + 0.55f * Ease.Pulse(phase)) : 1f;
			using (GraphicsPath circle = Sh.Circle(new PointF(left + r, rect.Y + rect.Height / 2f), r))
			{
				Sh.Fill(g, circle, WithAlpha(color, alpha));
			}
			if (live)
			{
				using GraphicsPath ring = Sh.Circle(new PointF(left + r, rect.Y + rect.Height / 2f), r + 2.6f * scale * Ease.Pulse(phase));
				Sh.Stroke(g, ring, WithAlpha(color, 0.3f * (1f - Ease.Pulse(phase))), MathF.Max(1f, scale));
			}
			left += r * 2f + 6f * scale;
		}
		Sh.Text(g, text, Tok.Caption(true), color, new RectangleF(left, rect.Y, Math.Max(4f, rect.Right - left - 9f * scale), rect.Height), ContentAlignment.MiddleLeft, ellipsis: false);
	}

	public static Color WithAlpha(Color c, float alpha)
	{
		return Color.FromArgb((int)Math.Clamp(alpha * 255f, 0f, 255f), c);
	}

	/// <summary>图标方块（行的左侧标识）。</summary>
	public static void IconTile(Graphics g, RectangleF rect, float scale, Icon3 icon, Tone tone, bool filled = true)
	{
		Color color = Tok.Tone(tone);
		float radius = 9f * scale;
		if (filled)
		{
			using GraphicsPath path = Sh.Round(rect, radius);
			Sh.Fill(g, path, Tok.Tint(color, Tok.Surface, 0.14f));
			Sh.Stroke(g, path, Tok.Tint(color, Tok.Surface, 0.26f), MathF.Max(1f, scale));
		}
		float size = Math.Min(rect.Width, rect.Height) * 0.58f;
		DuoIcons.Draw(g, icon, new RectangleF(rect.X + (rect.Width - size) / 2f, rect.Y + (rect.Height - size) / 2f, size, size), color, WithAlpha(color, 0.18f));
	}

	/// <summary>一行链路/状态：图标 + 标题 +（详情，支持 \n 两行）+（右侧状态文本）。</summary>
	public static void Row(Graphics g, RectangleF rect, float scale, Icon3 icon, Tone iconTone, string title, string? detail, string? status = null, Tone statusTone = Tone.Muted, bool statusLive = false, float phase = 0f)
	{
		float tile = 30f * scale;
		float pad = 18f * scale;
		float top = rect.Y;
		IconTile(g, new RectangleF(rect.X + pad, top + 8f * scale, tile, tile), scale, icon, iconTone);
		float textLeft = rect.X + pad + tile + 12f * scale;
		string[] lines = string.IsNullOrEmpty(detail) ? Array.Empty<string>() : detail.Split('\n');
		bool hasDetail = lines.Length > 0;
		float titleTop = hasDetail ? (top + 8f * scale) : (top + (rect.Height - 20f * scale) / 2f);
		Sh.Text(g, title, Tok.Body(true), Tok.Text1, new RectangleF(textLeft, titleTop, Math.Max(20f, rect.Width - (textLeft - rect.X) - pad - 96f * scale), 20f * scale), ContentAlignment.MiddleLeft);
		float detailWidth = Math.Max(20f, rect.Width - (textLeft - rect.X) - pad - 8f * scale);
		for (int i = 0; i < lines.Length && i < 2; i++)
		{
			Sh.Text(g, lines[i], Tok.Caption(), Tok.Text3, new RectangleF(textLeft, titleTop + (19f + i * 15f) * scale, detailWidth, 16f * scale), ContentAlignment.MiddleLeft);
		}
		if (!string.IsNullOrEmpty(status))
		{
			Color color = Tok.Tone(statusTone);
			float dotR = 3.2f * scale;
			Size size = TextRenderer.MeasureText(g, status, Tok.Body(true), Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
			float block = size.Width + dotR * 2f + 7f * scale;
			float x = rect.Right - pad - block;
			float cy = titleTop + 10f * scale;
			float alpha = statusLive ? (0.45f + 0.55f * Ease.Pulse(phase)) : 1f;
			using (GraphicsPath circle = Sh.Circle(new PointF(x + dotR, cy), dotR))
			{
				Sh.Fill(g, circle, WithAlpha(color, alpha));
			}
			if (statusLive)
			{
				using GraphicsPath ring = Sh.Circle(new PointF(x + dotR, cy), dotR + 2.4f * scale * Ease.Pulse(phase));
				Sh.Stroke(g, ring, WithAlpha(color, 0.3f * (1f - Ease.Pulse(phase))), MathF.Max(1f, scale));
			}
			Sh.Text(g, status, Tok.Body(true), color, new RectangleF(x + dotR * 2f + 7f * scale, titleTop, size.Width + 2f, 20f * scale), ContentAlignment.MiddleLeft, ellipsis: false);
		}
	}

	/// <summary>英雄区：眉标 + 大标题 + 副标题。</summary>
	public static void Hero(Graphics g, RectangleF rect, float scale, string eyebrow, Tone eyebrowTone, string title, string subtitle)
	{
		float pad = 22f * scale;
		float y = rect.Y + 20f * scale;
		if (!string.IsNullOrEmpty(eyebrow))
		{
			SizeF chip = MeasureChip(g, eyebrow, scale);
			Chip(g, new RectangleF(rect.X + pad, y, chip.Width, chip.Height), scale, eyebrow, eyebrowTone);
			y += chip.Height + 12f * scale;
		}
		Sh.Text(g, title, Tok.Hero(), Tok.Text1, new RectangleF(rect.X + pad, y, rect.Width - pad * 2f - 240f * scale, 30f * scale), ContentAlignment.MiddleLeft);
		y += 30f * scale;
		Sh.Text(g, subtitle, Tok.Body(), Tok.Text3, new RectangleF(rect.X + pad, y, rect.Width - pad * 2f - 240f * scale, 20f * scale), ContentAlignment.MiddleLeft);
	}

	/// <summary>管线节点：图标 + 名称 + 状态。</summary>
	public static void PipeNode(Graphics g, RectangleF rect, float scale, Icon3 icon, string name, string state, Tone tone, bool live, float phase)
	{
		Color color = Tok.Tone(tone);
		float radius = 12f * scale;
		using (GraphicsPath path = Sh.Round(new RectangleF(rect.X, rect.Y, rect.Width - 1f, rect.Height - 1f), radius))
		{
			Sh.Fill(g, path, Tok.Mix(Tok.Surface, Tok.Tint(color, Tok.Surface, 0.10f), live ? (0.7f + 0.3f * Ease.Pulse(phase)) : 0.9f));
			Sh.Stroke(g, path, Tok.Tint(color, Tok.Surface, live ? 0.42f : 0.22f), MathF.Max(1f, scale));
		}
		float iconSize = 20f * scale;
		DuoIcons.Draw(g, icon, new RectangleF(rect.X + 14f * scale, rect.Y + (rect.Height - iconSize) / 2f, iconSize, iconSize), color, WithAlpha(color, 0.18f));
		float textLeft = rect.X + 14f * scale + iconSize + 12f * scale;
		Sh.Text(g, name, Tok.Body(true), Tok.Text1, new RectangleF(textLeft, rect.Y + 12f * scale, Math.Max(20f, rect.Width - (textLeft - rect.X) - 14f * scale), 18f * scale), ContentAlignment.MiddleLeft);
		Sh.Text(g, state, Tok.Caption(true), color, new RectangleF(textLeft, rect.Y + 30f * scale, Math.Max(20f, rect.Width - (textLeft - rect.X) - 14f * scale), 16f * scale), ContentAlignment.MiddleLeft);
	}

	/// <summary>管线连接线（带方向的小箭头）。</summary>
	public static void PipeLink(Graphics g, RectangleF rect, float scale, bool active)
	{
		float cy = rect.Y + rect.Height / 2f;
		Color color = active ? Tok.Accent : Tok.Line;
		Sh.Hair(g, rect.X, cy, Math.Max(1f, rect.Width), color, MathF.Max(1f, scale));
		float size = 4.5f * scale;
		using GraphicsPath arrow = Sh.PolyClosed(
			new PointF(rect.Right - size, cy - size),
			new PointF(rect.Right, cy),
			new PointF(rect.Right - size, cy + size));
		Sh.Fill(g, arrow, color);
	}

	/// <summary>编号步骤。</summary>
	public static void Step(Graphics g, RectangleF rect, float scale, int index, string title, string text)
	{
		float badge = 26f * scale;
		using (GraphicsPath path = Sh.Circle(new PointF(rect.X + badge / 2f, rect.Y + badge / 2f + 2f * scale), badge / 2f))
		{
			Sh.Fill(g, path, Tok.Tint(Tok.Accent, Tok.Surface, 0.16f));
			Sh.Stroke(g, path, Tok.Tint(Tok.Accent, Tok.Surface, 0.36f), MathF.Max(1f, scale));
		}
		Sh.Text(g, index.ToString(), Tok.Caption(true), Tok.Accent, new RectangleF(rect.X, rect.Y + 2f * scale, badge, badge), ContentAlignment.MiddleCenter, ellipsis: false);
		float left = rect.X + badge + 12f * scale;
		Sh.Text(g, title, Tok.Body(true), Tok.Text1, new RectangleF(left, rect.Y, Math.Max(20f, rect.Width - (left - rect.X)), 20f * scale), ContentAlignment.MiddleLeft);
		Sh.Text(g, text, Tok.Caption(), Tok.Text3, new RectangleF(left, rect.Y + 19f * scale, Math.Max(20f, rect.Width - (left - rect.X)), 34f * scale), ContentAlignment.TopLeft);
	}

	/// <summary>信息块：小标题 + 值（关于页三连块）。</summary>
	public static void InfoTile(Graphics g, RectangleF rect, float scale, Icon3 icon, string caption, string value)
	{
		Card(g, rect, scale, Tok.SurfaceAlt, radiusLogical: Tok.RTile);
		float pad = 14f * scale;
		IconTile(g, new RectangleF(rect.X + pad, rect.Y + pad, 26f * scale, 26f * scale), scale, icon, Tone.Accent, filled: false);
		Sh.Text(g, caption, Tok.Micro(), Tok.Text3, new RectangleF(rect.X + pad + 32f * scale, rect.Y + pad + 2f * scale, Math.Max(20f, rect.Width - pad * 2f - 32f * scale), 22f * scale), ContentAlignment.MiddleLeft);
		Sh.Text(g, value, Tok.Body(true), Tok.Text1, new RectangleF(rect.X + pad, rect.Y + pad + 34f * scale, Math.Max(20f, rect.Width - pad * 2f), 22f * scale), ContentAlignment.MiddleLeft);
	}

	/// <summary>读数块（等宽，用于坐标轴）。</summary>
	public static void Readout(Graphics g, RectangleF rect, float scale, string label, string value, Tone tone = Tone.Neutral)
	{
		using (GraphicsPath path = Sh.Round(new RectangleF(rect.X, rect.Y, rect.Width - 1f, rect.Height - 1f), 10f * scale))
		{
			Sh.Fill(g, path, Tok.Well);
			Sh.Stroke(g, path, Tok.LineSoft, MathF.Max(1f, scale));
		}
		float pad = 12f * scale;
		Sh.Text(g, label, Tok.Micro(), Tok.Text3, new RectangleF(rect.X + pad, rect.Y, rect.Width - pad * 2f, rect.Height * 0.44f), ContentAlignment.BottomLeft);
		Sh.Text(g, value, Tok.Readout(true), tone == Tone.Neutral ? Tok.Text1 : Tok.Tone(tone), new RectangleF(rect.X + pad, rect.Y + rect.Height * 0.42f, Math.Max(20f, rect.Width - pad * 2f), rect.Height * 0.5f), ContentAlignment.TopLeft);
	}

	/// <summary>设置行：标题 + 说明（右侧放真实控件）。</summary>
	public static void SettingText(Graphics g, RectangleF rect, float scale, string title, string? description)
	{
		Sh.Text(g, title, Tok.Body(true), Tok.Text1, new RectangleF(rect.X, rect.Y + 4f * scale, rect.Width, 20f * scale), ContentAlignment.MiddleLeft);
		if (!string.IsNullOrEmpty(description))
		{
			Sh.Text(g, description, Tok.Caption(), Tok.Text3, new RectangleF(rect.X, rect.Y + 23f * scale, rect.Width, 18f * scale), ContentAlignment.MiddleLeft);
		}
	}
}
