using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPlacementStore : IDisposable
{
	private readonly string _path;

	private readonly string _temporaryPath;

	private readonly Channel<OpenVrSavedPlacement> _pending;

	private readonly CancellationTokenSource _stop = new CancellationTokenSource();

	private readonly Task _worker;

	private long _dropped;

	private long _written;

	private long _failures;

	private int _disposed;

	public long Dropped => Interlocked.Read(in _dropped);

	public long Written => Interlocked.Read(in _written);

	public long Failures => Interlocked.Read(in _failures);

	public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketDeck", "phone-overlay-placement.json");

	public OpenVrPlacementStore(string path)
	{
		_path = path;
		_temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		_pending = Channel.CreateBounded(new BoundedChannelOptions(1)
		{
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = true
		}, (OpenVrSavedPlacement _) =>
		{
			Interlocked.Increment(ref _dropped);
		});
		_worker = WritePendingAsync(_stop.Token);
	}

	public static async Task<OpenVrSavedPlacement?> LoadAsync(string path, CancellationToken cancellationToken)
	{
		try
		{
			if (!File.Exists(path) || new FileInfo(path).Length > 4096)
			{
				return null;
			}
			OpenVrSavedPlacement openVrSavedPlacement = JsonSerializer.Deserialize<OpenVrSavedPlacement>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			if ((object)openVrSavedPlacement != null && openVrSavedPlacement.SchemaVersion == 1)
			{
				openVrSavedPlacement = openVrSavedPlacement with
				{
					SchemaVersion = 2
				};
			}
			return ((object)openVrSavedPlacement != null && openVrSavedPlacement.IsValid) ? openVrSavedPlacement : null;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is OperationCanceledException) ? true : false)
		{
			return null;
		}
	}

	public void Queue(OpenVrSavedPlacement value)
	{
		if (Volatile.Read(in _disposed) == 0 && value.IsValid)
		{
			_pending.Writer.TryWrite(value);
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}
		_pending.Writer.TryComplete();
		if (!_worker.Wait(TimeSpan.FromSeconds(2L)))
		{
			_stop.Cancel();
			_worker.ContinueWith((Task _, object? state) =>
			{
				((CancellationTokenSource)state).Dispose();
			}, _stop, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}
		else
		{
			_stop.Dispose();
		}
	}

	private async Task WritePendingAsync(CancellationToken cancellationToken)
	{
		_ = 2;
		try
		{
			await foreach (OpenVrSavedPlacement item in _pending.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(_path));
					await File.WriteAllTextAsync(_temporaryPath, JsonSerializer.Serialize(item), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					cancellationToken.ThrowIfCancellationRequested();
					File.Move(_temporaryPath, _path, overwrite: true);
					Interlocked.Increment(ref _written);
				}
				catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
				{
					Interlocked.Increment(ref _failures);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		finally
		{
			try
			{
				File.Delete(_temporaryPath);
			}
			catch (Exception ex3) when ((ex3 is IOException || ex3 is UnauthorizedAccessException) ? true : false)
			{
				Interlocked.Increment(ref _failures);
			}
		}
	}
}
