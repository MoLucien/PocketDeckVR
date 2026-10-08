using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>
/// 主页：英雄动作区（状态 + 主操作）→ 信号管线（手机→视频→音频→控制→SteamVR）
/// → 链路与设备 / 运行数据 → 空间拖拽。
/// </summary>
internal sealed class Home3 : Page3
{
	private sealed class StreamState
	{
		public string Name = string.Empty;

		public string State = "未运行";

		public Tone Tone = Tone.Muted;

		public bool Live;
	}

	private readonly StreamState[] _streams = new StreamState[4]
	{
		new StreamState { Name = "视频流" },
		new StreamState { Name = "音频流" },
		new StreamState { Name = "控制流" },
		new StreamState { Name = "SteamVR" },
	};

	private string _phoneState = "等待设备";

	private Tone _phoneTone = Tone.Muted;

	private bool _phoneLive;

	private string _adbState = "等待设备";

	private Tone _adbTone = Tone.Info;

	private bool _adbLive;

	private string _phoneDetail = "未选择手机";

	private string _adbDetail = "内置 ADB 已就绪";

	private string _wifiDetail = "首次用 USB 连接后会自动开通";

	private string _wifiState = "未开启";

	private Tone _wifiTone = Tone.Muted;

	private string _heroTitle = "手机浮窗未运行";

	private string _heroSubtitle = "连接手机后即可把屏幕搬进 SteamVR";

	private string _metricResolution = "--";

	private string _metricBitrate = "--";

	private string _metricFps = "--";

	private string _metricLatency = "--";

	private float _rRes;

	private float _rBitrate;

	private float _rFps;

	private float _rLatency;

	private string _playspaceState = "已关闭";

	private Tone _playspaceTone = Tone.Muted;

	private string _axisX = "0.00";

	private string _axisY = "0.00";

	private string _axisZ = "0.00";

	public Button3 Action { get; } = new Button3 { Variant = Button3.Look.Primary, Text = "开启手机浮窗", Icon = Icon3.Play };

	public Button3 Probe { get; } = new Button3 { Variant = Button3.Look.Ghost, Text = "画质探测", Icon = Icon3.Gauge };


	public Switch3 PlayspaceSwitch { get; } = new Switch3();

	public Stepper3<int> Multiplier { get; } = new Stepper3<int>();

	private readonly Meter3[] _meters = new Meter3[4]
	{
		new Meter3 { Caption = "分辨率", Icon = Icon3.Monitor },
		new Meter3 { Caption = "码率", Icon = Icon3.Gauge },
		new Meter3 { Caption = "帧率", Icon = Icon3.Clock },
		new Meter3 { Caption = "延迟", Icon = Icon3.Link },
	};

	public override void Build(Layout3 ctx)
	{
		Ctx = ctx;
		bool twoColumn = ctx.TwoColumn;
		Stack3 root = ctx.Stack(16f);
		root.Add(Slot("hero", 0f, 104f));
		root.Add(Slot("pipe", 0f, 62f));
		if (twoColumn)
		{
			root.Add(ctx.Row(16f,
				(Slot("link", 0f, 218f), 1f),
				(Slot("metrics", Tok.RightColumnWidth, 218f), 0f)));
		}
		else
		{
			root.Add(Slot("link", 0f, 218f));
			root.Add(Slot("metrics", 0f, 218f));
		}
		root.Add(Slot("space", 0f, 228f));
		SetRoot(root);
		EnsureChildren();
	}

	private void EnsureChildren()
	{
		if (!Controls.Contains(Action))
		{
			Action.ClearColor = Tok.Surface;
			Probe.ClearColor = Tok.Surface;
			PlayspaceSwitch.SurfaceColor = Tok.Surface;
			Multiplier.ClearColor = Tok.Surface;
			Controls.Add(Action);
			Controls.Add(Probe);
			Controls.Add(PlayspaceSwitch);
			Controls.Add(Multiplier);
			foreach (Meter3 meter in _meters)
			{
				meter.ClearColor = Tok.Surface;
				Controls.Add(meter);
			}
		}
	}

	protected override void PlaceControls()
	{
		float s = Base3.Scale;
		RectangleF hero = R("hero");
		float actionW = 194f * s;
		float probeW = 120f * s;
		float gap = 10f * s;
		float y = hero.Y + (hero.Height - 46f * s) / 2f;
		float right = hero.Right - 22f * s;
		Action.Bounds = new Rectangle((int)(right - actionW), (int)y, (int)actionW, (int)(46f * s));
		Probe.Bounds = new Rectangle((int)(right - actionW - gap - probeW), (int)(y + 4f * s), (int)probeW, (int)(38f * s));

		RectangleF link = R("link");

		RectangleF space = R("space");
		float contentTop = space.Y + 46f * s + 12f * s;
		PlayspaceSwitch.Bounds = new Rectangle((int)(space.Right - 18f * s - 42f * s), (int)(contentTop + (44f * s - 24f * s) / 2f), (int)(42f * s), (int)(24f * s));
		float stepperTop = contentTop + 44f * s + 8f * s + 58f * s + 26f * s;
		Multiplier.Bounds = new Rectangle((int)(space.X + 18f * s), (int)stepperTop, (int)(280f * s), (int)(54f * s));

		RectangleF metrics = R("metrics");
		if (metrics.Width > 1f)
		{
			float pad = 18f * s;
			float top = metrics.Y + 46f * s + 10f * s;
			float rowH = 40f * s;
			for (int i = 0; i < _meters.Length; i++)
			{
				_meters[i].Bounds = new Rectangle((int)(metrics.X + pad), (int)(top + rowH * i), (int)(metrics.Width - pad * 2f), (int)rowH);
			}
		}
	}

	// ============================ 状态写入（MainForm 调用） ============================

	/// <summary>手机/链路状态（左上状态点）。</summary>
	/// <summary>手机总体状态（用于英雄区胶囊与信号管线的手机节点）。</summary>
	public void SetPhoneStatus(string text, Tone tone)
	{
		_phoneState = text ?? string.Empty;
		_phoneTone = tone;
		if (tone == Tone.Ok)
		{
			_phoneLive = true;
			Animate();
		}
		Invalidate();
	}

	/// <summary>ADB 行状态：只反映"有线（USB）链路"本身，与手机总体状态解耦。</summary>
	public void SetAdbStatus(string text, Tone tone, bool live)
	{
		_adbState = text ?? string.Empty;
		_adbTone = tone;
		_adbLive = live;
		if (live)
		{
			Animate();
		}
		Invalidate();
	}

	public void SetAdbDetail(string text)
	{
		_adbDetail = text ?? string.Empty;
		Invalidate();
	}

	public void SetPhoneDetail(string text)
	{
		_phoneDetail = text ?? string.Empty;
		Invalidate();
	}

	/// <summary>WiFi 行状态：与 ADB 行同一种表达（右侧圆点 + 文本），哪种链路在用就在哪行显示已连接。</summary>
	public void SetWifiStatus(string text, Tone tone)
	{
		_wifiState = text ?? string.Empty;
		_wifiTone = tone;
		Invalidate();
	}

	public void SetWifiDetail(string text)
	{
		_wifiDetail = text ?? string.Empty;
		Invalidate();
	}

	/// <summary>指标：0=分辨率 1=码率 2=帧率 3=延迟。</summary>
	public void SetMetric(int index, string text)
	{
		if (index < 0 || index >= _meters.Length)
		{
			return;
		}
		switch (index)
		{
			case 0:
				_metricResolution = text;
				break;
			case 1:
				_metricBitrate = text;
				break;
			case 2:
				_metricFps = text;
				break;
			default:
				_metricLatency = text;
				break;
		}
		_meters[index].SetValue(text, RatioFor(index, text));
		Invalidate();
	}

	/// <summary>把原始数值换算成进度条比例（仅表达视觉强度）。</summary>
	private static float RatioFor(int index, string text)
	{
		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		foreach (char c in text ?? string.Empty)
		{
			if ((c >= '0' && c <= '9') || c == '.')
			{
				builder.Append(c);
			}
			else if (builder.Length > 0)
			{
				break;
			}
		}
		if (!float.TryParse(builder.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) || value <= 0f)
		{
			return 0f;
		}
		return index switch
		{
			0 => 1f,
			1 => Math.Clamp(value / 32f, 0.08f, 1f),
			2 => Math.Clamp(value / 90f, 0.08f, 1f),
			_ => Math.Clamp(1f - value / 150f, 0.06f, 1f),
		};
	}

	public void SetPlayspaceStatus(string text, Tone tone)
	{
		_playspaceState = text ?? string.Empty;
		_playspaceTone = tone;
		Invalidate();
	}

	/// <summary>坐标文本（"X 0.12  Y 0.04  Z -0.28 m"）拆到三个读数块。</summary>
	public void SetCoordinates(string text)
	{
		string raw = (text ?? string.Empty).Replace("X", " ").Replace("Y", " ").Replace("Z", " ").Replace("m", " ");
		string[] parts = raw.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length >= 3)
		{
			_axisX = parts[0];
			_axisY = parts[1];
			_axisZ = parts[2];
		}
		else
		{
			_axisX = "--";
			_axisY = "--";
			_axisZ = "--";
		}
		Invalidate();
	}

	public void SetPhone(string state, Tone tone, bool live, string adbDetail, string phoneDetail, string wifiDetail)
	{
		_phoneState = state;
		_phoneTone = tone;
		_phoneLive = live;
		_adbDetail = adbDetail ?? string.Empty;
		_phoneDetail = phoneDetail ?? string.Empty;
		_wifiDetail = wifiDetail ?? string.Empty;
		Invalidate();
	}

	public void SetHero(string title, string subtitle)
	{
		_heroTitle = title;
		_heroSubtitle = subtitle;
		Invalidate();
	}

	/// <summary>index：0=视频流 1=音频流 2=控制流 3=SteamVR。</summary>
	public void SetStream(int index, string state, Tone tone, bool live)
	{
		if (index < 0 || index >= _streams.Length)
		{
			return;
		}
		_streams[index].State = state;
		_streams[index].Tone = tone;
		_streams[index].Live = live;
		Invalidate();
	}

	public void SetMetrics(string resolution, float resRatio, string bitrate, float bitrateRatio, string fps, float fpsRatio, string latency, float latencyRatio)
	{
		_metricResolution = resolution;
		_metricBitrate = bitrate;
		_metricFps = fps;
		_metricLatency = latency;
		_rRes = resRatio;
		_rBitrate = bitrateRatio;
		_rFps = fpsRatio;
		_rLatency = latencyRatio;
		_meters[0].SetValue(resolution, resRatio);
		_meters[1].SetValue(bitrate, bitrateRatio);
		_meters[2].SetValue(fps, fpsRatio);
		_meters[3].SetValue(latency, latencyRatio);
		Invalidate();
	}

	public void SetPlayspace(string state, Tone tone, string x, string y, string z)
	{
		_playspaceState = state;
		_playspaceTone = tone;
		_axisX = x;
		_axisY = y;
		_axisZ = z;
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.Clear(ClearColor);
		float s = Base3.Scale;
		PaintHero(g, R("hero"), s);
		PaintPipeline(g, R("pipe"), s);
		PaintLink(g, R("link"), s);
		PaintMetrics(g, R("metrics"), s);
		PaintSpace(g, R("space"), s);
	}

	private void PaintHero(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Tone tone = _phoneTone == Tone.Muted ? Tone.Info : _phoneTone;
		Draw3.Hero(g, rect, s, _phoneState, tone, _heroTitle, _heroSubtitle);
	}

	private void PaintPipeline(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		float gap = 26f * s;
		int count = 5;
		float nodeW = (rect.Width - gap * (count - 1)) / count;
		float x = rect.X;
		// 节点 0：手机
		Draw3.PipeNode(g, new RectangleF(x, rect.Y, nodeW, rect.Height), s, Icon3.Phone, "手机", _phoneState, _phoneTone, _phoneLive, Phase);
		x += nodeW;
		for (int i = 0; i < 4; i++)
		{
			Draw3.PipeLink(g, new RectangleF(x + 6f * s, rect.Y, gap - 12f * s, rect.Height), s, _streams[i].Tone == Tone.Ok);
			x += gap;
			Icon3 icon = i switch
			{
				0 => Icon3.Monitor,
				1 => Icon3.Speaker,
				2 => Icon3.Gamepad,
				_ => Icon3.Headset,
			};
			Draw3.PipeNode(g, new RectangleF(x, rect.Y, nodeW, rect.Height), s, icon, _streams[i].Name, _streams[i].State, _streams[i].Tone, _streams[i].Live, Phase);
			x += nodeW;
		}
	}

	private void PaintLink(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "链路与设备");
		float top = rect.Y + 46f * s + 10f * s;
		float pad = 18f * s;
		float width = rect.Width - pad * 2f;
		float h1 = 50f * s;
		float h2 = 62f * s;
		Draw3.Row(g, new RectangleF(rect.X, top, rect.Width, h1), s, Icon3.Plug, _adbTone, "ADB 有线链路", _adbDetail, _adbState, _adbTone, _adbLive, Phase);
		Sh.Hair(g, rect.X + pad, top + h1, width, Tok.LineSoft, s);
		Draw3.Row(g, new RectangleF(rect.X, top + h1, rect.Width, 54f * s), s, Icon3.Wifi, Tone.Accent, "WiFi 无线", _wifiDetail, _wifiState, _wifiTone, _wifiTone == Tone.Ok);
		Sh.Hair(g, rect.X + pad, top + h1 + 54f * s, width, Tok.LineSoft, s);
		Draw3.Row(g, new RectangleF(rect.X, top + h1 + 54f * s, rect.Width, h2), s, Icon3.Phone, Tone.Info, "手机", _phoneDetail, null);
	}

	private void PaintMetrics(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "运行数据");
	}

	private void PaintSpace(Graphics g, RectangleF rect, float s)
	{
		if (rect.Width <= 1f)
		{
			return;
		}
		Draw3.Card(g, rect, s);
		Draw3.CardTitle(g, rect, s, "空间拖拽", _playspaceState, _playspaceTone);
		float pad = 18f * s;
		float contentTop = rect.Y + 46f * s + 12f * s;
		Sh.Text(g, "抓握键拖动手机浮窗", Tok.Body(true), Tok.Text1, new RectangleF(rect.X + pad, contentTop, rect.Width * 0.6f, 20f * s), ContentAlignment.MiddleLeft);
		Sh.Text(g, "在 VR 中按住抓握键即可搬动浮窗位置", Tok.Caption(), Tok.Text3, new RectangleF(rect.X + pad, contentTop + 22f * s, rect.Width * 0.62f, 18f * s), ContentAlignment.MiddleLeft);
		float axisTop = contentTop + 44f * s + 8f * s;
		float axisGap = 12f * s;
		float axisW = (rect.Width - pad * 2f - axisGap * 2f) / 3f;
		Draw3.Readout(g, new RectangleF(rect.X + pad, axisTop, axisW, 58f * s), s, "X 轴", _axisX);
		Draw3.Readout(g, new RectangleF(rect.X + pad + axisW + axisGap, axisTop, axisW, 58f * s), s, "Y 轴", _axisY);
		Draw3.Readout(g, new RectangleF(rect.X + pad + (axisW + axisGap) * 2f, axisTop, axisW, 58f * s), s, "Z 轴", _axisZ);
		float stepTop = axisTop + 58f * s + 26f * s;
		Sh.Text(g, "倍率", Tok.Caption(true), Tok.Text3, new RectangleF(rect.X + pad, stepTop - 18f * s, 120f * s, 16f * s), ContentAlignment.MiddleLeft);
		Sh.Text(g, "即时生效，仅本次运行", Tok.Caption(), Tok.Text3, new RectangleF(rect.X + pad + 296f * s, stepTop + 4f * s, Math.Max(20f, rect.Width - pad * 2f - 296f * s), 46f * s), ContentAlignment.MiddleLeft);
	}

	private float _phase;

	protected override bool AdvanceExtra(float dt)
	{
		bool anyLive = _phoneLive;
		foreach (StreamState stream in _streams)
		{
			anyLive |= stream.Live;
		}
		if (!anyLive)
		{
			return false;
		}
		_phase += dt * 0.55f;
		if (_phase > 1f)
		{
			_phase -= 1f;
		}
		Invalidate();
		return true;
	}

	private float Phase => _phase;
}
