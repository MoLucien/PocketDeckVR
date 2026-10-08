using System;
using System.IO;

namespace PocketDeck.SteamVR;

public static class OpenVrBindingRecovery
{
	private static readonly object _gate = new object();

	public static OpenVrBindingPreparationSnapshot DiagnosticSnapshot => OpenVrBindingStartup.GetDiagnosticSnapshot();

	public static OpenVrBindingResult PrepareChangedLocalBinding()
	{
		if (!OpenVrBindingStartup.HasSavedBindingChanged())
		{
			return new OpenVrBindingResult(Succeeded: true, "OPENVR_LOCAL_BINDING_UNCHANGED", "本地手柄绑定没有变化");
		}
		return PrepareLocalBinding();
	}

	public static bool HasSavedBindingChanged()
	{
		return OpenVrBindingStartup.HasSavedBindingChanged();
	}

	/// <summary>
	/// 只准备本地绑定文件（把随包的动作清单与各控制器绑定写进运行目录），**不启动绑定工具、不触碰 SteamVR 会话**。
	/// 独立工具进程调用它，避免 PrepareCore → 工具 → PrepareCore 的自我递归。
	/// </summary>
	public static OpenVrBindingResult PrepareLocalBindingFiles()
	{
		lock (_gate)
		{
			try
			{
				OpenVrPreparedInputManifest prepared = OpenVrBindingProfileStore.CreateDefault().Prepare();
				return new OpenVrBindingResult(Succeeded: true, "OPENVR_LOCAL_BINDING_FILES_READY", "本地绑定文件已准备：" + prepared.ActionManifestPath);
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is InvalidOperationException) ? true : false)
			{
				return new OpenVrBindingResult(Succeeded: false, "OPENVR_LOCAL_BINDING_FILES_FAILED", "本地绑定文件准备失败：" + ex.Message);
			}
		}
	}

	public static OpenVrBindingResult PrepareLocalBinding()
	{
		lock (_gate)
		{
			try
			{
				OpenVrRuntimeHost.Shutdown();
				return OpenVrBindingStartup.Refresh();
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is InvalidOperationException) ? true : false)
			{
				return new OpenVrBindingResult(Succeeded: false, "OPENVR_BINDING_DEFAULT_RESTORE_FAILED", "默认绑定恢复失败，请重启 SteamVR 后重试");
			}
		}
	}
}
