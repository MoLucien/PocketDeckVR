using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Settings;

public sealed class JsonAppSettingsService : IAppSettingsService, IDisposable
{
	private sealed record SettingsDocument(int SchemaVersion, string? ControllerHand, int VideoResolutionPercent, int VideoBitrateMbps, int VideoMaximumFramesPerSecond, string? UpdateChannel, bool KeepAwakeWhileGrabbed = false, bool VrUnlockKeypadEnabled = false)
	{
		public AppSettings ToSettings(out bool repaired)
		{
			bool flag = Enum.TryParse<ControllerHandPreference>(ControllerHand, ignoreCase: true, out var result) && Enum.IsDefined(result);
			bool flag2 = Enum.TryParse<UpdateChannel>(UpdateChannel, ignoreCase: true, out var result2) && Enum.IsDefined(result2);
			repaired = !flag || !flag2;
			return new AppSettings(SchemaVersion, flag ? result : AppSettings.Default.ControllerHand, VideoResolutionPercent, VideoBitrateMbps, VideoMaximumFramesPerSecond, flag2 ? result2 : AppSettings.Default.UpdateChannel, KeepAwakeWhileGrabbed, VrUnlockKeypadEnabled);
		}

		public static SettingsDocument FromSettings(AppSettings settings)
		{
			return new SettingsDocument(settings.SchemaVersion, settings.ControllerHand.ToString(), settings.VideoResolutionPercent, settings.VideoBitrateMbps, settings.VideoMaximumFramesPerSecond, settings.UpdateChannel.ToString(), settings.KeepAwakeWhileGrabbed, settings.VrUnlockKeypadEnabled);
		}
	}

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

	private readonly string _settingsPath;

	private readonly string _legacyInputPath;

	private readonly object _snapshotGate = new object();

	private AppSettingsSnapshot _snapshot = new AppSettingsSnapshot(AppSettings.Default, "SETTINGS_NOT_INITIALIZED", "设置服务尚未初始化", IsPersisted: false);

	private bool _disposed;

	public AppSettingsSnapshot Snapshot
	{
		get
		{
			lock (_snapshotGate)
			{
				return _snapshot;
			}
		}
	}

	public event EventHandler<AppSettingsChangedEventArgs>? Changed;

	public JsonAppSettingsService(string settingsPath, string legacyInputPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath, "settingsPath");
		ArgumentException.ThrowIfNullOrWhiteSpace(legacyInputPath, "legacyInputPath");
		_settingsPath = Path.GetFullPath(settingsPath);
		_legacyInputPath = Path.GetFullPath(legacyInputPath);
	}

	public static JsonAppSettingsService CreateDefault()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string path = Path.Combine(folderPath, "PocketDeck");
		return new JsonAppSettingsService(Path.Combine(path, "settings.json"), Path.Combine(path, "input.json"));
	}

	public async ValueTask InitializeAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			AppSettings settings;
			bool flag;
			string reasonCode;
			string message;
			if (!File.Exists(_settingsPath))
			{
				ControllerHandPreference controllerHandPreference = await ReadLegacyHandAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				settings = AppSettings.Default with
				{
					ControllerHand = controllerHandPreference
				};
				flag = true;
				reasonCode = ((controllerHandPreference == AppSettings.Default.ControllerHand && !File.Exists(_legacyInputPath)) ? "SETTINGS_DEFAULT_CREATED" : "SETTINGS_INPUT_MIGRATED");
				message = ((reasonCode == "SETTINGS_INPUT_MIGRATED") ? "已迁移原有左右手布局并建立统一设置" : "已建立默认统一设置");
			}
			else
			{
				try
				{
					SettingsDocument settingsDocument = JsonSerializer.Deserialize<SettingsDocument>(await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), _jsonOptions) ?? throw new JsonException("Settings document was empty.");
					AppSettings appSettings = settingsDocument.ToSettings(out var repaired);
					settings = AppSettingsPolicy.Normalize(appSettings);
					flag = repaired || settings != appSettings;
					reasonCode = (flag ? "SETTINGS_VALUES_REPAIRED" : "SETTINGS_LOADED");
					message = (flag ? "部分设置无效，已恢复为安全值" : "统一设置已加载");
				}
				catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException || ex is FormatException) ? true : false)
				{
					settings = AppSettings.Default;
					flag = true;
					reasonCode = "SETTINGS_CORRUPT_DEFAULTED";
					message = "设置文件损坏，已使用安全默认值";
				}
			}
			bool persisted = true;
			if (flag)
			{
				try
				{
					await WriteAtomicAsync(settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex2) when ((ex2 is IOException || ex2 is UnauthorizedAccessException) ? true : false)
				{
					persisted = false;
					reasonCode = "SETTINGS_SAVE_UNAVAILABLE";
					message = "设置目录不可写，本次运行将使用安全设置";
				}
			}
			Publish(new AppSettingsSnapshot(settings, reasonCode, message, persisted));
		}
		finally
		{
			_gate.Release();
		}
	}

	public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(settings, "settings");
		AppSettings normalized = AppSettingsPolicy.Normalize(settings);
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			await WriteAtomicAsync(normalized, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new AppSettingsSnapshot(normalized, "SETTINGS_SAVED", "设置已安全保存", IsPersisted: true));
		}
		finally
		{
			_gate.Release();
		}
	}

	private async ValueTask<ControllerHandPreference> ReadLegacyHandAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (!File.Exists(_legacyInputPath))
			{
				return AppSettings.Default.ControllerHand;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await File.ReadAllTextAsync(_legacyInputPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			if (jsonDocument.RootElement.TryGetProperty("controllerHand", out var value) && Enum.TryParse<ControllerHandPreference>(value.GetString(), ignoreCase: true, out var result) && Enum.IsDefined(result))
			{
				return result;
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException) ? true : false)
		{
		}
		return AppSettings.Default.ControllerHand;
	}

	private async ValueTask WriteAtomicAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		string directoryName = Path.GetDirectoryName(_settingsPath);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			throw new IOException("Settings path has no parent directory.");
		}
		Directory.CreateDirectory(directoryName);
		string temporaryPath = Path.Combine(directoryName, $".{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");
		try
		{
			byte[] array = JsonSerializer.SerializeToUtf8Bytes(SettingsDocument.FromSettings(settings), _jsonOptions);
			await using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
			{
				await stream.WriteAsync(array, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			cancellationToken.ThrowIfCancellationRequested();
			if (File.Exists(_settingsPath))
			{
				File.Replace(temporaryPath, _settingsPath, null, ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(temporaryPath, _settingsPath);
			}
		}
		finally
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_gate.Dispose();
		}
	}

	private void Publish(AppSettingsSnapshot snapshot)
	{
		lock (_snapshotGate)
		{
			_snapshot = snapshot;
		}
		Changed?.Invoke(this, new AppSettingsChangedEventArgs(snapshot));
	}
}
