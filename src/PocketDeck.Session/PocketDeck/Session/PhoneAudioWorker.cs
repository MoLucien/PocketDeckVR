using System;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Session;

public sealed class PhoneAudioWorker : IAsyncDisposable
{
	private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

	private readonly object _gate = new object();

	private Task? _stopTask;

	private int _stopRequested;

	private int _lifetimeDisposed;

	public Task RunTask { get; }

	public bool IsStopRequested => Volatile.Read(in _stopRequested) != 0;

	public PhoneAudioWorker(Func<CancellationToken, Task> run)
	{
		PhoneAudioWorker phoneAudioWorker = this;
		ArgumentNullException.ThrowIfNull(run, "run");
		RunTask = Task.Run(() => run(phoneAudioWorker._lifetime.Token), CancellationToken.None);
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			await StopAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			if (Interlocked.Exchange(ref _lifetimeDisposed, 1) == 0)
			{
				_lifetime.Dispose();
			}
		}
	}

	private ValueTask StopAsync()
	{
		Task stopTask;
		lock (_gate)
		{
			if (_stopTask == null)
			{
				_stopTask = StopCoreAsync();
			}
			stopTask = _stopTask;
		}
		return new ValueTask(stopTask);
	}

	private async Task StopCoreAsync()
	{
		Interlocked.Exchange(ref _stopRequested, 1);
		await _lifetime.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			await RunTask.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
		{
		}
	}
}
