using System;
using System.Linq;
using System.Threading;
using PocketDeck.SteamVR;

namespace PocketDeck.SteamVR.BindingTool;

/// <summary>
/// 本地手柄绑定准备工具（独立进程运行，避免占用主程序的 SteamVR 会话）。
/// 用法：<c>PocketDeck.SteamVR.BindingTool.exe activate-local-binding</c>
/// 退出码：0 = 成功，1 = 失败（主程序据此判定并提示重试）。
/// </summary>
internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		bool activate = args.Any((string value) => string.Equals(value, "activate-local-binding", StringComparison.OrdinalIgnoreCase));
		try
		{
			// 只做文件准备：绝不能调用 PrepareLocalBinding()/Refresh()，那会再启动本工具造成无限递归。
			OpenVrBindingResult result = OpenVrBindingRecovery.PrepareLocalBindingFiles();
			if (!result.Succeeded)
			{
				Console.Error.WriteLine("BINDING_TOOL_FAILED " + result.ReasonCode + " " + result.Message);
			}
			return result.Succeeded ? 0 : 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("BINDING_TOOL_EXCEPTION " + ex.GetType().Name + " " + ex.Message);
			return 1;
		}
	}
}
