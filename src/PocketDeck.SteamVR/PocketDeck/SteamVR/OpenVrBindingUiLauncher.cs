using System;
using System.IO;
using Valve.VR;

namespace PocketDeck.SteamVR;

public static class OpenVrBindingUiLauncher
{
	private static readonly object _gate = new object();

	public static OpenVrBindingResult Open()
	{
		lock (_gate)
		{
			try
			{
				EVRInitError initializationError = EVRInitError.None;
				OpenVrRuntimeHost.GetOrStart(ref initializationError);
				if (initializationError != EVRInitError.None)
				{
					return new OpenVrBindingResult(Succeeded: false, "OPENVR_INITIALIZATION_FAILED", $"SteamVR 连接失败：{initializationError}。请确认 SteamVR 正在运行");
				}
				EVRInputError eVRInputError = OpenVrRuntimeHost.EnsureActionManifestSubmitted();
				if (eVRInputError != EVRInputError.None && eVRInputError != EVRInputError.IPCError)
				{
					return new OpenVrBindingResult(Succeeded: false, "OPENVR_BINDING_UI_FAILED", $"SteamVR 动作清单加载失败：{eVRInputError}");
				}
				ulong actionSet = OpenVrInputManifest.GetActionSet();
				return OpenVrInputManifest.OpenBindingUi(actionSet);
			}
			catch (Exception ex) when ((ex is FileNotFoundException || ex is InvalidOperationException || ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException) ? true : false)
			{
				return new OpenVrBindingResult(Succeeded: false, "OPENVR_BINDING_UI_FAILED", "SteamVR 手柄绑定页面打开失败，请确认 SteamVR 正在运行");
			}
		}
	}

	public static void Shutdown()
	{
		OpenVrRuntimeHost.Shutdown();
	}
}
