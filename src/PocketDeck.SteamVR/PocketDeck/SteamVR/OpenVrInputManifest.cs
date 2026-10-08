using System;
using System.IO;
using System.Text;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrInputManifest
{
	public const string ApplicationKey = "local.pocketdeck.desktop.v1";

	public const string ActionSetPath = "/actions/main";

	public const string PointerActionSetPath = "/actions/pointer";

	public const string PhoneButtonStateActionSetPath = "/actions/phonebuttonstate";

	public static (string ApplicationManifest, string ActionManifest) ResolveFiles()
	{
		string text = Path.Combine(AppContext.BaseDirectory, "manifest.vrmanifest");
		string text2 = OpenVrBindingStartup.EnsurePrepared();
		if (!File.Exists(text) || !File.Exists(text2))
		{
			throw new FileNotFoundException("SteamVR 应用或动作清单未随程序发布");
		}
		return (ApplicationManifest: text, ActionManifest: text2);
	}

	public static EVRInputError RegisterAndSubmit()
	{
		var (applicationManifest, actionManifestPath) = ResolveFiles();
		RegisterApplicationManifest(applicationManifest);
		return OpenVR.Input.SetActionManifestPath(actionManifestPath);
	}

	public static ulong GetActionSet()
	{
		return GetActionSet("/actions/main", "动作集");
	}

	public static ulong GetPointerActionSet()
	{
		return GetActionSet("/actions/pointer", "手机射线动作集");
	}

	public static ulong GetPhoneButtonStateActionSet()
	{
		return GetActionSet("/actions/phonebuttonstate", "手机输入原始状态动作集");
	}

	private static ulong GetActionSet(string path, string operation)
	{
		ulong pHandle = 0uL;
		Check(OpenVR.Input.GetActionSetHandle(path, ref pHandle), operation);
		return pHandle;
	}

	public static OpenVrBindingResult OpenBindingUi(ulong actionSet)
	{
		EVRInputError eVRInputError = OpenVR.Input.OpenBindingUI("local.pocketdeck.desktop.v1", actionSet, 0uL, bShowOnDesktop: true);
		if (eVRInputError != EVRInputError.None)
		{
			return new OpenVrBindingResult(Succeeded: false, "OPENVR_BINDING_UI_FAILED", $"SteamVR 手柄绑定页面打开失败：{eVRInputError}");
		}
		return new OpenVrBindingResult(Succeeded: true, "OPENVR_BINDING_UI_OPENED", "SteamVR 绑定页面已打开；拖拽、触控、返回、桌面、最近任务、控制栏和截屏现在位于同一动作集");
	}

	private static void RegisterApplicationManifest(string applicationManifest)
	{
		EVRApplicationError peError = EVRApplicationError.None;
		StringBuilder stringBuilder = new StringBuilder(2048);
		EVRApplicationError eVRApplicationError;
		checked
		{
			OpenVR.Applications.GetApplicationPropertyString("local.pocketdeck.desktop.v1", EVRApplicationProperty.BinaryPath_String, stringBuilder, (uint)stringBuilder.Capacity, ref peError);
			if (peError == EVRApplicationError.None && stringBuilder.Length > 0)
			{
				string text = stringBuilder.ToString();
				if (!Path.IsPathRooted(text))
				{
					EVRApplicationError peError2 = EVRApplicationError.None;
					StringBuilder stringBuilder2 = new StringBuilder(2048);
					OpenVR.Applications.GetApplicationPropertyString("local.pocketdeck.desktop.v1", EVRApplicationProperty.WorkingDirectory_String, stringBuilder2, (uint)stringBuilder2.Capacity, ref peError2);
					if (peError2 == EVRApplicationError.None && stringBuilder2.Length > 0)
					{
						text = Path.Combine(stringBuilder2.ToString(), text);
					}
				}
				string directoryName = Path.GetDirectoryName(text);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					string[] array = new string[2] { "manifest.vrmanifest", "app/manifest.vrmanifest" };
					foreach (string path in array)
					{
						string fullPath = Path.GetFullPath(Path.Combine(directoryName, path));
						if (!string.Equals(fullPath, applicationManifest, StringComparison.OrdinalIgnoreCase))
						{
							OpenVR.Applications.RemoveApplicationManifest(fullPath);
						}
					}
				}
			}
			eVRApplicationError = OpenVR.Applications.AddApplicationManifest(applicationManifest, bTemporary: false);
		}
		if ((uint)(eVRApplicationError - 107) <= 1u)
		{
			throw new InvalidOperationException($"应用清单注册失败：{eVRApplicationError}");
		}
		OpenVR.Applications.SetApplicationAutoLaunch("local.pocketdeck.desktop.v1", bAutoLaunch: true);
	}

	private static void Check(EVRInputError error, string operation)
	{
		if (error != EVRInputError.None)
		{
			throw new InvalidOperationException($"{operation}失败：{error}");
		}
	}
}
