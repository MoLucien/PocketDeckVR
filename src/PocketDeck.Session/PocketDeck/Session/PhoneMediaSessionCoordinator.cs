using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;

namespace PocketDeck.Session;

public sealed class PhoneMediaSessionCoordinator : IPhoneMediaSessionCoordinator, IAsyncDisposable
{
	private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);

	private readonly object _stateGate = new object();

	private readonly object _reconnectGate = new object();

	private readonly CancellationTokenSource _disposeLifetime = new CancellationTokenSource();

	private readonly IPhoneOverlayService _phoneOverlay;

	private readonly IPhoneAudioService _phoneAudio;

	private readonly IPhoneControlService _phoneControl;

	private readonly IAndroidMediaPlaybackControl _androidMediaPlayback;

	private readonly IAndroidConnectionService? _phoneConnection;

	private AndroidConnectionSnapshot? _lastConnectionSnapshot;

	private PhoneMediaSessionSnapshot _snapshot = new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Stopped, "PHONE_MEDIA_STOPPED", "手机媒体会话未运行");

	private AndroidMediaPauseLease? _pendingPhonePlaybackPause;

	private string? _activeDeviceKey;

	private string? _desiredDeviceKey;

	private AndroidVideoOptions? _desiredVideoOptions;

	private CancellationTokenSource? _reconnectLifetime;

	private Task _reconnectTask = Task.CompletedTask;

	private bool _desiredRunning;

	private bool _sessionActive;

	private bool _disposed;

	public PhoneMediaSessionSnapshot Snapshot
	{
		get
		{
			lock (_stateGate)
			{
				return _snapshot;
			}
		}
	}

	public event EventHandler<PhoneMediaSessionChangedEventArgs>? StateChanged;

	public PhoneMediaSessionCoordinator(IPhoneOverlayService phoneOverlay, IPhoneAudioService phoneAudio, IPhoneControlService phoneControl, IAndroidMediaPlaybackControl androidMediaPlayback, IAndroidConnectionService? phoneConnection = null)
	{
		_phoneOverlay = phoneOverlay ?? throw new ArgumentNullException("phoneOverlay");
		_phoneAudio = phoneAudio ?? throw new ArgumentNullException("phoneAudio");
		_phoneControl = phoneControl ?? throw new ArgumentNullException("phoneControl");
		_androidMediaPlayback = androidMediaPlayback ?? throw new ArgumentNullException("androidMediaPlayback");
		_phoneConnection = phoneConnection;
		if (_phoneConnection != null)
		{
			_lastConnectionSnapshot = _phoneConnection.Snapshot;
			_phoneConnection.StateChanged += OnPhoneConnectionStateChanged;
		}
	}

	public async ValueTask StartAsync(string? deviceKey, AndroidVideoOptions videoOptions, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(videoOptions, "videoOptions");
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			SetDesiredRunning(deviceKey, videoOptions);
			await PrepareDeviceAsync(deviceKey).ConfigureAwait(continueOnCapturedContext: false);
			await StartOwnedAsync(deviceKey, videoOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await ResumePendingPhoneMediaAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			_activeDeviceKey = deviceKey;
			_sessionActive = true;
			PublishRunning();
		}
		catch (Exception)
		{
			try
			{
				await StopOwnedChannelsAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				if (cancellationToken.IsCancellationRequested)
				{
					ClearDesiredRunning();
				}
				Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Faulted, "PHONE_MEDIA_START_FAILED", "手机媒体会话启动失败"));
			}
			throw;
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async ValueTask RestartScreenAsync(string? deviceKey, AndroidVideoOptions videoOptions, Func<CancellationToken, ValueTask>? whilePaused, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(videoOptions, "videoOptions");
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		bool restartedScreen = false;
		bool changingDevice = false;
		try
		{
			SetDesiredRunning(deviceKey, videoOptions);
			changingDevice = _activeDeviceKey != null && !string.Equals(_activeDeviceKey, deviceKey, StringComparison.Ordinal);
			await PrepareDeviceAsync(deviceKey).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.PausingForScreenRestart, "PHONE_MEDIA_PAUSING_FOR_SCREEN_RESTART", "正在暂停手机媒体并准备重启手机屏幕"));
			AndroidMediaPauseLease mediaPause = await EnsurePhonePlaybackPausedAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await _phoneOverlay.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await _phoneControl.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.ScreenRestartPaused, "PHONE_MEDIA_SCREEN_RESTART_PAUSED", "手机媒体已暂停，正在重启手机屏幕"));
			if (whilePaused != null)
			{
				await whilePaused(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.ResumingAfterScreenRestart, "PHONE_MEDIA_RESUMING_AFTER_SCREEN_RESTART", "手机屏幕已重启，正在恢复手机媒体"));
			await _phoneOverlay.StartAsync(deviceKey, videoOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			restartedScreen = true;
			if (changingDevice)
			{
				await _phoneAudio.StartAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await _phoneControl.StartAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await _androidMediaPlayback.ResumeAfterVrReadyAsync(mediaPause, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			_pendingPhonePlaybackPause = null;
			_activeDeviceKey = deviceKey;
			_sessionActive = true;
			PublishRunning();
		}
		catch (Exception)
		{
			try
			{
				if (changingDevice)
				{
					await StopOwnedChannelsAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				}
				else if (restartedScreen)
				{
					try
					{
						await _phoneOverlay.StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					}
					finally
					{
						await _phoneControl.StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
			}
			finally
			{
				if (cancellationToken.IsCancellationRequested)
				{
					ClearDesiredRunning();
				}
				Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Faulted, "PHONE_MEDIA_SCREEN_RESTART_FAILED", "手机屏幕重启失败，媒体播放保持暂停"));
			}
			throw;
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ClearDesiredRunning();
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Stopping, "PHONE_MEDIA_STOPPING", "正在停止手机媒体会话"));
			Exception phonePauseFailure = null;
			if (_sessionActive)
			{
				try
				{
					await EnsurePhonePlaybackPausedAsync(_activeDeviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex)
				{
					phonePauseFailure = ex;
				}
			}
			await StopOwnedChannelsAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Stopped, "PHONE_MEDIA_STOPPED", (phonePauseFailure == null) ? "手机媒体会话已停止，手机播放保持暂停" : "手机媒体会话已停止，但暂停手机播放失败"));
			if (phonePauseFailure != null)
			{
				ExceptionDispatchInfo.Capture(phonePauseFailure).Throw();
			}
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		if (_phoneConnection != null)
		{
			_phoneConnection.StateChanged -= OnPhoneConnectionStateChanged;
		}
		Task task = CancelReconnect();
		try
		{
			_ = 1;
			try
			{
				try
				{
					await task.ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException)
				{
				}
				await StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (AndroidConnectionException)
			{
			}
		}
		finally
		{
			_disposed = true;
			await _disposeLifetime.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
			_disposeLifetime.Dispose();
			_lifecycle.Dispose();
		}
	}

	private async ValueTask StartOwnedAsync(string? deviceKey, AndroidVideoOptions videoOptions, CancellationToken cancellationToken)
	{
		Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Starting, "PHONE_MEDIA_STARTING", "正在启动手机媒体会话"));
		await _phoneOverlay.StartAsync(deviceKey, videoOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await _phoneAudio.StartAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await _phoneControl.StartAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void PublishRunning()
	{
		Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Running, "PHONE_MEDIA_RUNNING", "手机画面和音频正在播放"));
	}

	private async ValueTask PrepareDeviceAsync(string? deviceKey)
	{
		DiscardOtherDevicePause(deviceKey);
		if (_activeDeviceKey != null && !string.Equals(_activeDeviceKey, deviceKey, StringComparison.Ordinal))
		{
			await StopOwnedChannelsAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private void DiscardOtherDevicePause(string? deviceKey)
	{
		AndroidMediaPauseLease pendingPhonePlaybackPause = _pendingPhonePlaybackPause;
		if ((object)pendingPhonePlaybackPause != null && !string.Equals(pendingPhonePlaybackPause.DeviceKey, deviceKey, StringComparison.Ordinal))
		{
			_pendingPhonePlaybackPause = null;
		}
	}

	private async ValueTask ResumePendingPhoneMediaAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		DiscardOtherDevicePause(deviceKey);
		AndroidMediaPauseLease pendingPhonePlaybackPause = _pendingPhonePlaybackPause;
		if ((object)pendingPhonePlaybackPause != null)
		{
			await _androidMediaPlayback.ResumeAfterVrReadyAsync(pendingPhonePlaybackPause, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			_pendingPhonePlaybackPause = null;
		}
	}

	private async ValueTask<AndroidMediaPauseLease> EnsurePhonePlaybackPausedAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		DiscardOtherDevicePause(deviceKey);
		AndroidMediaPauseLease pendingPhonePlaybackPause = _pendingPhonePlaybackPause;
		if ((object)pendingPhonePlaybackPause != null)
		{
			return pendingPhonePlaybackPause;
		}
		return _pendingPhonePlaybackPause = await _androidMediaPlayback.PauseForVrInterruptionAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void OnPhoneConnectionStateChanged(object? sender, AndroidConnectionChangedEventArgs eventArgs)
	{
		if (_disposed)
		{
			return;
		}
		lock (_reconnectGate)
		{
			AndroidConnectionSnapshot snapshot = eventArgs.Snapshot;
			if (snapshot.Revision <= (_lastConnectionSnapshot?.Revision ?? (-1)))
			{
				return;
			}
			AndroidConnectionSnapshot lastConnectionSnapshot = _lastConnectionSnapshot;
			bool flag = (object)lastConnectionSnapshot != null && lastConnectionSnapshot.IsReady && string.Equals(lastConnectionSnapshot.SelectedDevice?.DeviceKey, snapshot.SelectedDevice?.DeviceKey, StringComparison.Ordinal);
			_lastConnectionSnapshot = snapshot;
			if (!snapshot.IsReady)
			{
				CancelReconnect();
				if (IsDesiredRunning())
				{
					Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.WaitingForDevice, "PHONE_MEDIA_WAITING_FOR_DEVICE", "手机已断开，等待同一设备重新连接后自动恢复"));
				}
				return;
			}
			var (flag2, text, androidVideoOptions) = ReadDesiredRunning();
			if (!flag2 || (object)androidVideoOptions == null || string.IsNullOrWhiteSpace(text) || !string.Equals(snapshot.SelectedDevice?.DeviceKey, text, StringComparison.Ordinal))
			{
				CancelReconnect();
			}
			else if (!flag)
			{
				ScheduleReconnect(text, androidVideoOptions);
			}
		}
	}

	private void ScheduleReconnect(string deviceKey, AndroidVideoOptions videoOptions)
	{
		lock (_reconnectGate)
		{
			if (_reconnectLifetime != null)
			{
				CancelAndDisposeAsync(_reconnectLifetime);
			}
			_reconnectLifetime = CancellationTokenSource.CreateLinkedTokenSource(_disposeLifetime.Token);
			_reconnectTask = RecoverAfterReconnectAsync(deviceKey, videoOptions, _reconnectLifetime.Token);
		}
	}

	private async Task RecoverAfterReconnectAsync(string deviceKey, AndroidVideoOptions videoOptions, CancellationToken cancellationToken)
	{
		_ = 6;
		try
		{
			await Task.Delay(TimeSpan.FromMilliseconds(750L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			for (int attempt = 1; attempt <= 3; attempt = checked(attempt + 1))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!CanRecover(deviceKey))
				{
					break;
				}
				try
				{
					await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					try
					{
						if (CanRecover(deviceKey))
						{
							Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Starting, "PHONE_MEDIA_RECONNECTING", (attempt == 1) ? "手机已重新连接，正在自动恢复浮窗、音频和控制" : $"正在重试自动恢复（{attempt}/{3}）"));
							await StopOwnedChannelsAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							await StartOwnedAsync(deviceKey, videoOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							await ResumePendingPhoneMediaAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							_activeDeviceKey = deviceKey;
							_sessionActive = true;
							PublishRunning();
						}
						break;
					}
					catch
					{
						await StopOwnedChannelsAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
						throw;
					}
					finally
					{
						_lifecycle.Release();
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex2) when ((ex2 is AndroidConnectionException || ex2 is PhoneOverlayServiceException || ex2 is PhoneControlServiceException || ex2 is IOException || ex2 is InvalidOperationException) ? true : false)
				{
					if (attempt == 3)
					{
						Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Faulted, "PHONE_MEDIA_RECONNECT_FAILED", "手机重新连接后自动恢复失败，请手动重新打开浮窗"));
						break;
					}
					await Task.Delay(TimeSpan.FromSeconds(2L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			Publish(new PhoneMediaSessionSnapshot(PhoneMediaSessionState.Faulted, "PHONE_MEDIA_RECONNECT_UNEXPECTED", "手机重连恢复遇到未预期错误，请手动重新打开浮窗"));
		}
	}

	private async ValueTask StopOwnedChannelsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _phoneAudio.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			try
			{
				await _phoneOverlay.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				try
				{
					await _phoneControl.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				finally
				{
					_sessionActive = false;
					_activeDeviceKey = null;
				}
			}
		}
	}

	private bool CanRecover(string deviceKey)
	{
		var (flag, a, _) = ReadDesiredRunning();
		if (flag && string.Equals(a, deviceKey, StringComparison.Ordinal))
		{
			AndroidConnectionSnapshot androidConnectionSnapshot = _phoneConnection?.Snapshot;
			if ((object)androidConnectionSnapshot != null && androidConnectionSnapshot.IsReady)
			{
				return string.Equals(androidConnectionSnapshot.SelectedDevice?.DeviceKey, deviceKey, StringComparison.Ordinal);
			}
		}
		return false;
	}

	private void SetDesiredRunning(string? deviceKey, AndroidVideoOptions videoOptions)
	{
		CancelReconnect();
		lock (_stateGate)
		{
			_desiredRunning = true;
			_desiredDeviceKey = deviceKey;
			_desiredVideoOptions = videoOptions;
		}
	}

	private void ClearDesiredRunning()
	{
		lock (_stateGate)
		{
			_desiredRunning = false;
			_desiredDeviceKey = null;
			_desiredVideoOptions = null;
		}
		CancelReconnect();
	}

	private bool IsDesiredRunning()
	{
		lock (_stateGate)
		{
			return _desiredRunning;
		}
	}

	private (bool Desired, string? DeviceKey, AndroidVideoOptions? Options) ReadDesiredRunning()
	{
		lock (_stateGate)
		{
			return (Desired: _desiredRunning, DeviceKey: _desiredDeviceKey, Options: _desiredVideoOptions);
		}
	}

	private Task CancelReconnect()
	{
		lock (_reconnectGate)
		{
			if (_reconnectLifetime != null)
			{
				CancelAndDisposeAsync(_reconnectLifetime);
			}
			_reconnectLifetime = null;
			Task reconnectTask = _reconnectTask;
			_reconnectTask = Task.CompletedTask;
			return reconnectTask;
		}
	}

	private static async Task CancelAndDisposeAsync(CancellationTokenSource source)
	{
		await source.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
		source.Dispose();
	}

	private void Publish(PhoneMediaSessionSnapshot snapshot)
	{
		lock (_stateGate)
		{
			_snapshot = snapshot;
		}
		StateChanged?.Invoke(this, new PhoneMediaSessionChangedEventArgs(snapshot));
	}
}
