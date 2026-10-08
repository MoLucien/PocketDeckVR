using System;
using System.Drawing;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>
/// 状态适配器：把既有业务逻辑「写文本 + 写颜色」的调用，转成 v3 视图的属性写入。
/// 业务侧无需知道新视图长什么样。
/// </summary>
internal sealed class Sink
{
	private readonly Action<string, Color> _apply;

	private readonly Action<string, Color>? _onText;

	private string _text = string.Empty;

	private Color _color = Tok.Text2;

	/// <param name="apply">任何变化都回调（状态栏用）。</param>
	/// <param name="onText">仅当文本真正变化时回调（弹 Toast 用；只改颜色时不会把旧文案当新结果）。</param>
	public Sink(Action<string, Color> apply, Action<string, Color>? onText = null)
	{
		_apply = apply ?? throw new ArgumentNullException("apply");
		_onText = onText;
	}

	public string Text
	{
		get => _text;
		set
		{
			string next = value ?? string.Empty;
			bool changed = !string.Equals(next, _text, StringComparison.Ordinal);
			_text = next;
			_apply(_text, _color);
			if (changed)
			{
				_onText?.Invoke(_text, _color);
			}
		}
	}

	public Color ForeColor
	{
		get => _color;
		set
		{
			_color = value;
			_apply(_text, _color);
		}
	}

	/// <summary>保持与 Control 相同的调用手感。</summary>
	public bool Enabled { get; set; } = true;

	public bool Visible { get; set; } = true;

	public void Invalidate()
	{
		_apply(_text, _color);
	}
}
