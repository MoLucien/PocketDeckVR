using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>v3 控件基类：双缓冲、动画时钟接入、DPI 比例、悬停/按下/焦点波。</summary>
internal abstract class Base3 : Control, Anim.IAnimated
{
	/// <summary>当前界面比例（设备像素 / 逻辑像素），由外壳统一设置。</summary>
	public static float Scale { get; set; } = 1f;

	/// <summary>清屏底色：控件所在容器的颜色（画布或卡片面）。</summary>
	public Color ClearColor { get; set; } = Tok.Canvas;

	protected Wave Hover = Wave.At(0f, 16f);

	protected Wave Press = Wave.At(0f, 26f);

	protected Wave Glow = Wave.At(0f, 8f);

	protected bool Tracking;

	protected bool Holding;

	protected Base3()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, value: true);
		BackColor = Color.Transparent;
		DoubleBuffered = true;
	}

	/// <summary>逻辑单位 → 设备像素。</summary>
	protected float U(float logical)
	{
		return logical * Scale;
	}

	private ToolTip? _tip;

	/// <summary>悬停提示（交互辅助）。</summary>
	public void Tooltip(string text)
	{
		_tip ??= new ToolTip { InitialDelay = 350, ReshowDelay = 120, AutoPopDelay = 6000 };
		_tip.SetToolTip(this, text ?? string.Empty);
	}

	protected static float Clamp01(float v)
	{
		return Math.Clamp(v, 0f, 1f);
	}

	public bool Advance(float dt)
	{
		bool busy = Hover.Advance(dt) | Press.Advance(dt) | Glow.Advance(dt);
		busy |= AdvanceExtra(dt);
		if (busy)
		{
			Invalidate();
		}
		return busy;
	}

	protected virtual bool AdvanceExtra(float dt)
	{
		return false;
	}

	protected void Animate()
	{
		Anim.Request(this);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		Tracking = true;
		Hover.Set(1f);
		Animate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		Tracking = false;
		Holding = false;
		Hover.Set(0f);
		Press.Set(0f);
		Animate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && Enabled)
		{
			Holding = true;
			Press.Set(1f);
			Animate();
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		Holding = false;
		Press.Set(0f);
		Animate();
		base.OnMouseUp(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		if (!Enabled)
		{
			Tracking = false;
			Holding = false;
			Hover.Set(0f);
			Press.Set(0f);
		}
		Invalidate();
		base.OnEnabledChanged(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Glow.Set(1f);
		Animate();
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Glow.Set(0f);
		Animate();
		Invalidate();
		base.OnLostFocus(e);
	}

	/// <summary>焦点环（仅键盘导航时显示）。</summary>
	protected void DrawFocus(Graphics g, RectangleF bounds, float radius)
	{
		if (!Focused || !ShowFocusCues)
		{
			return;
		}
		float a = 0.35f + 0.65f * Clamp01(Glow.Value);
		using GraphicsPath path = Sh.Round(RectangleF.Inflate(bounds, -U(2f), -U(2f)), Math.Max(1f, radius - U(2f)));
		Sh.Stroke(g, path, Color.FromArgb((int)(200f * a), Tok.Accent), U(2f));
	}

	protected static Color WithAlpha(Color c, float alpha)
	{
		return Color.FromArgb((int)Math.Clamp(alpha * 255f, 0f, 255f), c);
	}
}

/// <summary>面板：抬升底色 + 发丝描边 + 顶部高光，可选可点击。</summary>
internal class Panel3 : Base3
{
	private bool _clickable;

	public Panel3()
	{
		SetStyle(ControlStyles.Selectable, false);
	}

	public bool Clickable
	{
		get => _clickable;
		set
		{
			_clickable = value;
			Cursor = value ? Cursors.Hand : Cursors.Default;
		}
	}

	/// <summary>面板底色（默认 Surface）。</summary>
	public Color Fill { get; set; } = Tok.Surface;

	/// <summary>描边色（默认 LineSoft）。</summary>
	public Color Edge { get; set; } = Tok.LineSoft;

	/// <summary>顶部高光（抬升感），默认开启。</summary>
	public bool TopHighlight { get; set; } = true;

	/// <summary>圆角（逻辑单位）。</summary>
	public float Radius { get; set; } = Tok.RPanel;

	protected virtual Color ResolveFill()
	{
		if (!Enabled)
		{
			return Tok.Surface;
		}
		float hover = Clamp01(Hover.Value);
		float press = Clamp01(Press.Value);
		Color fill = Fill;
		if (hover > 0.01f)
		{
			fill = Tok.Mix(fill, Tok.SurfaceHover, hover * (_clickable ? 0.9f : 0.35f));
		}
		if (press > 0.01f)
		{
			fill = Tok.Mix(fill, Tok.SurfacePressed, press * 0.9f);
		}
		return fill;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		RectangleF bounds = new RectangleF(0f, 0f, ClientSize.Width - 1f, ClientSize.Height - 1f);
		PaintSelf(g, bounds);
	}

	protected virtual void PaintSelf(Graphics g, RectangleF bounds)
	{
		PaintPanel(g, bounds, ResolveFill(), Edge, Radius, TopHighlight);
	}

	protected void PaintPanel(Graphics g, RectangleF bounds, Color fill, Color edge, float radiusLogical, bool topLight)
	{
		float radius = U(radiusLogical);
		using (GraphicsPath path = Sh.Round(bounds, radius))
		{
			Sh.Fill(g, path, fill);
		}
		if (topLight)
		{
			float inset = radius * 0.7f;
			using GraphicsPath top = Sh.Round(new RectangleF(bounds.X + inset, bounds.Y, Math.Max(1f, bounds.Width - inset * 2f), U(2f)), U(1f));
			Sh.Fill(g, top, Tok.TopLight);
		}
		if (edge.A > 0)
		{
			using GraphicsPath path2 = Sh.Round(bounds, radius);
			Sh.Stroke(g, path2, edge, MathF.Max(1f, U(1f)));
		}
	}
}

/// <summary>按钮：Primary / Soft / Ghost / Danger / Ok，三档尺寸，动效悬停与按下。</summary>
internal sealed class Button3 : Base3
{
	public enum Look
	{
		Primary,
		Soft,
		Ghost,
		Danger,
		Ok,
	}

	private Look _look = Look.Soft;

	public Button3()
	{
		SetStyle(ControlStyles.Selectable, true);
		TabStop = true;
		Font = Tok.Body(true);
		Cursor = Cursors.Hand;
		Size = new Size(120, 38);
	}

	public Look Variant
	{
		get => _look;
		set
		{
			_look = value;
			Invalidate();
		}
	}

	public Icon3 Icon { get; set; } = Icon3.None;

	/// <summary>紧凑尺寸（逻辑高度）：38 = 常规，32 = 小。</summary>
	public float LogicalHeight { get; set; } = 38f;

	/// <summary>内容是否左对齐（用于「带图标 + 文案」的宽按钮）。</summary>
	public bool AlignLeft { get; set; }

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		RectangleF bounds = new RectangleF(0f, 0f, ClientSize.Width - 1f, ClientSize.Height - 1f);
		bool on = Enabled;
		float hover = Clamp01(Hover.Value);
		float press = Clamp01(Press.Value);
		Color fill;
		Color edge;
		Color ink;
		switch (_look)
		{
			case Look.Primary:
				fill = Tok.Mix(Tok.Mix(Tok.Accent, Tok.AccentHi, hover), Tok.AccentLo, press);
				edge = Color.Transparent;
				ink = Tok.AccentInk;
				break;
			case Look.Ok:
				fill = Tok.Mix(Tok.Tint(Tok.Ok, Tok.Surface, 0.22f + 0.12f * hover), Tok.Tint(Tok.Ok, Tok.Surface, 0.34f), press);
				edge = Tok.Tint(Tok.Ok, Tok.Surface, 0.45f);
				ink = Tok.Ok;
				break;
			case Look.Danger:
				fill = Tok.Mix(Tok.Tint(Tok.Bad, Tok.Surface, 0.2f + 0.12f * hover), Tok.Tint(Tok.Bad, Tok.Surface, 0.32f), press);
				edge = Tok.Tint(Tok.Bad, Tok.Surface, 0.42f);
				ink = Tok.Bad;
				break;
			case Look.Ghost:
				fill = Tok.Tint(Tok.SurfaceHover, Tok.Surface, hover * 0.85f + press * 0.15f);
				edge = Tok.Mix(Tok.LineSoft, Tok.Line, hover);
				ink = Tok.Mix(Tok.Text2, Tok.Text1, hover);
				break;
			default:
				fill = Tok.Mix(Tok.Mix(Tok.SurfaceAlt, Tok.SurfaceHover, hover), Tok.SurfacePressed, press);
				edge = Tok.Mix(Tok.LineSoft, Tok.Line, hover);
				ink = Tok.Text1;
				break;
		}
		if (!on)
		{
			fill = Tok.SurfaceAlt;
			edge = Tok.LineSoft;
			ink = Tok.TextOff;
		}
		float radius = U(_look == Look.Primary ? Tok.RCtl : Tok.RCtl);
		using (GraphicsPath path = Sh.Round(bounds, radius))
		{
			Sh.Fill(g, path, fill);
		}
		using (GraphicsPath path = Sh.Round(bounds, radius))
		{
			if (edge.A > 0)
			{
				Sh.Stroke(g, path, edge, MathF.Max(1f, U(1f)));
			}
		}
		if (_look == Look.Primary && on)
		{
			using GraphicsPath path = Sh.Round(new RectangleF(bounds.X + U(8f), bounds.Y, Math.Max(1f, bounds.Width - U(16f)), U(2f)), U(1f));
			Sh.Fill(g, path, WithAlpha(Color.White, 0.12f));
		}
		float iconSize = U(16f);
		Size textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
		float gap = Icon == Icon3.None ? 0f : U(8f);
		float contentWidth = (Icon == Icon3.None ? 0f : iconSize) + gap + textSize.Width;
		float left = AlignLeft ? U(14f) : Math.Max(U(10f), (bounds.Width - contentWidth) / 2f);
		float offsetY = press * U(1f);
		if (Icon != Icon3.None)
		{
			DuoIcons.Draw(g, Icon, new RectangleF(left, (bounds.Height - iconSize) / 2f + offsetY, iconSize, iconSize), ink, WithAlpha(ink, 0.18f));
			left += iconSize + gap;
		}
		Sh.Text(g, Text, Font, ink, new RectangleF(left, offsetY, Math.Max(U(4f), bounds.Right - left - U(6f)), bounds.Height), AlignLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleLeft);
		DrawFocus(g, bounds, radius);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		return keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
		{
			Press.Set(1f);
			Animate();
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
		{
			Press.Set(0f);
			Animate();
			PerformClick();
			e.Handled = true;
		}
		base.OnKeyUp(e);
	}

	public void PerformClick()
	{
		OnClick(EventArgs.Empty);
	}
}

/// <summary>方形图标按钮（窗口控制、行内小动作）。</summary>
internal sealed class IconButton3 : Base3
{
	public IconButton3()
	{
		Size = new Size(34, 34);
		Cursor = Cursors.Hand;
		TabStop = false;
	}

	public Icon3 Icon { get; set; } = Icon3.None;

	public Tone Tone { get; set; } = Tone.Neutral;

	/// <summary>悬停时的底色（默认淡面板）。</summary>
	public Color? HoverFill { get; set; }

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		RectangleF bounds = new RectangleF(U(2f), U(2f), ClientSize.Width - U(4f) - 1f, ClientSize.Height - U(4f) - 1f);
		float hover = Clamp01(Hover.Value);
		float press = Clamp01(Press.Value);
		if (hover > 0.01f || press > 0.01f)
		{
			Color fill = HoverFill ?? Tok.SurfaceHover;
			using GraphicsPath path = Sh.Round(bounds, U(Tok.RCtl));
			Sh.Fill(g, path, Tok.Mix(Tok.Surface, Tok.Mix(fill, Tok.SurfacePressed, press), Math.Max(hover, press)));
		}
		Color ink = Tone == Tone.Neutral ? Tok.Mix(Tok.Text2, Tok.Text1, hover) : Tok.Tone(Tone);
		if (!Enabled)
		{
			ink = Tok.TextOff;
		}
		float size = U(16f);
		DuoIcons.Draw(g, Icon, new RectangleF((ClientSize.Width - size) / 2f, (ClientSize.Height - size) / 2f, size, size), ink, WithAlpha(ink, 0.16f));
	}
}

/// <summary>胶囊标签：淡底 + 语义色文本 +（可选）呼吸点。</summary>
internal sealed class Chip3 : Base3
{
	private float _phase;

	public Chip3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Font = Tok.Caption(true);
		Size = new Size(80, 24);
	}

	public Tone Tone { get; set; } = Tone.Neutral;

	public bool Dot { get; set; } = true;

	/// <summary>呼吸动画（表示「正在进行 / 在线」）。</summary>
	public bool Live
	{
		get => _live;
		set
		{
			if (_live == value)
			{
				return;
			}
			_live = value;
			if (value)
			{
				Animate();
			}
			Invalidate();
		}
	}

	private bool _live;

	protected override bool AdvanceExtra(float dt)
	{
		if (!_live)
		{
			return false;
		}
		_phase += dt * 0.9f;
		if (_phase > 1f)
		{
			_phase -= 1f;
		}
		return true;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		RectangleF bounds = new RectangleF(0f, 0f, ClientSize.Width - 1f, ClientSize.Height - 1f);
		Color tone = Enabled ? Tok.Tone(Tone) : Tok.TextOff;
		using (GraphicsPath path = Sh.Capsule(bounds))
		{
			Sh.Fill(g, path, Tok.Tint(tone, Tok.Surface, 0.16f));
			Sh.Stroke(g, path, Tok.Tint(tone, Tok.Surface, 0.34f), MathF.Max(1f, U(1f)));
		}
		float left = U(10f);
		if (Dot)
		{
			float r = U(3.2f);
			float cy = bounds.Height / 2f;
			float alpha = _live ? (0.45f + 0.55f * Ease.Pulse(_phase)) : 1f;
			using GraphicsPath dot = Sh.Circle(new PointF(left + r, cy), r);
			Sh.Fill(g, dot, WithAlpha(tone, alpha));
			if (_live)
			{
				using GraphicsPath ring = Sh.Circle(new PointF(left + r, cy), r + U(2.4f) * (0.4f + 0.6f * Ease.Pulse(_phase)));
				Sh.Stroke(g, ring, WithAlpha(tone, 0.35f * (1f - Ease.Pulse(_phase))), MathF.Max(1f, U(1f)));
			}
			left += r * 2f + U(6f);
		}
		Sh.Text(g, Text, Font, tone, new RectangleF(left, 0f, Math.Max(U(4f), bounds.Right - left - U(8f)), bounds.Height), ContentAlignment.MiddleLeft);
	}
}

/// <summary>开关：轨道 + 动效圆钮。</summary>
internal sealed class Switch3 : CheckBox, Anim.IAnimated
{
	private Wave _knob = Wave.At(0f, 18f);

	private Wave _hover = Wave.At(0f, 16f);

	public Switch3()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, value: true);
		Appearance = Appearance.Button;
		AutoSize = false;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		Size = new Size(42, 24);
		TabStop = true;
		_knob.Value = 0f;
		_knob.Target = 0f;
	}

	public Color SurfaceColor { get; set; } = Tok.Surface;

	bool Anim.IAnimated.Advance(float dt)
	{
		bool busy = _knob.Advance(dt) | _hover.Advance(dt);
		if (busy)
		{
			Invalidate();
		}
		return busy;
	}

	private void Kick()
	{
		Anim.Request(this);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hover.Set(1f);
		Kick();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover.Set(0f);
		Kick();
		base.OnMouseLeave(e);
	}

	protected override void OnCheckedChanged(EventArgs e)
	{
		_knob.Set(Checked ? 1f : 0f);
		Kick();
		base.OnCheckedChanged(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		Invalidate();
		base.OnEnabledChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (_knob.Target != (Checked ? 1f : 0f))
		{
			_knob.Set(Checked ? 1f : 0f);
			Kick();
		}
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(SurfaceColor);
		float scale = Base3.Scale;
		RectangleF track = new RectangleF(0f, 0f, ClientSize.Width - 1f, ClientSize.Height - 1f);
		float t = Math.Clamp(_knob.Value, 0f, 1f);
		float hover = Math.Clamp(_hover.Value, 0f, 1f);
		Color off = Tok.Mix(Tok.SurfaceAlt, Tok.SurfaceHover, hover);
		Color on = Tok.Mix(Tok.Accent, Tok.AccentHi, hover);
		Color fill = Enabled ? Tok.Mix(off, on, t) : Tok.SurfaceAlt;
		using (GraphicsPath path = Sh.Capsule(track))
		{
			Sh.Fill(g, path, fill);
			Sh.Stroke(g, path, Enabled ? Tok.Mix(Tok.Line, Tok.Tint(Tok.Accent, Tok.Surface, 0.6f), t) : Tok.LineSoft, MathF.Max(1f, scale));
		}
		float pad = 3f * scale;
		float knob = track.Height - pad * 2f;
		float x = pad + t * (track.Width - knob - pad * 2f);
		using GraphicsPath circle = Sh.Circle(new PointF(x + knob / 2f, track.Height / 2f), knob / 2f);
		Sh.Fill(g, circle, Enabled ? Color.White : Tok.TextOff);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		return keyData is Keys.Space || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Space)
		{
			Checked = !Checked;
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}
}

/// <summary>分段选择器：轨道 + 会滑动的选中块。</summary>
internal sealed class Segments3<T> : Base3 where T : notnull
{
	private readonly List<FixedChoiceNode3<T>> _nodes = new List<FixedChoiceNode3<T>>();

	private int _index;

	private Wave _slide = Wave.At(0f, 16f);

	public event EventHandler? SelectedValueChanged;

	public Segments3()
	{
		SetStyle(ControlStyles.Selectable, true);
		TabStop = true;
		Font = Tok.Body(true);
		Height = 38;
	}

	public void SetNodes(IEnumerable<FixedChoiceNode3<T>> nodes, T selected)
	{
		_nodes.Clear();
		_nodes.AddRange(nodes);
		_index = Math.Max(0, _nodes.FindIndex(n => EqualityComparer<T>.Default.Equals(n.Value, selected)));
		if (_nodes.Count > 0 && _index < 0)
		{
			_index = 0;
		}
		_slide.Value = _index;
		_slide.Target = _index;
		Invalidate();
	}

	public bool TryGetSelectedValue(out T value)
	{
		if (_nodes.Count == 0 || _index < 0 || _index >= _nodes.Count)
		{
			value = default;
			return false;
		}
		value = _nodes[_index].Value;
		return true;
	}

	public bool SelectValue(T value)
	{
		int i = _nodes.FindIndex(n => EqualityComparer<T>.Default.Equals(n.Value, value));
		if (i < 0)
		{
			return false;
		}
		if (i != _index)
		{
			_index = i;
			_slide.Set(i);
			Animate();
			Invalidate();
		}
		return true;
	}

	protected override bool AdvanceExtra(float dt)
	{
		return _slide.Advance(dt);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (_nodes.Count > 0)
		{
			int i = IndexAt(e.X);
			if (i >= 0 && i != _index)
			{
				_index = i;
				_slide.Set(i);
				Animate();
				Invalidate();
				SelectedValueChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	protected override bool IsInputKey(Keys keyData)
	{
		return keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		int next = _index;
		if (e.KeyCode == Keys.Left)
		{
			next--;
		}
		else if (e.KeyCode == Keys.Right)
		{
			next++;
		}
		if (next != _index && next >= 0 && next < _nodes.Count)
		{
			_index = next;
			_slide.Set(next);
			Animate();
			Invalidate();
			SelectedValueChanged?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	private int IndexAt(int x)
	{
		if (_nodes.Count == 0)
		{
			return -1;
		}
		float inner = ClientSize.Width - U(6f);
		float seg = inner / _nodes.Count;
		int i = (int)((x - U(3f)) / Math.Max(1f, seg));
		return (i < 0 || i >= _nodes.Count) ? -1 : i;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		RectangleF bounds = new RectangleF(0f, 0f, ClientSize.Width - 1f, ClientSize.Height - 1f);
		float radius = U(Tok.RCtl);
		using (GraphicsPath track = Sh.Round(bounds, radius))
		{
			Sh.Fill(g, track, Tok.Well);
			Sh.Stroke(g, track, Tok.LineSoft, MathF.Max(1f, U(1f)));
		}
		if (_nodes.Count == 0)
		{
			return;
		}
		float pad = U(3f);
		float inner = bounds.Width - pad * 2f;
		float seg = inner / _nodes.Count;
		float slide = _slide.Value;
		RectangleF pill = new RectangleF(pad + slide * seg, pad, seg, bounds.Height - pad * 2f);
		using (GraphicsPath pillPath = Sh.Round(pill, Math.Max(1f, radius - U(2f))))
		{
			Sh.Fill(g, pillPath, Enabled ? Tok.SurfaceHover : Tok.SurfaceAlt);
			Sh.Stroke(g, pillPath, Tok.Line, MathF.Max(1f, U(1f)));
		}
		for (int i = 0; i < _nodes.Count; i++)
		{
			RectangleF cell = new RectangleF(pad + i * seg, pad, seg, bounds.Height - pad * 2f);
			bool selected = i == _index;
			float near = 1f - Math.Min(1f, MathF.Abs(slide - i));
			Color ink = !Enabled ? Tok.TextOff : (selected ? Tok.Text1 : Tok.Mix(Tok.Text3, Tok.Text2, near * 0.6f));
			Sh.Text(g, _nodes[i].Text, Font, ink, cell, ContentAlignment.MiddleCenter);
		}
		DrawFocus(g, bounds, radius);
	}
}

internal sealed record FixedChoiceNode3<T>(T Value, string Text) where T : notnull;
