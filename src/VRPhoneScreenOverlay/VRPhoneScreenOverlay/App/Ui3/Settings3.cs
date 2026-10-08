using System;
using System.Drawing;
using System.Windows.Forms;
using PocketDeck.Settings;

namespace PocketDeck.App.Ui3;

/// <summary>设置页：即时生效 → 画质与帧率 → 手柄与绑定 → 底部动作条（始终留在视口内）。</summary>
internal sealed class Settings3 : Page3
{
	private const float InstantHeight = 160f;

	private const float QualityHeight = 300f;

	private const float BindHeight = 116f;

	private const float ActionsHeight = 52f;

	private const float RowGap = 14f;

	private const float CardPad = 18f;

	private const float HeaderHeight = 46f;

	private const float CardTopPad = 10f;

	private const float SwitchRowHeight = 46f;

	private const float FieldPitch = 58f;

	private const float FieldLabelHeight = 18f;

	private const float ControlHeight = 38f;

	private string _qualityText = "等待手机";

	private Tone _qualityTone = Tone.Muted;

	private bool _dirty;

	private string _bindNotice = string.Empty;

	private Tone _bindTone = Tone.Muted;

	public Switch3 KeepAwake { get; } = new Switch3();

	public Switch3 UnlockKeypad { get; } = new Switch3();

	public Segments3<ControllerHandPreference> Hand { get; } = new Segments3<ControllerHandPreference>();

	public Segments3<VideoResolutionProfile> Resolution { get; } = new Segments3<VideoResolutionProfile>();

	public Stepper3<int> Bitrate { get; } = new Stepper3<int>();

	public Stepper3<int> FrameRate { get; } = new Stepper3<int>();

	public Button3 Bindings { get; } = new Button3 { Variant = Button3.Look.Ghost, Text = "手柄绑定", Icon = Icon3.Steam, AlignLeft = true };

	public Button3 Discard { get; } = new Button3 { Variant = Button3.Look.Ghost, Text = "撤销", Icon = Icon3.Undo };

	public Button3 Apply { get; } = new Button3 { Variant = Button3.Look.Primary, Text = "应用", Icon = Icon3.Check };

	public override void Build(Layout3 ctx)
	{
		Ctx = ctx;
		Stack3 root = ctx.Stack(RowGap);
		root.Add(Slot("instant", 0f, InstantHeight));
		root.Add(Slot("quality", 0f, QualityHeight));
		root.Add(Slot("bind", 0f, BindHeight));
		root.Add(Slot("actions", 0f, ActionsHeight));
		SetRoot(root);
		EnsureChildren();
	}

	private void EnsureChildren()
	{
		if (Controls.Contains(KeepAwake))
		{
			return;
		}
		foreach (Control control in new Control[] { KeepAwake, UnlockKeypad, Hand, Resolution, Bitrate, FrameRate, Bindings, Discard, Apply })
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
		float pad = CardPad * s;
		RectangleF instant = R("instant");
		float iTop = instant.Y + (HeaderHeight + CardTopPad) * s;
		float toggleY = iTop + (SwitchRowHeight * s - 24f * s) / 2f;
		KeepAwake.Bounds = new Rectangle((int)(instant.Right - pad - 42f * s), (int)toggleY, (int)(42f * s), (int)(24f * s));
		UnlockKeypad.Bounds = new Rectangle((int)(instant.Right - pad - 42f * s), (int)(toggleY + SwitchRowHeight * s), (int)(42f * s), (int)(24f * s));

		RectangleF quality = R("quality");
		float qTop = quality.Y + (HeaderHeight + CardTopPad) * s;
		float ctlW = quality.Width - pad * 2f;
		for (int i = 0; i < 4; i++)
		{
			float top = qTop + FieldPitch * s * i + FieldLabelHeight * s;
			Rectangle bounds = new Rectangle((int)(quality.X + pad), (int)top, (int)ctlW, (int)(ControlHeight * s));
			switch (i)
			{
				case 0:
					Hand.Bounds = bounds;
					break;
				case 1:
					Resolution.Bounds = bounds;
					break;
				case 2:
					Bitrate.Bounds = bounds;
					break;
				default:
					FrameRate.Bounds = bounds;
					break;
			}
		}

		RectangleF bind = R("bind");
		float bTop = bind.Y + (HeaderHeight + CardTopPad) * s;
		Bindings.Bounds = new Rectangle((int)(bind.X + pad), (int)bTop, (int)(160f * s), (int)(40f * s));

		RectangleF actions = R("actions");
		Apply.Bounds = new Rectangle((int)(actions.Right - 108f * s), (int)actions.Y, (int)(108f * s), (int)(46f * s));
		Discard.Bounds = new Rectangle((int)(actions.Right - 108f * s - 10f * s - 100f * s), (int)actions.Y, (int)(100f * s), (int)(46f * s));
	}

	public void SetQuality(string text, Tone tone)
	{
		_qualityText = text;
		_qualityTone = tone;
		Invalidate();
	}

	public void SetDirty(bool dirty)
	{
		if (_dirty == dirty)
		{
			return;
		}
		_dirty = dirty;
		Invalidate();
	}

	public void SetBindingNotice(string text, Tone tone)
	{
		_bindNotice = text ?? string.Empty;
		_bindTone = tone;
		Invalidate();
	}

	/// <summary>绑定动作可用性（操作进行中禁用）。</summary>
	public void SetBindingNoticeEnabled(bool enabled)
	{
		if (Bindings.Enabled != enabled)
		{
			Bindings.Enabled = enabled;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.Clear(ClearColor);
		float s = Base3.Scale;
		PaintInstant(g, R("instant"), s);
		PaintQuality(g, R("quality"), s);
		PaintBind(g, R("bind"), s);
		PaintActions(g, R("actions"), s);
	}

	private void PaintInstant(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "即时生效", "无需重启浮窗");
		float pad = CardPad * s;
		float top = rect.Y + (HeaderHeight + CardTopPad) * s;
		float textW = rect.Width - pad * 2f - 60f * s;
		Draw3.SettingText(g, new RectangleF(rect.X + pad, top, textW, SwitchRowHeight * s), s, "抓握时保持亮屏", "实验特性：抓住浮窗时手机屏幕保持点亮");
		Sh.Hair(g, rect.X + pad, top + SwitchRowHeight * s, rect.Width - pad * 2f, Tok.LineSoft, s);
		Draw3.SettingText(g, new RectangleF(rect.X + pad, top + SwitchRowHeight * s, textW, SwitchRowHeight * s), s, "VR 数字键盘", "实验特性：密码锁屏后在 VR 中解锁");
	}

	private void PaintQuality(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "画质与帧率", null, Tone.Muted, _qualityText, _qualityTone);
		float pad = CardPad * s;
		float top = rect.Y + (HeaderHeight + CardTopPad) * s;
		string[] labels = new string[4] { "惯用手", "分辨率", "码率", "帧率" };
		for (int i = 0; i < labels.Length; i++)
		{
			Sh.Text(g, labels[i], Tok.Caption(true), Tok.Text3, new RectangleF(rect.X + pad, top + FieldPitch * s * i, rect.Width - pad * 2f, FieldLabelHeight * s), ContentAlignment.MiddleLeft);
		}
	}

	private void PaintBind(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "手柄与绑定");
		float pad = CardPad * s;
		float top = rect.Y + (HeaderHeight + CardTopPad) * s;
		float left = rect.X + pad + 176f * s;
		float width = Math.Max(40f, rect.Right - pad - left);
		Sh.Text(g, "SteamVR 手柄绑定", Tok.Body(true), Tok.Text1, new RectangleF(left, top + 1f * s, width, 20f * s), ContentAlignment.MiddleLeft);
		Sh.Text(g, string.IsNullOrEmpty(_bindNotice) ? "本地绑定正常" : _bindNotice, Tok.Caption(), string.IsNullOrEmpty(_bindNotice) ? Tok.Text3 : Tok.Tone(_bindTone), new RectangleF(left, top + 21f * s, width, 18f * s), ContentAlignment.MiddleLeft);
	}

	private void PaintActions(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		if (_dirty)
		{
			SizeF chip = Draw3.MeasureChip(g, "有未应用的修改", s);
			Draw3.Chip(g, new RectangleF(rect.X, rect.Y + (rect.Height - chip.Height) / 2f, chip.Width, chip.Height), s, "有未应用的修改", Tone.Warn);
		}
		else
		{
			Sh.Text(g, "设置已保存", Tok.Caption(), Tok.Text3, new RectangleF(rect.X, rect.Y, 220f * s, rect.Height), ContentAlignment.MiddleLeft);
			Sh.Text(g, "改动会统一应用，最多重启一次浮窗", Tok.Caption(), Tok.Text3, new RectangleF(rect.X + 220f * s, rect.Y, Math.Max(20f, rect.Right - rect.X - 550f * s), rect.Height), ContentAlignment.MiddleLeft);
		}
	}
}
