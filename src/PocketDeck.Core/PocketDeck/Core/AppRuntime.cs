using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Core;

public sealed class AppRuntime : IAsyncDisposable
{
	private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

	private AppRuntimeSnapshot _snapshot = NewSnapshot(AppLifecycleState.Created, "APP_CREATED", "工程运行时已创建");

	private bool _disposed;

	public AppRuntimeSnapshot Snapshot => Volatile.Read(in _snapshot);

	public event EventHandler<AppRuntimeStateChangedEventArgs>? StateChanged;

	public async ValueTask StartAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			ThrowIfDisposed();
			AppLifecycleState state = _snapshot.State;
			if ((uint)(state - 1) > 1u)
			{
				EnsureState(AppLifecycleState.Created, AppLifecycleState.Stopped);
				Transition(AppLifecycleState.Starting, "APP_STARTING", "正在启动工程运行时");
				Transition(AppLifecycleState.Ready, "APP_READY", "工程运行时已就绪");
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (_snapshot.State != AppLifecycleState.Stopped)
			{
				if (_snapshot.State == AppLifecycleState.Created)
				{
					Transition(AppLifecycleState.Stopped, "APP_NOT_STARTED", "工程运行时未启动");
					return;
				}
				EnsureState(AppLifecycleState.Starting, AppLifecycleState.Ready, AppLifecycleState.Degraded);
				Transition(AppLifecycleState.Stopping, "APP_STOPPING", "正在停止工程运行时");
				Transition(AppLifecycleState.Stopped, "APP_STOPPED", "工程运行时已停止");
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			_disposed = true;
			_gate.Dispose();
		}
	}

	private static AppRuntimeSnapshot NewSnapshot(AppLifecycleState state, string reasonCode, string reasonMessage)
	{
		return new AppRuntimeSnapshot(state, StateReason.Normal(reasonCode, reasonMessage), DateTimeOffset.UtcNow);
	}

	private void Transition(AppLifecycleState state, string reasonCode, string reasonMessage)
	{
		AppRuntimeSnapshot snapshot = _snapshot;
		AppRuntimeSnapshot appRuntimeSnapshot = NewSnapshot(state, reasonCode, reasonMessage);
		Volatile.Write(ref _snapshot, appRuntimeSnapshot);
		StateChanged?.Invoke(this, new AppRuntimeStateChangedEventArgs(snapshot, appRuntimeSnapshot));
	}

	private void EnsureState(params AppLifecycleState[] allowed)
	{
		if (!allowed.Contains(_snapshot.State, null))
		{
			throw new InvalidOperationException($"State {_snapshot.State} cannot perform this transition.");
		}
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}
}
