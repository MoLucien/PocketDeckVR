using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>应用图标（读取 exe 关联图标，随新品牌图标自动更新）。</summary>
internal sealed class AppMark3 : Base3
{
	private readonly Bitmap? _icon;

	public AppMark3()
	{
		SetStyle(ControlStyles.Selectable, false);
		if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
		{
			try
			{
				using Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
				_icon = icon?.ToBitmap();
			}
			catch (Exception)
			{
				_icon = null;
			}
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.Clear(Tok.Canvas);
		if (_icon == null)
		{
			return;
		}
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.DrawImage(_icon, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_icon?.Dispose();
		}
		base.Dispose(disposing);
	}
}

/// <summary>
/// v3 外壳：自绘标题栏（原生拖拽 + 窗口按钮）+ 顶部标签栏 + 可滚动内容 + 底部状态栏 + Toast 浮层。
/// 窗口可缩放，内容按宽度在双列/单列之间折叠。
/// </summary>
internal sealed class Shell3 : Base3
{
	private const int WM_NCLBUTTONDOWN = 161;

	private const int HTCAPTION = 2;

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	private readonly AppMark3 _mark = new AppMark3();

	private readonly IconButton3 _minimize = new IconButton3 { Icon = Icon3.Minus };

	private readonly IconButton3 _close = new IconButton3 { Icon = Icon3.Close, Tone = Tone.Bad, HoverFill = Tok.Tint(Tok.Bad, Tok.Surface, 0.22f) };

	private readonly Chip3 _headerStatus = new Chip3 { Dot = true };

	private readonly TabBar3 _tabs = new TabBar3();

	private readonly ScrollHost3 _scroll = new ScrollHost3();

	private readonly StatusBar3 _status = new StatusBar3();

	private readonly ToastHost3 _toasts = new ToastHost3();

	private readonly List<Page3> _pages = new List<Page3>();

	private readonly List<Panel3> _pageHosts = new List<Panel3>();

	private string _productName = "PocketDeck VR";

	private bool _twoColumn;

	private bool _built;

	private int _selected;

	public Shell3()
	{
		SetStyle(ControlStyles.Selectable, false);
		Controls.Add(_scroll);
		Controls.Add(_toasts);
		Controls.Add(_tabs);
		Controls.Add(_headerStatus);
		Controls.Add(_mark);
		Controls.Add(_minimize);
		Controls.Add(_close);
		Controls.Add(_status);
		_tabs.SelectedIndexChanged += (_, _) => SelectPage(_tabs.SelectedIndex);
		_toasts.ContentChanged += (_, _) => Relayout();
		_scroll.SendToBack();
		_toasts.BringToFront();
		_minimize.Click += (_, _) =>
		{
			if (FindForm() is Form form)
			{
				form.WindowState = FormWindowState.Minimized;
			}
		};
		_close.Click += (_, _) => FindForm()?.Close();
		_mark.MouseDown += (_, e) => BeginDrag(e);
		_headerStatus.MouseDown += (_, e) => BeginDrag(e);
	}

	public TabBar3 Tabs => _tabs;

	public StatusBar3 Status => _status;

	public int SelectedPage => _selected;

	public float ScaleValue { get; private set; } = 1f;

	public void SetProductName(string name)
	{
		_productName = name;
		Invalidate();
	}

	public void SetPages(params Page3[] pages)
	{
		foreach (Panel3 host in _pageHosts)
		{
			host.Dispose();
		}
		_pageHosts.Clear();
		_pages.Clear();
		foreach (Page3 page in pages)
		{
			Panel3 host = new Panel3 { Fill = Tok.Canvas, Edge = Color.Transparent, TopHighlight = false, Radius = 0f, ClearColor = Tok.Canvas };
			page.ClearColor = Tok.Canvas;
			host.Controls.Add(page);
			_pages.Add(page);
			_pageHosts.Add(host);
		}
		_selected = 0;
		if (_pageHosts.Count > 0)
		{
			_scroll.SetContent(_pageHosts[0]);
		}
		_tabs.SetTabs(new string[_pages.Count]);
		// 页面在 SetPages 之前可能已经触发过一次布局，这里强制重建，确保每页都拿到布局树。
		_built = false;
		Relayout();
	}

	public void SetTabTitles(params string[] titles)
	{
		_tabs.SetTabs(titles);
		_tabs.Select(_selected, silent: true);
		Invalidate();
	}

	public void SelectPage(int index)
	{
		if (index < 0 || index >= _pages.Count)
		{
			return;
		}
		_selected = index;
		_scroll.SetContent(_pageHosts[index]);
		_tabs.Select(index, silent: true);
		Relayout();
	}

	public Page3 Page(int index)
	{
		return _pages[index];
	}

	public void SetHeaderStatus(string text, Tone tone, bool live)
	{
		_headerStatus.Text = text ?? string.Empty;
		_headerStatus.Tone = tone;
		_headerStatus.Live = live;
		float width = TextRenderer.MeasureText(text ?? string.Empty, Tok.Caption(true), Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + U(44f);
		_headerStatus.Width = (int)Math.Max(U(80f), width);
		Relayout();
	}

	public void Toast(string title, string message, Tone tone, Icon3 icon)
	{
		_toasts.Push(title, message, tone, icon);
	}

	/// <summary>DPI/缩放变化：重设比例并重建页面布局。</summary>
	public void ApplyScale(float scale)
	{
		if (MathF.Abs(scale - ScaleValue) < 0.001f)
		{
			return;
		}
		ScaleValue = scale;
		Base3.Scale = scale;
		RebuildPages(force: true);
	}

	public void Relayout()
	{
		float scale = ScaleValue;
		int w = ClientSize.Width;
		int h = ClientSize.Height;
		if (w <= 0 || h <= 0)
		{
			return;
		}
		bool twoColumn = (w / scale) >= Tok.TwoColumnMinWidth;
		if (!_built || twoColumn != _twoColumn)
		{
			_twoColumn = twoColumn;
			_built = true;
			RebuildPages(force: false);
		}
		float header = U(Tok.HeaderHeight);
		float tabBar = U(Tok.TabBarHeight);
		float statusBar = U(Tok.StatusBarHeight);
		_mark.Bounds = new Rectangle((int)U(18f), (int)((header - U(26f)) / 2f), (int)U(26f), (int)U(26f));
		int winSize = (int)U(34f);
		_close.Bounds = new Rectangle(w - (int)U(16f) - winSize, (int)((header - winSize) / 2f), winSize, winSize);
		_minimize.Bounds = new Rectangle(_close.Left - (int)U(6f) - winSize, _close.Top, winSize, winSize);
		int chipRight = _minimize.Left - (int)U(14f);
		_headerStatus.Bounds = new Rectangle(chipRight - _headerStatus.Width, (int)((header - U(24f)) / 2f), _headerStatus.Width, (int)U(24f));
		_tabs.Bounds = new Rectangle((int)U(10f), (int)header, w - (int)U(20f), (int)tabBar);
		_status.Bounds = new Rectangle(0, h - (int)statusBar, w, (int)statusBar);
		// Toast 放在状态栏正上方（右下角）：不与顶部操作区争位置；空时收成 0 尺寸。
		int toastHeight = _toasts.IsEmpty ? 0 : Math.Min((int)U(_toasts.NeededHeight), Math.Max(0, h - (int)(header + tabBar + statusBar + U(20f))));
		_toasts.Bounds = _toasts.IsEmpty
			? Rectangle.Empty
			: new Rectangle(w - (int)U(340f), h - (int)statusBar - toastHeight - (int)U(12f), (int)U(336f), toastHeight);
		// 浮层收起或移位后，被遮住的区域必须连同子控件一起重绘，否则留下残影。
		_scroll.Invalidate(true);
		_scroll.Bounds = new Rectangle(0, (int)(header + tabBar), w, Math.Max(1, h - (int)(header + tabBar + statusBar)));
		LayoutPages();
		_status.Right = _productName;
		Invalidate();
	}

	private void RebuildPages(bool force)
	{
		using Graphics g = CreateGraphics();
		float width = Math.Max(U(320f), ClientSize.Width - U(Tok.ContentPad) * 2f - U(8f));
		foreach (Page3 page in _pages)
		{
			Layout3 ctx = new Layout3 { Scale = ScaleValue, Width = width, Height = ClientSize.Height, TwoColumn = _twoColumn };
			page.Build(ctx);
			page.Width = (int)width;
		}
		GC.KeepAlive(g);
	}

	private void LayoutPages()
	{
		using Graphics g = CreateGraphics();
		float width = Math.Max(U(320f), ClientSize.Width - U(Tok.ContentPad) * 2f - U(8f));
		foreach (Panel3 host in _pageHosts)
		{
			host.Width = (int)width + (int)U(Tok.ContentPad) * 2;
		}
		foreach (Page3 page in _pages)
		{
			page.Location = new Point((int)U(Tok.ContentPad), (int)U(Tok.ContentPad));
			page.Width = (int)width;
			page.RunLayout(g, width, ClientSize.Height);
			page.Height = (int)Math.Max(page.ContentHeight, ClientSize.Height - U(Tok.ContentPad) * 2f);
			if (page.Parent is Control parent)
			{
				parent.Height = page.Height + (int)U(Tok.ContentPad) * 2;
			}
		}
		_scroll.UpdateBounds();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Relayout();
	}

	private void BeginDrag(MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left)
		{
			return;
		}
		Form form = FindForm();
		if (form == null)
		{
			return;
		}
		ReleaseCapture();
		SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Tok.Canvas);
		float scale = ScaleValue;
		float header = U(Tok.HeaderHeight);
		float tabBar = U(Tok.TabBarHeight);
		float statusBar = U(Tok.StatusBarHeight);
		Sh.Text(g, _productName, Tok.Body(true), Tok.Text1, new RectangleF(U(54f), 0f, ClientSize.Width * 0.5f, header), ContentAlignment.MiddleLeft);
		Sh.Hair(g, 0f, header - 1f, ClientSize.Width, Tok.LineSoft, scale);
		Sh.Hair(g, 0f, header + tabBar - 1f, ClientSize.Width, Tok.LineSoft, scale);
		if (ClientSize.Height > statusBar)
		{
			Sh.Hair(g, 0f, ClientSize.Height - statusBar, ClientSize.Width, Tok.LineSoft, scale);
		}
	}
}
