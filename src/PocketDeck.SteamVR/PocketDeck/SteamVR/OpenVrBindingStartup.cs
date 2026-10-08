using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PocketDeck.SteamVR;

internal static class OpenVrBindingStartup
{
	private const int _bindingToolTimeoutMilliseconds = 10000;

	private static readonly object _gate = new object();

	private static bool _prepared;

	private static string? _actionManifestPath;

	private static string? _savedBindingRevision;

	private static OpenVrControllerHand? _preparedHand;

	private static OpenVrBindingPreparationSnapshot _diagnosticSnapshot = OpenVrBindingPreparationSnapshot.NotPrepared;

	/// <summary>绑定工具最近一次的告警（非致命，供诊断查看）。</summary>
	internal static string? _bindingToolWarning;

	public static OpenVrBindingPreparationSnapshot GetDiagnosticSnapshot()
	{
		lock (_gate)
		{
			return _diagnosticSnapshot;
		}
	}

	public static string EnsurePrepared()
	{
		lock (_gate)
		{
			OpenVrControllerHand openVrControllerHand = OpenVrControllerPreferences.Load();
			if (_prepared && _actionManifestPath != null && _preparedHand == openVrControllerHand)
			{
				return _actionManifestPath;
			}
			return PrepareCore();
		}
	}

	public static OpenVrBindingResult Refresh()
	{
		lock (_gate)
		{
			try
			{
				PrepareCore();
				return new OpenVrBindingResult(Succeeded: true, "OPENVR_LOCAL_BINDING_PREPARED", "已准备并强制激活程序本地绑定");
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is InvalidOperationException || ex is Win32Exception) ? true : false)
			{
				_prepared = false;
				_actionManifestPath = null;
				_savedBindingRevision = null;
				_preparedHand = null;
				_diagnosticSnapshot = OpenVrBindingPreparationSnapshot.NotPrepared;
				return new OpenVrBindingResult(Succeeded: false, "OPENVR_LOCAL_BINDING_PREPARE_FAILED", "无法准备本地手柄绑定（绑定工具未能完成，可在设置页点「手柄绑定」重试）");
			}
		}
	}

	public static bool HasSavedBindingChanged()
	{
		lock (_gate)
		{
			if (!_prepared || _savedBindingRevision == null)
			{
				return false;
			}
			try
			{
				string savedBindingRevision = OpenVrBindingProfileStore.CreateDefault().GetSavedBindingRevision();
				return !string.Equals(savedBindingRevision, _savedBindingRevision, StringComparison.Ordinal);
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is InvalidOperationException) ? true : false)
			{
				return false;
			}
		}
	}

	private static string PrepareCore()
	{
		OpenVrBindingProfileStore openVrBindingProfileStore = OpenVrBindingProfileStore.CreateDefault();
		string savedBindingRevision = openVrBindingProfileStore.GetSavedBindingRevision();
		OpenVrPreparedInputManifest openVrPreparedInputManifest = openVrBindingProfileStore.Prepare();
		string savedBindingRevision2 = openVrBindingProfileStore.GetSavedBindingRevision();
		if (!string.Equals(savedBindingRevision, savedBindingRevision2, StringComparison.Ordinal))
		{
			openVrPreparedInputManifest = openVrBindingProfileStore.Prepare();
			savedBindingRevision2 = openVrBindingProfileStore.GetSavedBindingRevision();
		}
		// 绑定工具是"提前激活"的附加步骤：文件已在上一步准备好，主程序启动浮窗时还会自行把清单提交给 SteamVR。
		// 因此工具失败（缺文件/超时/非 0 退出）不应连带让整个绑定准备失败。
		try
		{
			RunBindingTool();
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is InvalidOperationException || ex is Win32Exception) ? true : false)
		{
			_bindingToolWarning = ex.Message;
		}
		_actionManifestPath = openVrPreparedInputManifest.ActionManifestPath;
		_savedBindingRevision = savedBindingRevision2;
		_preparedHand = OpenVrControllerPreferences.Load();
		_diagnosticSnapshot = new OpenVrBindingPreparationSnapshot(Prepared: true, savedBindingRevision2, _preparedHand.Value, openVrPreparedInputManifest.Bindings.Select((OpenVrPreparedBindingProfile profile) => new OpenVrBindingProfileDiagnostic(profile.ControllerType, profile.Source.ToString())).ToArray());
		_prepared = true;
		return openVrPreparedInputManifest.ActionManifestPath;
	}

	private static void RunBindingTool()
	{
		string text = Path.Combine(AppContext.BaseDirectory, "PocketDeck.SteamVR.BindingTool.exe");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("SteamVR 本地绑定准备工具未随程序发布", text);
		}
		using Process process = Process.Start(new ProcessStartInfo
		{
			FileName = text,
			Arguments = "activate-local-binding",
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden
		}) ?? throw new InvalidOperationException("无法启动 SteamVR 本地绑定准备工具");
		if (!process.WaitForExit(10000))
		{
			process.Kill(entireProcessTree: true);
			process.WaitForExit();
			throw new InvalidOperationException("SteamVR 本地绑定准备工具运行超时");
		}
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException("SteamVR 本地绑定准备工具执行失败");
		}
	}
}
