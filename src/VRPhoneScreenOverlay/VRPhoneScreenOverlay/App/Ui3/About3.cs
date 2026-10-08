using System;
using System.Drawing;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>关于页：产品标识 + 三项信息块 + 快速上手三步 + 页脚。</summary>
internal sealed class About3 : Page3
{
	private string _version = "--";

	private string _appId = "--";

	private string _logState = "--";

	private string _productName = "PocketDeck VR";

	private string _tagline = "在 VR 里悬浮你的手机屏幕";

	private string _footer = string.Empty;

	private string _licenseName = "Boost Software License 1.0";

	private string _thirdPartyState = "随程序 licenses 目录";

	private RectangleF _licenseViewHit;

	private RectangleF _licenseFolderHit;

	public Button3 CheckUpdate { get; } = new Button3 { Variant = Button3.Look.Soft, Text = "检查更新", Icon = Icon3.Clock };

	public Button3 Feedback { get; } = new Button3 { Variant = Button3.Look.Ghost, Text = "问题反馈", Icon = Icon3.Info };

	public Switch3 AutoCheck { get; } = new Switch3();

	public Button3 ChannelToggle { get; } = new Button3 { Variant = Button3.Look.Ghost, Text = "通道：稳定版", Icon = Icon3.Undo };

	public Button3 OpenDownload { get; } = new Button3 { Variant = Button3.Look.Primary, Text = "前往下载", Icon = Icon3.Sparkle, Visible = false };

	private string _updateState = "尚未检查更新";

	private Tone _updateTone = Tone.Muted;

	private bool _updateAvailable;

	private string _latestVersion = string.Empty;

	private string _updateNotes = string.Empty;

	/// <summary>点击「本项目许可」。</summary>
	public event EventHandler? LicenseViewRequested;

	/// <summary>点击「第三方组件」。</summary>
	public event EventHandler? LicenseFolderRequested;

	public void SetIdentity(string productName, string tagline)
	{
		_productName = productName;
		_tagline = tagline;
		Invalidate();
	}

	public void SetDetails(string version, string appId, string logState)
	{
		_version = string.IsNullOrWhiteSpace(version) ? "--" : version;
		_appId = string.IsNullOrWhiteSpace(appId) ? "--" : appId;
		_logState = string.IsNullOrWhiteSpace(logState) ? "--" : logState;
		Invalidate();
	}

	public void SetLicense(string licenseName, string thirdPartyState)
	{
		_licenseName = string.IsNullOrWhiteSpace(licenseName) ? _licenseName : licenseName;
		_thirdPartyState = string.IsNullOrWhiteSpace(thirdPartyState) ? _thirdPartyState : thirdPartyState;
		Invalidate();
	}

	/// <summary>更新状态（结果文本、语义色、是否有新版、新版号、更新内容）。</summary>
	public void SetUpdateState(string state, Tone tone, bool available, string latestVersion, string notes)
	{
		_updateState = string.IsNullOrWhiteSpace(state) ? "尚未检查更新" : state;
		_updateTone = tone;
		_updateAvailable = available;
		_latestVersion = latestVersion ?? string.Empty;
		_updateNotes = notes ?? string.Empty;
		OpenDownload.Visible = available;
		Invalidate();
	}

	public void SetChannelText(string text)
	{
		ChannelToggle.Text = "通道：" + text;
		Invalidate();
	}

	public void SetAutoCheck(bool value)
	{
		AutoCheck.Checked = value;
	}

	public void SetFooter(string text)
	{
		_footer = text ?? string.Empty;
		Invalidate();
	}

	public override void Build(Layout3 ctx)
	{
		Ctx = ctx;
		Stack3 root = ctx.Stack(16f);
		root.Add(Slot("hero", 0f, 148f));
		root.Add(Slot("tiles", 0f, 96f));
		root.Add(Slot("update", 0f, 226f));
		root.Add(Slot("license", 0f, 152f));
		root.Add(Slot("steps", 0f, 246f));
		SetRoot(root);
		EnsureChildren();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.Clear(ClearColor);
		float s = Base3.Scale;
		PaintHero(g, R("hero"), s);
		PaintTiles(g, R("tiles"), s);
		PaintUpdate(g, R("update"), s);
		PaintLicense(g, R("license"), s);
		PaintSteps(g, R("steps"), s);
	}

	private void PaintHero(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		float pad = 26f * s;
		float mark = 56f * s;
		RectangleF markRect = new RectangleF(rect.X + pad, rect.Y + (rect.Height - mark) / 2f, mark, mark);
		Draw3.IconTile(g, markRect, s, Icon3.Sparkle, Tone.Accent, filled: true);
		float left = markRect.Right + 20f * s;
		Sh.Text(g, _productName, Tok.Hero(), Tok.Text1, new RectangleF(left, rect.Y + 34f * s, rect.Width - (left - rect.X) - pad, 32f * s), ContentAlignment.MiddleLeft);
		Sh.Text(g, _tagline, Tok.Body(), Tok.Text3, new RectangleF(left, rect.Y + 68f * s, rect.Width - (left - rect.X) - pad, 22f * s), ContentAlignment.MiddleLeft);
		SizeF chip = Draw3.MeasureChip(g, "v" + _version, s);
		Draw3.Chip(g, new RectangleF(left, rect.Y + 96f * s, chip.Width, chip.Height), s, "v" + _version, Tone.Accent, dot: false);
		Sh.Text(g, _footer, Tok.Caption(), Tok.Text3, new RectangleF(rect.X + pad, rect.Y + rect.Height - 30f * s, rect.Width - pad * 2f, 20f * s), ContentAlignment.MiddleLeft);
	}

	private void PaintTiles(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		float gap = 16f * s;
		float w = (rect.Width - gap * 2f) / 3f;
		Draw3.InfoTile(g, new RectangleF(rect.X, rect.Y, w, rect.Height), s, Icon3.Sparkle, "版本", _version);
		Draw3.InfoTile(g, new RectangleF(rect.X + w + gap, rect.Y, w, rect.Height), s, Icon3.Shield, "应用标识", _appId);
		Draw3.InfoTile(g, new RectangleF(rect.X + (w + gap) * 2f, rect.Y, w, rect.Height), s, Icon3.Clock, "本地日志", _logState);
	}

	private void EnsureChildren()
	{
		if (Controls.Contains(CheckUpdate))
		{
			return;
		}
		foreach (Control control in new Control[] { CheckUpdate, Feedback, AutoCheck, ChannelToggle, OpenDownload })
		{
			if (control is Base3 base3)
			{
				base3.ClearColor = Tok.Surface;
			}
			Controls.Add(control);
		}
	}

	protected override void PlaceControls()
	{
		float s = Base3.Scale;
		RectangleF rect = R("update");
		if (rect.Width <= 1f)
		{
			return;
		}
		float pad = 18f * s;
		float right = rect.Right - pad;
		float top = rect.Y + 46f * s + 8f * s;
		CheckUpdate.Bounds = new Rectangle((int)(right - 120f * s), (int)top, (int)(120f * s), (int)(30f * s));
		Feedback.Bounds = new Rectangle((int)(right - 120f * s - 8f * s - 104f * s), (int)top, (int)(104f * s), (int)(30f * s));
		AutoCheck.Bounds = new Rectangle((int)(rect.X + pad + 128f * s), (int)(top + 38f * s), (int)(42f * s), (int)(24f * s));
		ChannelToggle.Bounds = new Rectangle((int)(rect.X + pad + 250f * s), (int)(top + 34f * s), (int)(132f * s), (int)(30f * s));
		OpenDownload.Bounds = new Rectangle((int)(right - 112f * s), (int)(rect.Bottom - pad - 32f * s), (int)(112f * s), (int)(32f * s));
	}

	private void PaintUpdate(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "更新");
		float pad = 18f * s;
		float left = rect.X + pad;
		float width = rect.Width - pad * 2f;
		float top = rect.Y + 46f * s + 8f * s;
		string versionLine = "当前版本 v" + _version + (_latestVersion.Length > 0 ? "　·　最新 v" + _latestVersion : string.Empty);
		Sh.Text(g, versionLine, Tok.Body(), Tok.Text2, new RectangleF(left, top, width - 200f * s, 22f * s), ContentAlignment.MiddleLeft);
		Sh.Text(g, "自动检查更新", Tok.Body(), Tok.Text3, new RectangleF(left, top + 38f * s, 120f * s, 24f * s), ContentAlignment.MiddleLeft);
		RectangleF dot = new RectangleF(left, top + 70f * s + 8f * s, 8f * s, 8f * s);
		using (SolidBrush dotBrush = new SolidBrush(_updateAvailable ? Tok.Ok : ((_updateTone == Tone.Bad) ? Tok.Bad : Tok.Text3)))
		{
			g.FillEllipse(dotBrush, dot);
		}
		Sh.Text(g, _updateState, Tok.Body(), _updateAvailable ? Tok.Text1 : Tok.Text3, new RectangleF(left + 16f * s, top + 70f * s, width - 146f * s, 22f * s), ContentAlignment.MiddleLeft);
		if (_updateNotes.Length > 0)
		{
			DrawWrapped(g, "更新内容：" + _updateNotes, Tok.Caption(), Tok.Text3, new RectangleF(left, top + 96f * s, width - 150f * s, 58f * s), 16f * s, 3);
		}
		else
		{
			DrawWrapped(g, _updateAvailable ? string.Empty : "有新版本时会在这里显示更新内容；下载交给浏览器，程序不会自动安装。", Tok.Caption(), Tok.Text3, new RectangleF(left, top + 96f * s, width - 150f * s, 58f * s), 16f * s, 3);
		}
	}

	/// <summary>中英混排手工折行（避免依赖不确定的排版 API），超出 maxLines 加省略号。</summary>
	private static void DrawWrapped(Graphics g, string text, Font font, Color color, RectangleF rect, float lineHeight, int maxLines)
	{
		if (text.Length == 0 || rect.Width <= 1f)
		{
			return;
		}
		System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
		string current = string.Empty;
		foreach (char c in text)
		{
			if (c == '\n')
			{
				lines.Add(current);
				current = string.Empty;
				continue;
			}
			string candidate = current + c;
			if (g.MeasureString(candidate, font).Width > rect.Width && current.Length > 0)
			{
				lines.Add(current);
				current = c.ToString();
			}
			else
			{
				current = candidate;
			}
		}
		if (current.Length > 0)
		{
			lines.Add(current);
		}
		using SolidBrush brush = new SolidBrush(color);
		for (int i = 0; i < lines.Count && i < maxLines; i++)
		{
			string line = lines[i];
			if (i == maxLines - 1 && lines.Count > maxLines)
			{
				line += "…";
			}
			g.DrawString(line, font, brush, rect.X, rect.Y + i * lineHeight);
		}
	}

	private void PaintLicense(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "许可证");
		float pad = 18f * s;
		float top = rect.Y + 46f * s + 6f * s;
		float rowH = 50f * s;
		_licenseViewHit = new RectangleF(rect.X + pad, top, rect.Width - pad * 2f, rowH);
		_licenseFolderHit = new RectangleF(rect.X + pad, top + rowH, rect.Width - pad * 2f, rowH);
		Draw3.Row(g, _licenseViewHit, s, Icon3.Shield, Tone.Accent, "本项目许可", "以开源许可分发，点击查看全文", _licenseName, Tone.Accent);
		Draw3.Row(g, _licenseFolderHit, s, Icon3.Info, Tone.Info, "第三方组件", "各组件许可见安装目录", "打开目录", Tone.Info);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (_licenseViewHit.Contains(e.Location))
		{
			LicenseViewRequested?.Invoke(this, EventArgs.Empty);
		}
		else if (_licenseFolderHit.Contains(e.Location))
		{
			LicenseFolderRequested?.Invoke(this, EventArgs.Empty);
		}
	}

	private void PaintSteps(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "快速上手");
		float pad = 18f * s;
		float top = rect.Y + 46f * s + 14f * s;
		float stepH = 58f * s;
		Draw3.Step(g, new RectangleF(rect.X + pad, top, rect.Width - pad * 2f, stepH), s, 1, "用 USB 连一次手机", "首次接入请用数据线连接手机并允许 USB 调试。");
		Draw3.Step(g, new RectangleF(rect.X + pad, top + stepH, rect.Width - pad * 2f, stepH), s, 2, "自动开通 WiFi 无线", "插着数据线即可，程序会自动切到无线调试并记住地址，之后就能拔掉数据线。");
		Draw3.Step(g, new RectangleF(rect.X + pad, top + stepH * 2f, rect.Width - pad * 2f, stepH), s, 3, "戴上头显开始", "点「开启手机浮窗」，在 VR 里按住抓握键即可搬动浮窗。");
	}
}
