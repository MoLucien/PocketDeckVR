using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PocketDeck.Android;
using PocketDeck.Core;
using PocketDeck.Session;
using PocketDeck.Settings;
using PocketDeck.SteamVR;

namespace PocketDeck.App;

internal static class Program
{
	/// <summary>
	/// 程序集解析兜底：厂商随包发布的第三方程序集（如 SharpGen.Runtime.COM）没有出现在
	/// 本次重建的 deps.json 里，默认探测不会找它们 —— 缺了会在媒体链路上抛
	/// TypeInitializationException/FileNotFoundException。这里统一回退到程序目录加载。
	/// </summary>
	private static void InstallAssemblyFallback()
	{
		AppDomain.CurrentDomain.AssemblyResolve += (object? sender, ResolveEventArgs args) =>
		{
			try
			{
				string? simpleName = new AssemblyName(args.Name).Name;
				if (string.IsNullOrEmpty(simpleName))
				{
					return null;
				}
				string candidate = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");
				return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
			}
			catch (Exception)
			{
				return null;
			}
		};
	}

	[STAThread]
	private static int Main(string[] args)
	{
		InstallAssemblyFallback();
		if (args.Length == 1 && string.Equals(args[0], "--smoke-test", StringComparison.Ordinal))
		{
			return AppSmokeTest.Run();
		}
		if (args.Contains("--ui3-snapshot", StringComparer.Ordinal))
		{
			ApplicationConfiguration.Initialize();
			return Ui3.Snapshot3.Run(ReadArgument(args, "--ui3-snapshot-output"));
		}
		ApplicationConfiguration.Initialize();
		AppRuntime appRuntime = new AppRuntime();
		IAndroidConnectionLogSink connectionLog = AndroidConnectionFactory.CreateDefaultLog();
		IAndroidConnectionService androidConnectionService = AndroidConnectionFactory.CreateDefault(connectionLog);
		IAndroidVideoSessionFactory videoSessions = AndroidConnectionFactory.CreateDefaultVideoSessions(androidConnectionService, connectionLog);
		IAndroidControlSessionFactory sessions = AndroidConnectionFactory.CreateDefaultControlSessions(androidConnectionService, connectionLog);
		IAndroidAudioSessionFactory audioSessions = AndroidConnectionFactory.CreateDefaultAudioSessions(androidConnectionService, connectionLog);
		IAndroidMediaPlaybackControl androidMediaPlayback = AndroidConnectionFactory.CreateDefaultMediaPlaybackControl(androidConnectionService, connectionLog);
		IPhoneVideoDecodeProbeService videoProbe = new PhoneVideoDecodeProbeService(videoSessions, connectionLog);
		PhoneControlService phoneControlService = new PhoneControlService(sessions, connectionLog);
		PhoneAudioService phoneAudioService = new PhoneAudioService(audioSessions, connectionLog);
		PhoneOverlayService phoneOverlayService = new PhoneOverlayService(videoSessions, phoneControlService, AndroidConnectionFactory.CreateDefaultKeyguardStateService(androidConnectionService), connectionLog);
		PhoneMediaSessionCoordinator phoneMediaSessionCoordinator = new PhoneMediaSessionCoordinator(phoneOverlayService, phoneAudioService, phoneControlService, androidMediaPlayback, androidConnectionService);
		JsonAppSettingsService jsonAppSettingsService = JsonAppSettingsService.CreateDefault();
		OpenVrPlayspaceDragService openVrPlayspaceDragService = null;
		EventHandler<OpenVrPlayspaceDragDiagnosticEventArgs> value = (object? _, OpenVrPlayspaceDragDiagnosticEventArgs eventArgs) =>
		{
			OpenVrPlayspaceDragDiagnostic diagnostic = eventArgs.Diagnostic;
			connectionLog.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "steamvr_playspace_drag", diagnostic.ReasonCode, diagnostic.Message));
		};
		try
		{
			jsonAppSettingsService.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter()
				.GetResult();
			AppSettings value2 = jsonAppSettingsService.Snapshot.Value;
			OpenVrBindingResult openVrBindingResult = OpenVrBindingRecovery.PrepareLocalBinding();
			connectionLog.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "steamvr_binding_startup", openVrBindingResult.ReasonCode, openVrBindingResult.Message));
			openVrPlayspaceDragService = new OpenVrPlayspaceDragService(MapControllerHand(value2.ControllerHand), 1f);
			openVrPlayspaceDragService.DiagnosticRecorded += value;
			openVrPlayspaceDragService.Start();
			appRuntime.StartAsync(CancellationToken.None).AsTask().GetAwaiter()
				.GetResult();
			androidConnectionService.StartAsync(CancellationToken.None).AsTask().GetAwaiter()
				.GetResult();
			using MainForm mainForm = new MainForm(appRuntime, androidConnectionService, videoProbe, phoneOverlayService, phoneAudioService, phoneControlService, phoneMediaSessionCoordinator, openVrPlayspaceDragService, jsonAppSettingsService, connectionLog.CurrentLogPath);
			Application.Run(mainForm);
		}
		finally
		{
			jsonAppSettingsService.Dispose();
			phoneMediaSessionCoordinator.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			phoneAudioService.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			if (openVrPlayspaceDragService != null)
			{
				openVrPlayspaceDragService.DiagnosticRecorded -= value;
				openVrPlayspaceDragService.Dispose();
			}
			phoneOverlayService.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			phoneControlService.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			androidConnectionService.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			connectionLog.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
			appRuntime.StopAsync(CancellationToken.None).AsTask().GetAwaiter()
				.GetResult();
			appRuntime.DisposeAsync().AsTask().GetAwaiter()
				.GetResult();
		}
		return 0;
	}

	private static OpenVrControllerHand MapControllerHand(ControllerHandPreference hand)
	{
		if (hand != ControllerHandPreference.Left)
		{
			return OpenVrControllerHand.Right;
		}
		return OpenVrControllerHand.Left;
	}

	private static string? ReadArgument(string[] args, string name)
	{
		checked
		{
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (string.Equals(args[i], name, StringComparison.Ordinal))
				{
					return args[i + 1];
				}
			}
			return null;
		}
	}
}
