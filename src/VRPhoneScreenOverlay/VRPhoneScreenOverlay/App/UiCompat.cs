using System.Drawing;

namespace PocketDeck.App;

/// <summary>
/// 语义色兼容层：业务逻辑（MainForm 的状态机/操作提示）用「颜色」表达结果语义，
/// 这里把语义映射到 v3 调色板。旧的 UiPalette 及其全部颜色定义已删除，
/// 本文件只有映射，不含任何视觉设计。
/// </summary>
internal static class UiPalette
{
	public static Color Success => Ui3.Tok.Ok;

	public static Color Danger => Ui3.Tok.Bad;

	public static Color Warning => Ui3.Tok.Warn;

	public static Color Info => Ui3.Tok.Info;

	public static Color Accent => Ui3.Tok.Accent;

	public static Color TextPrimary => Ui3.Tok.Text1;

	public static Color TextSecondary => Ui3.Tok.Text2;

	public static Color TextMuted => Ui3.Tok.Text3;
}
