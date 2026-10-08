using System;
using System.Threading;
using Valve.VR;

namespace PocketDeck.SteamVR;

public static class OpenVrBindingSettingsCleaner
{
	public static OpenVrBindingResult ActivateLocalBinding()
	{
		bool flag = false;
		try
		{
			EVRInitError peError = EVRInitError.None;
			OpenVR.Init(ref peError, EVRApplicationType.VRApplication_Utility);
			if (peError != EVRInitError.None)
			{
				return new OpenVrBindingResult(Succeeded: false, "OPENVR_INITIALIZATION_FAILED", "无法连接 SteamVR，尚未切换到程序本地绑定");
			}
			flag = true;
			using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2L)))
			{
				OpenVrInputCaptureProfiles.EnrichAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
			}
			EVRSettingsError peError2 = EVRSettingsError.None;
			OpenVR.Settings.RemoveSection("local.pocketdeck.desktop.v1", ref peError2);
			return (peError2 == EVRSettingsError.None) ? new OpenVrBindingResult(Succeeded: true, "OPENVR_LOCAL_BINDING_ACTIVATED", "已清除 SteamVR 云端或 Workshop 绑定选择并启用程序本地绑定") : new OpenVrBindingResult(Succeeded: false, "OPENVR_LOCAL_BINDING_ACTIVATION_FAILED", "无法清除 SteamVR 托管绑定选择，尚未切换到程序本地绑定");
		}
		catch (Exception ex) when ((ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException) ? true : false)
		{
			return new OpenVrBindingResult(Succeeded: false, "OPENVR_LOCAL_BINDING_ACTIVATION_FAILED", "SteamVR 本地绑定准备工具无法启动 OpenVR");
		}
		finally
		{
			if (flag)
			{
				OpenVR.Shutdown();
			}
		}
	}
}
