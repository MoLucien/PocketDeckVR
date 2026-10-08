using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;
using PocketDeck.Contracts;
using PocketDeck.Input;

namespace PocketDeck.Session;

public sealed class PhoneControlService(IAndroidControlSessionFactory sessions, IAndroidConnectionLogSink log) : IPhoneControlService, IAsyncDisposable
{
	private const long _primaryPointerId = -2L;

	private readonly IAndroidControlSessionFactory _sessions = sessions;

	private readonly IAndroidConnectionLogSink _log = log;

	private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);

	private readonly object _stateGate = new object();

	private PhoneInputDispatcher? _dispatcher;

	private IAndroidControlSession? _session;

	private long _sequence;

	private bool _disposed;

	private PhoneControlSnapshot _diagnosticSnapshot = new PhoneControlSnapshot(PhoneControlState.Stopped, "PHONE_CONTROL_NOT_STARTED", "手机控制尚未启动", null, 0L, 0L, 0.0);

	public PhoneControlSnapshot Snapshot { get; private set; } = new PhoneControlSnapshot(PhoneControlState.Stopped, "PHONE_CONTROL_NOT_STARTED", "手机控制尚未启动", null, 0L, 0L, 0.0);

	public PhoneControlSnapshot DiagnosticSnapshot
	{
		get
		{
			lock (_stateGate)
			{
				return _diagnosticSnapshot;
			}
		}
	}

	public event EventHandler<PhoneControlChangedEventArgs>? StateChanged;

	public async ValueTask StartAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (string.IsNullOrWhiteSpace(deviceKey))
		{
			throw new PhoneControlServiceException("PHONE_CONTROL_DEVICE_NOT_READY", "没有可用于控制的手机");
		}
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			lock (_stateGate)
			{
				if (_dispatcher != null && _session != null && string.Equals(_session.DeviceKey, deviceKey, StringComparison.Ordinal) && Snapshot.State == PhoneControlState.Ready)
				{
					return;
				}
			}
			await StopOwnedAsync().ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneControlSnapshot(PhoneControlState.Starting, "PHONE_CONTROL_STARTING", "正在建立独立手机控制通道", deviceKey, 0L, 0L, 0.0));
			try
			{
				IAndroidControlSession androidControlSession = await _sessions.OpenAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				PhoneInputDispatcher dispatcher = new PhoneInputDispatcher(androidControlSession.SendAsync);
				lock (_stateGate)
				{
					_session = androidControlSession;
					_dispatcher = dispatcher;
				}
				Publish(new PhoneControlSnapshot(PhoneControlState.Ready, "PHONE_CONTROL_READY", "手机控制已就绪", androidControlSession.DeviceKey, 0L, 0L, 0.0));
				WriteLog("control_pipeline", "PHONE_CONTROL_READY", "手机控制已就绪", androidControlSession.DeviceKey);
			}
			catch (Exception ex) when ((ex is AndroidConnectionException || ex is IOException) ? true : false)
			{
				Publish(new PhoneControlSnapshot(PhoneControlState.Faulted, "PHONE_CONTROL_START_FAILED", "手机控制通道启动失败", deviceKey, 0L, 0L, 0.0));
				throw new PhoneControlServiceException("PHONE_CONTROL_START_FAILED", "手机控制通道启动失败", ex);
			}
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async ValueTask SendAsync(PhoneInputCommandKind kind, float normalizedX, float normalizedY, int screenWidth, int screenHeight, float scrollDelta, CancellationToken cancellationToken, int unlockDigit = -1)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		PhoneInputDispatcher dispatcher;
		string deviceKey;
		PhoneControlState state;
		lock (_stateGate)
		{
			dispatcher = _dispatcher;
			deviceKey = _session?.DeviceKey;
			state = Snapshot.State;
		}
		if (state == PhoneControlState.Faulted && !string.IsNullOrWhiteSpace(deviceKey))
		{
			await StartAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			lock (_stateGate)
			{
				dispatcher = _dispatcher;
				deviceKey = _session?.DeviceKey;
			}
		}
		if (dispatcher == null || string.IsNullOrWhiteSpace(deviceKey))
		{
			throw new PhoneControlServiceException("PHONE_CONTROL_NOT_READY", "手机控制尚未就绪");
		}
		bool flag = (((uint)kind <= 3u || kind == PhoneInputCommandKind.Scroll) ? true : false);
		bool flag2 = flag;
		if (flag2 && (screenWidth < 1 || screenHeight < 1))
		{
			throw new PhoneControlServiceException("PHONE_CONTROL_VIDEO_SIZE_MISSING", "尚未获得手机画面尺寸，无法发送触控");
		}
		PhoneInputCommand command = new PhoneInputCommand(Interlocked.Increment(ref _sequence), kind, -2L, normalizedX, normalizedY, flag2 ? screenWidth : 0, flag2 ? screenHeight : 0, DateTimeOffset.UtcNow, scrollDelta, unlockDigit);
		try
		{
			await dispatcher.EnqueueAsync(command, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when ((ex is AndroidConnectionException || ex is IOException || ex is InvalidOperationException) ? true : false)
		{
			PhoneInputDispatcherSnapshot snapshot = dispatcher.Snapshot;
			Publish(new PhoneControlSnapshot(PhoneControlState.Faulted, "PHONE_CONTROL_SEND_FAILED", "手机控制指令发送失败；下次操作会重新建立通道", deviceKey, snapshot.SentCommands, snapshot.ReplacedPointerMoves, snapshot.LastQueueDelayMilliseconds));
			throw new PhoneControlServiceException("PHONE_CONTROL_SEND_FAILED", "手机控制指令发送失败", ex);
		}
		if (kind != PhoneInputCommandKind.PointerMove)
		{
			PhoneInputDispatcherSnapshot snapshot2 = dispatcher.Snapshot;
			Publish(new PhoneControlSnapshot(PhoneControlState.Ready, "PHONE_CONTROL_COMMAND_SENT", "手机操作已发送：" + CommandName(kind), deviceKey, snapshot2.SentCommands, snapshot2.ReplacedPointerMoves, snapshot2.LastQueueDelayMilliseconds));
		}
	}

	public void QueueFromSteamVr(PhoneInputCommandKind kind, float normalizedX, float normalizedY, int screenWidth, int screenHeight, float scrollDelta = 0f, int unlockDigit = -1)
	{
		ValueTask pending = SendAsync(kind, normalizedX, normalizedY, screenWidth, screenHeight, scrollDelta, CancellationToken.None, unlockDigit);
		if (!pending.IsCompletedSuccessfully)
		{
			ObserveSteamVrCommandAsync(pending);
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			Publish(Snapshot with
			{
				State = PhoneControlState.Stopping,
				ReasonCode = "PHONE_CONTROL_STOPPING",
				Message = "正在停止手机控制"
			});
			await StopOwnedAsync().ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneControlSnapshot(PhoneControlState.Stopped, "PHONE_CONTROL_STOPPED", "手机控制已停止", null, 0L, 0L, 0.0));
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			_disposed = true;
			_lifecycle.Dispose();
		}
	}

	private async ValueTask StopOwnedAsync()
	{
		PhoneInputDispatcher dispatcher;
		IAndroidControlSession session;
		lock (_stateGate)
		{
			dispatcher = _dispatcher;
			session = _session;
			_dispatcher = null;
			_session = null;
		}
		if (dispatcher != null)
		{
			PhoneInputDispatcherSnapshot metrics = dispatcher.Snapshot;
			await dispatcher.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			IAndroidConnectionLogSink log = _log;
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			string message = $"手机控制统计；已发送 {metrics.SentCommands}；替换 MOVE {metrics.ReplacedPointerMoves}；最后排队 {metrics.LastQueueDelayMilliseconds:F1} ms";
			string deviceKey = session?.DeviceKey;
			long? packetCount = metrics.SentCommands;
			int? queueDepth = metrics.ReliableQueueDepth;
			long? replacedMoveCount = metrics.ReplacedPointerMoves;
			log.TryWrite(new AndroidConnectionLogEntry(utcNow, "control_metrics", "PHONE_CONTROL_METRICS", message, deviceKey, null, null, null, null, null, null, null, null, null, null, packetCount, null, null, null, queueDepth, replacedMoveCount));
		}
		if (session != null)
		{
			await session.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static async Task ObserveSteamVrCommandAsync(ValueTask pending)
	{
		try
		{
			await pending.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when ((ex is PhoneControlServiceException || ex is ObjectDisposedException) ? true : false)
		{
		}
	}

	private void Publish(PhoneControlSnapshot snapshot)
	{
		lock (_stateGate)
		{
			Snapshot = snapshot;
			if (snapshot.SentCommands > 0 || snapshot.ReplacedPointerMoves > 0 || snapshot.LastQueueDelayMilliseconds > 0.0)
			{
				_diagnosticSnapshot = snapshot;
			}
		}
		StateChanged?.Invoke(this, new PhoneControlChangedEventArgs(snapshot));
	}

	private void WriteLog(string eventName, string reasonCode, string message, string? deviceKey)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, reasonCode, message, deviceKey));
	}

	private static string CommandName(PhoneInputCommandKind kind)
	{
		return kind switch
		{
			PhoneInputCommandKind.PointerDown => "按下", 
			PhoneInputCommandKind.PointerUp => "抬起", 
			PhoneInputCommandKind.PointerCancel => "取消触控", 
			PhoneInputCommandKind.Back => "返回", 
			PhoneInputCommandKind.Home => "桌面", 
			PhoneInputCommandKind.RecentApps => "最近任务", 
			PhoneInputCommandKind.OpenControlPanel => "控制栏", 
			PhoneInputCommandKind.Screenshot => "截屏", 
			PhoneInputCommandKind.Scroll => "滚动", 
			PhoneInputCommandKind.WakeScreen => "唤醒屏幕", 
			PhoneInputCommandKind.UserActivity => "刷新屏幕活动", 
			PhoneInputCommandKind.UnlockDigit => "安全数字输入", 
			PhoneInputCommandKind.UnlockBackspace => "安全输入删除", 
			PhoneInputCommandKind.UnlockConfirm => "安全输入确认", 
			_ => kind.ToString(), 
		};
	}
}
