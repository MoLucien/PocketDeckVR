using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>步进器：[−] 数值 [+] + 位置刻度点。</summary>
internal sealed class Stepper3<T> : Base3 where T : notnull
{
	private readonly List<FixedChoiceNode3<T>> _nodes = new List<FixedChoiceNode3<T>>();

	private int _index;

	private Wave _minus = Wave.At(0f, 18f);

	private Wave _plus = Wave.At(0f, 18f);

	private Wave _glide = Wave.At(0f, 14f);

	private int _hot;

	public event EventHandler? SelectedValueChanged;

	public Stepper3()
	{
		SetStyle(ControlStyles.Selectable, true);
		TabStop = true;
		Font = Tok.Body(true);
		Height = 54;
	}

	public void SetNodes(IEnumerable<FixedChoiceNode3<T>> nodes, T selected)
	{
		_nodes.Clear();
		_nodes.AddRange(nodes);
		int i = _nodes.FindIndex(n => EqualityComparer<T>.Default.Equals(n.Value, selected));
		_index = i < 0 ? 0 : i;
		_glide.Value = _index;
		_glide.Target = _index;
		Invalidate();
	}

	public bool TryGetSelectedValue(out T value)
	{
		if (_nodes.Count == 0)
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
			_glide.Set(i);
			Animate();
			Invalidate();
		}
		return true;
	}

	protected override bool AdvanceExtra(float dt)
	{
		return _minus.Advance(dt) | _plus.Advance(dt) | _glide.Advance(dt);
	}

	private RectangleF MinusRect()
	{
		float h = ClientSize.Height - U(12f);
		return new RectangleF(0f, 0f, U(38f), h);
	}

	private RectangleF PlusRect()
	{
		float h = ClientSize.Height - U(12f);
		return new RectangleF(ClientSize.Width - U(38f), 0f, U(38f), h);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int hot = 0;
		if (MinusRect().Contains(e.Location) && CanMinus)
		{
			hot = -1;
		}
		else if (PlusRect().Contains(e.Location) && CanPlus)
		{
			hot = 1;
		}
		if (hot != _hot)
		{
			_hot = hot;
			_minus.Set(hot == -1 ? 1f : 0f);
			_plus.Set(hot == 1 ? 1f : 0f);
			Animate();
		}
		Cursor = hot == 0 ? Cursors.Default : Cursors.Hand;
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = 0;
		_minus.Set(0f);
		_plus.Set(0f);
		Animate();
		Cursor = Cursors.Default;
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (MinusRect().Contains(e.Location) && CanMinus)
		{
			Move(-1);
		}
		else if (PlusRect().Contains(e.Location) && CanPlus)
		{
			Move(1);
		}
	}

	protected override bool IsInputKey(Keys keyData)
	{
		return keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Left && CanMinus)
		{
			Move(-1);
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Right && CanPlus)
		{
			Move(1);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	private bool CanMinus => Enabled && _index > 0;

	private bool CanPlus => Enabled && _index >= 0 && _index < _nodes.Count - 1;

	private void Move(int delta)
	{
		int i = _index + delta;
		if (i < 0 || i >= _nodes.Count)
		{
			return;
		}
		_index = i;
		_glide.Set(i);
		Animate();
		Invalidate();
		SelectedValueChanged?.Invoke(this, EventArgs.Empty);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		float w = ClientSize.Width - 1f;
		float h = ClientSize.Height - 1f;
		float buttonH = h - U(12f);
		PaintStep(g, MinusRect(), Icon3.Minus, Clamp01(_minus.Value), CanMinus);
		PaintStep(g, PlusRect(), Icon3.Plus, Clamp01(_plus.Value), CanPlus);
		RectangleF well = new RectangleF(U(38f) + U(8f), 0f, Math.Max(U(40f), w - U(38f) * 2f - U(16f)), buttonH);
		using (GraphicsPath path = Sh.Round(well, U(Tok.RCtl)))
		{
			Sh.Fill(g, path, Tok.Well);
			Sh.Stroke(g, path, Tok.LineSoft, MathF.Max(1f, U(1f)));
		}
		if (_nodes.Count > 0)
		{
			string text = _nodes[_index].Text;
			Sh.Text(g, text, Font, Enabled ? Tok.Text1 : Tok.TextOff, well, ContentAlignment.MiddleCenter);
		}
		// 位置刻度点
		if (_nodes.Count > 1 && _nodes.Count <= 12)
		{
			float cy = buttonH + U(7f);
			float trackW = Math.Min(w, U(120f));
			float x0 = (w - trackW) / 2f;
			float step = _nodes.Count > 1 ? trackW / (_nodes.Count - 1) : trackW;
			for (int i = 0; i < _nodes.Count; i++)
			{
				float cx = x0 + step * i;
				bool on = i <= _index;
				float r = (i == _index) ? U(2.6f) : U(1.8f);
				using GraphicsPath dot = Sh.Circle(new PointF(cx, cy), r);
				Sh.Fill(g, dot, !Enabled ? Tok.TextOff : (on ? Tok.Accent : Tok.Line));
			}
		}
		DrawFocus(g, new RectangleF(0f, 0f, w, buttonH), U(Tok.RCtl));
	}

	private void PaintStep(Graphics g, RectangleF rect, Icon3 icon, float hover, bool on)
	{
		Color fill = on ? Tok.Mix(Tok.SurfaceAlt, Tok.SurfaceHover, hover) : Tok.Surface;
		using (GraphicsPath path = Sh.Round(rect, U(Tok.RCtl)))
		{
			Sh.Fill(g, path, fill);
			Sh.Stroke(g, path, on ? Tok.Mix(Tok.LineSoft, Tok.Line, hover) : Tok.LineSoft, MathF.Max(1f, U(1f)));
		}
		Color ink = !on ? Tok.TextOff : Tok.Mix(Tok.Text2, Tok.Text1, hover);
		float size = U(16f);
		DuoIcons.Draw(g, icon, new RectangleF(rect.X + (rect.Width - size) / 2f, rect.Y + (rect.Height - size) / 2f, size, size), ink, WithAlpha(ink, 0.14f));
	}
}

/// <summary>指标行：名称 + 数值 + 动效进度条。</summary>
internal sealed class Meter3 : Base3
{
	private Wave _fill = Wave.At(0f, 9f);

	private string _value = "--";

	private float _ratio;

	public Meter3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Height = 46;
	}

	public string Caption { get; set; } = string.Empty;

	public Icon3 Icon { get; set; } = Icon3.None;

	public Tone Tone { get; set; } = Tone.Neutral;

	/// <summary>更新数值文本与进度（0..1）。</summary>
	public void SetValue(string value, float ratio)
	{
		_value = value ?? "--";
		_ratio = Math.Clamp(ratio, 0f, 1f);
		_fill.Set(_ratio);
		Animate();
		Invalidate();
	}

	protected override bool AdvanceExtra(float dt)
	{
		return _fill.Advance(dt);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		float w = ClientSize.Width - 1f;
		float h = ClientSize.Height - 1f;
		float iconSize = U(15f);
		float left = 0f;
		if (Icon != Icon3.None)
		{
			Color ink = Tone == Tone.Neutral ? Tok.Text3 : Tok.Tone(Tone);
			DuoIcons.Draw(g, Icon, new RectangleF(0f, U(1f), iconSize, iconSize), ink, WithAlpha(ink, 0.16f));
			left = iconSize + U(8f);
		}
		Sh.Text(g, Caption, Tok.Caption(), Tok.Text3, new RectangleF(left, 0f, Math.Max(U(10f), w - left - U(90f)), U(20f)), ContentAlignment.MiddleLeft);
		Color valueInk = Tone == Tone.Neutral ? Tok.Text1 : Tok.Tone(Tone);
		Sh.Text(g, _value, Tok.Body(true), valueInk, new RectangleF(w - U(96f), 0f, U(96f), U(20f)), ContentAlignment.MiddleRight);
		RectangleF track = new RectangleF(0f, h - U(6f), w, U(4f));
		using (GraphicsPath path = Sh.Capsule(track))
		{
			Sh.Fill(g, path, Tok.Well);
		}
		float fillW = Math.Max(0f, (track.Width) * Math.Clamp(_fill.Value, 0f, 1f));
		if (fillW > U(2f))
		{
			RectangleF bar = new RectangleF(track.X, track.Y, fillW, track.Height);
			using GraphicsPath path = Sh.Capsule(bar);
			Color from = Tone == Tone.Neutral ? Tok.Accent : Tok.Tone(Tone);
			using LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(track.X, track.Y, Math.Max(U(4f), fillW), track.Height), Tok.Mix(from, Tok.Text1, 0.15f), from, LinearGradientMode.Horizontal);
			g.FillPath(brush, path);
		}
	}
}

/// <summary>状态文本：呼吸点 + 文本（右对齐或左对齐）。</summary>
internal sealed class Status3 : Base3
{
	private float _phase;

	private bool _live;

	public Status3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Font = Tok.Body(true);
		ForeColor = Tok.Text2;
		Height = 20;
	}

	public Tone Tone { get; set; } = Tone.Muted;

	public bool Dot { get; set; } = true;

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

	public ContentAlignment Align { get; set; } = ContentAlignment.MiddleRight;

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
		string text = Text ?? string.Empty;
		if (text.Length == 0)
		{
			return;
		}
		Color tone = Enabled ? Tok.Tone(Tone) : Tok.TextOff;
		Size size = TextRenderer.MeasureText(g, text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
		float dot = U(6.5f);
		float gap = Dot ? (dot + U(7f)) : 0f;
		float block = size.Width + gap + U(2f);
		bool right = Align is ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight;
		bool center = Align is ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter;
		float x = right ? Math.Max(0f, ClientSize.Width - block) : (center ? Math.Max(0f, (ClientSize.Width - block) / 2f) : 0f);
		if (Dot)
		{
			float cy = ClientSize.Height / 2f;
			float alpha = _live ? (0.4f + 0.6f * Ease.Pulse(_phase)) : 1f;
			using GraphicsPath circle = Sh.Circle(new PointF(x + dot / 2f, cy), dot / 2f);
			Sh.Fill(g, circle, WithAlpha(tone, alpha));
			if (_live)
			{
				using GraphicsPath ring = Sh.Circle(new PointF(x + dot / 2f, cy), dot / 2f + U(2.6f) * Ease.Pulse(_phase));
				Sh.Stroke(g, ring, WithAlpha(tone, 0.3f * (1f - Ease.Pulse(_phase))), MathF.Max(1f, U(1f)));
			}
			x += dot + U(7f);
		}
		Sh.Text(g, text, Font, tone, new RectangleF(x, 0f, Math.Max(U(4f), ClientSize.Width - x), ClientSize.Height), ContentAlignment.MiddleLeft);
	}
}

/// <summary>顶部标签栏：文字标签 + 滑动下划线。</summary>
internal sealed class TabBar3 : Base3
{
	private readonly List<string> _tabs = new List<string>();

	private int _index;

	private Wave _slide = Wave.At(0f, 16f);

	private Wave _width = Wave.At(0f, 16f);

	private int _hot = -1;

	public event EventHandler? SelectedIndexChanged;

	public TabBar3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Font = Tok.Nav();
		Height = Tok.TabBarHeight;
	}

	public int SelectedIndex => _index;

	public void SetTabs(params string[] tabs)
	{
		_tabs.Clear();
		_tabs.AddRange(tabs);
		_index = 0;
		_slide.Value = 0f;
		_slide.Target = 0f;
		Invalidate();
	}

	public void Select(int index, bool silent = false)
	{
		if (index < 0 || index >= _tabs.Count || index == _index)
		{
			return;
		}
		_index = index;
		_slide.Set(index);
		Animate();
		Invalidate();
		if (!silent)
		{
			SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override bool AdvanceExtra(float dt)
	{
		return _slide.Advance(dt) | _width.Advance(dt);
	}

	private float TabWidth()
	{
		return U(96f);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int hot = (e.X >= 0 && e.X < _tabs.Count * TabWidth()) ? (int)(e.X / TabWidth()) : -1;
		if (hot != _hot)
		{
			_hot = hot;
			Invalidate();
		}
		Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		int i = (e.X >= 0 && e.X < _tabs.Count * TabWidth()) ? (int)(e.X / TabWidth()) : -1;
		if (i >= 0)
		{
			Select(i);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(ClearColor);
		float w = ClientSize.Width;
		float h = ClientSize.Height;
		float tabW = TabWidth();
		for (int i = 0; i < _tabs.Count; i++)
		{
			bool selected = i == _index;
			float near = 1f - Math.Min(1f, MathF.Abs(_slide.Value - i));
			Color ink = selected ? Tok.Text1 : (_hot == i ? Tok.Text2 : Tok.Mix(Tok.Text3, Tok.Text2, near * 0.8f));
			RectangleF cell = new RectangleF(i * tabW, 0f, tabW, h - U(3f));
			Sh.Text(g, _tabs[i], Font, ink, cell, ContentAlignment.MiddleCenter, ellipsis: false);
		}
		float cx = _slide.Value * tabW;
		float barW = Math.Max(U(18f), tabW * 0.38f);
		RectangleF bar = new RectangleF(cx + (tabW - barW) / 2f, h - U(3f), barW, U(3f));
		using GraphicsPath path = Sh.Capsule(bar);
		Sh.Fill(g, path, Tok.Accent);
		Sh.Hair(g, 0f, h - MathF.Max(1f, U(1f)), w, Tok.LineSoft, 1f);
	}
}

/// <summary>底部状态栏：最新操作消息 + 进度 + 版本。</summary>
internal sealed class StatusBar3 : Base3
{
	private Wave _progress = Wave.At(0f, 6f);

	private float _phase;

	private string _message = "就绪";

	private bool _busy;

	private string _right = string.Empty;

	public StatusBar3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Font = Tok.Caption();
		Height = Tok.StatusBarHeight;
	}

	public Tone Tone { get; set; } = Tone.Muted;

	public string Right
	{
		get => _right;
		set
		{
			_right = value ?? string.Empty;
			Invalidate();
		}
	}

	public void SetMessage(string message, Tone tone, float progress = -1f)
	{
		_message = string.IsNullOrWhiteSpace(message) ? "就绪" : message;
		Tone = tone;
		_busy = progress >= 0f;
		_progress.Set(_busy ? Math.Clamp(progress, 0f, 1f) : 0f);
		Animate();
		Invalidate();
	}

	protected override bool AdvanceExtra(float dt)
	{
		if (_busy)
		{
			_phase += dt * 0.6f;
			if (_phase > 1f)
			{
				_phase -= 1f;
			}
			Invalidate();
			return true;
		}
		return _progress.Advance(dt);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Tok.Canvas);
		float w = ClientSize.Width;
		float h = ClientSize.Height;
		Sh.Hair(g, 0f, 0f, w, Tok.LineSoft, Base3.Scale);
		Color tone = Tok.Tone(Tone);
		float left = U(4f);
		using (GraphicsPath dot = Sh.Circle(new PointF(left + U(3f), h / 2f), U(3f)))
		{
			Sh.Fill(g, dot, _busy ? WithAlpha(tone, 0.5f + 0.5f * Ease.Pulse(_phase)) : tone);
		}
		left += U(12f);
		float rightWidth = U(120f);
		Sh.Text(g, _message, Font, Tone == Tone.Muted ? Tok.Text3 : Tok.Mix(tone, Tok.Text2, 0.45f), new RectangleF(left, 0f, Math.Max(U(20f), w - left - rightWidth), h), ContentAlignment.MiddleLeft);
		if (_busy)
		{
			RectangleF track = new RectangleF(w - rightWidth - U(10f), h / 2f - U(2f), rightWidth, U(4f));
			using (GraphicsPath trackPath = Sh.Capsule(track))
			{
				Sh.Fill(g, trackPath, Tok.Mix(Tok.Line, Tok.Well, 0.35f));
			}
			float value = Clamp01(_progress.Value);
			float width = Math.Max(U(6f), track.Width * (value <= 0.001f ? 0.18f : value));
			float x = track.X + (value <= 0.001f ? track.Width * Ease.Pulse(_phase) * 0.6f : 0f);
			using GraphicsPath barPath = Sh.Capsule(new RectangleF(x, track.Y, width, track.Height));
			Sh.Fill(g, barPath, tone);
		}
		else if (_right.Length > 0)
		{
			Sh.Text(g, _right, Font, Tok.Text3, new RectangleF(w - rightWidth - U(6f), 0f, rightWidth, h), ContentAlignment.MiddleRight);
		}
	}
}

/// <summary>轻量 Toast 栈（右下角弹出，自动消失）。</summary>
internal sealed class ToastHost3 : Base3
{
	private sealed class Toast
	{
		public string Title = string.Empty;

		public string Message = string.Empty;

		public Tone Tone = Tone.Info;

		public Icon3 Icon = Icon3.Info;

		public float Life;

		public Wave Slide = Wave.At(0f, 10f);
	}

	private const float Lifetime = 4f;

	private readonly List<Toast> _items = new List<Toast>();

	public ToastHost3()
	{
		SetStyle(ControlStyles.Selectable, false);
	}

	/// <summary>没有 Toast 时调用方应把宿主收成 0 尺寸，避免它用不透明底色盖住下层内容。</summary>
	public bool IsEmpty => _items.Count == 0;

	/// <summary>宿主需要的逻辑高度（按当前 Toast 数）。</summary>
	public float NeededHeight
	{
		get
		{
			int count = Math.Min(_items.Count, 2);
			return count <= 0 ? 0f : count * 72f;
		}
	}

	public event EventHandler? ContentChanged;

	public void Push(string title, string message, Tone tone, Icon3 icon)
	{
		Toast toast = new Toast { Title = title ?? string.Empty, Message = message ?? string.Empty, Tone = tone, Icon = icon };
		toast.Slide.Value = 0f;
		toast.Slide.Target = 1f;
		_items.Add(toast);
		while (_items.Count > 2)
		{
			_items.RemoveAt(0);
		}
		ContentChanged?.Invoke(this, EventArgs.Empty);
		Animate();
		Invalidate();
	}

	protected override bool AdvanceExtra(float dt)
	{
		bool busy = false;
		for (int i = _items.Count - 1; i >= 0; i--)
		{
			Toast toast = _items[i];
			toast.Life += dt;
			if (toast.Life > Lifetime)
			{
				toast.Slide.Set(0f);
			}
			busy |= toast.Slide.Advance(dt);
			if (toast.Life > Lifetime + 0.4f)
			{
				_items.RemoveAt(i);
				ContentChanged?.Invoke(this, EventArgs.Empty);
			}
		}
		return busy || _items.Count > 0;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		int index = IndexAt(e.Y);
		if (index >= 0)
		{
			_items.RemoveAt(index);
			Invalidate();
		}
	}

	private int IndexAt(int y)
	{
		float top = 0f;
		float cardH = U(62f);
		for (int i = _items.Count - 1; i >= 0; i--)
		{
			if (y >= top && y <= top + cardH)
			{
				return i;
			}
			top += cardH + U(10f);
		}
		return -1;
	}

	/// <summary>没有 Toast 的区域不接收鼠标：让点击穿透到下层内容，绝不挡住用户操作。</summary>
	protected override void WndProc(ref Message m)
	{
		const int WM_NCHITTEST = 132;
		if (m.Msg == WM_NCHITTEST && IsEmpty)
		{
			m.Result = (IntPtr)(-1); // HTTRANSPARENT
			return;
		}
		if (m.Msg == WM_NCHITTEST)
		{
			int lp = m.LParam.ToInt32();
			Point point = PointToClient(new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF))));
			if (IndexAt(point.Y) < 0)
			{
				m.Result = (IntPtr)(-1); // HTTRANSPARENT
				return;
			}
		}
		base.WndProc(ref m);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Tok.Canvas);
		float cardW = U(320f);
		float cardH = U(62f);
		float top = 0f;
		for (int i = _items.Count - 1; i >= 0; i--)
		{
			Toast toast = _items[i];
			float t = Math.Clamp(toast.Slide.Value, 0f, 1f);
			float eased = Ease.Out(t);
			if (eased <= 0.001f)
			{
				continue;
			}
			float x = ClientSize.Width - cardW - U(4f) + (1f - eased) * U(28f);
			RectangleF card = new RectangleF(x, top, cardW, cardH);
			Color tone = Tok.Tone(toast.Tone);
			using (GraphicsPath path = Sh.Round(card, U(Tok.RPanel)))
			{
				Sh.Fill(g, path, Tok.Mix(Tok.SurfaceAlt, Tok.Tint(tone, Tok.Surface, 0.12f), 1f));
				Sh.Stroke(g, path, Tok.Tint(tone, Tok.Surface, 0.35f), MathF.Max(1f, U(1f)));
			}
			float iconSize = U(18f);
			DuoIcons.Draw(g, toast.Icon, new RectangleF(card.X + U(14f), card.Y + U(14f), iconSize, iconSize), tone, WithAlpha(tone, 0.18f));
			Sh.Text(g, toast.Title, Tok.Body(true), Tok.Text1, new RectangleF(card.X + U(42f), card.Y + U(12f), card.Width - U(56f), U(18f)), ContentAlignment.MiddleLeft);
			Sh.Text(g, toast.Message, Tok.Caption(), Tok.Text3, new RectangleF(card.X + U(42f), card.Y + U(30f), card.Width - U(56f), U(20f)), ContentAlignment.MiddleLeft);
			if (toast.Life > Lifetime)
			{
				float fade = Math.Clamp(1f - (toast.Life - Lifetime) / 0.4f, 0f, 1f);
				using GraphicsPath fadePath = Sh.Round(card, U(Tok.RPanel));
				Sh.Fill(g, fadePath, WithAlpha(Tok.Canvas, 1f - fade));
			}
			top += cardH + U(10f);
		}
	}
}

/// <summary>滚动宿主：滚轮 + 细滚动条 + 平滑位移。</summary>
internal sealed class ScrollHost3 : Base3
{
	private Control? _content;

	private float _offset;

	private Wave _smooth = Wave.At(0f, 16f);

	private float _max;

	public ScrollHost3()
	{
		SetStyle(ControlStyles.Selectable, false);
		MouseWheel += (_, e) => ScrollBy(-e.Delta / 120f * U(64f));
	}

	public void SetContent(Control content)
	{
		_content = content;
		Controls.Clear();
		Controls.Add(content);
		content.Location = new Point(0, 0);
		UpdateBounds();
	}

	public void UpdateBounds()
	{
		if (_content == null)
		{
			return;
		}
		_content.Width = ClientSize.Width;
		_max = Math.Max(0f, _content.Height - ClientSize.Height);
		_offset = Math.Clamp(_offset, 0f, _max);
		_smooth.Value = _offset;
		_smooth.Target = _offset;
		_content.Top = -(int)MathF.Round(_offset);
		Invalidate();
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		Focus();
		base.OnMouseEnter(e);
	}

	protected void ScrollBy(float delta)
	{
		if (_max <= 0f)
		{
			return;
		}
		_offset = Math.Clamp(_offset + delta, 0f, _max);
		_smooth.Set(_offset);
		Animate();
	}

	protected override bool AdvanceExtra(float dt)
	{
		bool busy = _smooth.Advance(dt);
		if (_content != null)
		{
			int top = -(int)MathF.Round(_smooth.Value);
			if (_content.Top != top)
			{
				_content.Top = top;
			}
		}
		return busy;
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		ScrollBy(-e.Delta / 120f * U(64f));
		base.OnMouseWheel(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Tok.Canvas);
		if (_max <= 0f)
		{
			return;
		}
		float trackX = ClientSize.Width - U(7f);
		float trackY = U(4f);
		float trackH = ClientSize.Height - U(8f);
		using (GraphicsPath path = Sh.Capsule(new RectangleF(trackX, trackY, U(3f), trackH)))
		{
			Sh.Fill(g, path, Tok.Well);
		}
		// 底部渐隐：明确"下面还有内容"，避免看起来像被硬切
		if (_offset < _max - 1f)
		{
			RectangleF fade = new RectangleF(0f, ClientSize.Height - U(18f), ClientSize.Width - U(8f), U(18f));
			using System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(fade, Color.FromArgb(0, Tok.Canvas), Tok.Canvas, System.Drawing.Drawing2D.LinearGradientMode.Vertical);
			g.FillRectangle(brush, fade);
		}
		float ratio = ClientSize.Height / (float)(ClientSize.Height + _max);
		float thumbH = Math.Max(U(28f), trackH * ratio);
		float pos = (trackH - thumbH) * Math.Clamp(_max <= 0f ? 0f : _offset / _max, 0f, 1f);
		using (GraphicsPath path = Sh.Capsule(new RectangleF(trackX, trackY + pos, U(3f), thumbH)))
		{
			Sh.Fill(g, path, Tok.Mix(Tok.Line, Tok.Text3, Tracking ? 1f : 0.55f));
		}
	}
}
