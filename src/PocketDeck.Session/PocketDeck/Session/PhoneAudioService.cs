using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;
using PocketDeck.Media;

namespace PocketDeck.Session;

public sealed class PhoneAudioService : IPhoneAudioService, IAsyncDisposable
{
	private const int _maximumAutomaticAttempts = 3;

	private readonly TimeSpan _retryDelay;

	private readonly IAndroidAudioSessionFactory _audioSessions;

	private readonly IAndroidConnectionLogSink _log;

	private readonly TimeSpan _startupTimeout;

	private readonly object _gate = new object();

	private PhoneAudioSnapshot _snapshot = new PhoneAudioSnapshot(PhoneAudioState.Stopped, "AUDIO_NOT_STARTED", "手机音频未运行", 0L, 0L);

	private PhoneAudioSnapshot _diagnosticSnapshot = new PhoneAudioSnapshot(PhoneAudioState.Stopped, "AUDIO_NOT_STARTED", "手机音频未运行", 0L, 0L);

	private PhoneAudioWorker? _worker;

	private long _workerGeneration;

	private long _stopRevision;

	public PhoneAudioSnapshot Snapshot
	{
		get
		{
			lock (_gate)
			{
				return _snapshot;
			}
		}
	}

	public PhoneAudioSnapshot DiagnosticSnapshot
	{
		get
		{
			lock (_gate)
			{
				return _diagnosticSnapshot;
			}
		}
	}

	public event EventHandler<PhoneAudioChangedEventArgs>? StateChanged;

	public PhoneAudioService(IAndroidAudioSessionFactory audioSessions, IAndroidConnectionLogSink log, TimeSpan? startupTimeout = null, TimeSpan? retryDelay = null)
	{
		_audioSessions = audioSessions ?? throw new ArgumentNullException("audioSessions");
		_log = log ?? throw new ArgumentNullException("log");
		_startupTimeout = startupTimeout ?? TimeSpan.FromSeconds(20L);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(3L);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_startupTimeout, TimeSpan.Zero, "_startupTimeout");
		ArgumentOutOfRangeException.ThrowIfLessThan(_retryDelay, TimeSpan.Zero, "_retryDelay");
	}

	public async ValueTask StartAsync(string? deviceKey, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		PhoneAudioWorker worker2;
		long workerGeneration;
		long stopRevision;
		while (true)
		{
			PhoneAudioWorker worker;
			lock (_gate)
			{
				worker = _worker;
				checked
				{
					if (worker == null)
					{
						worker2 = (_worker = new PhoneAudioWorker((CancellationToken token) => RunAsync(deviceKey, started, token)));
						_workerGeneration++;
						workerGeneration = _workerGeneration;
						stopRevision = _stopRevision;
						break;
					}
				}
				if (!worker.RunTask.IsCompleted)
				{
					PhoneAudioState state = _snapshot.State;
					if ((uint)(state - 3) > 1u)
					{
						return;
					}
				}
			}
			await StopWorkerAsync(worker).ConfigureAwait(continueOnCapturedContext: false);
		}
		Task timeoutTask = Task.Delay(_startupTimeout, cancellationToken);
		try
		{
			InlineArray3<Task> buffer = default;
			buffer[0] = started.Task;
			buffer[1] = worker2.RunTask;
			buffer[2] = timeoutTask;
			Task task = await Task.WhenAny(buffer).ConfigureAwait(continueOnCapturedContext: false);
			if (task == started.Task)
			{
				await started.Task.ConfigureAwait(continueOnCapturedContext: false);
				return;
			}
			if (task == worker2.RunTask)
			{
				await worker2.RunTask.ConfigureAwait(continueOnCapturedContext: false);
				if (worker2.IsStopRequested)
				{
					await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
					return;
				}
				await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
				PublishIfLifecycleMatches(workerGeneration, stopRevision, new PhoneAudioSnapshot(PhoneAudioState.Degraded, "AUDIO_START_ENDED", "手机音频启动流程提前结束，视频和控制不受影响", 0L, 0L));
				return;
			}
			await timeoutTask.ConfigureAwait(continueOnCapturedContext: false);
			await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
			PublishIfLifecycleMatches(workerGeneration, stopRevision, new PhoneAudioSnapshot(PhoneAudioState.Degraded, "AUDIO_START_TIMEOUT", "手机音频启动超时，后台启动任务已停止；视频和控制不受影响", 0L, 0L));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
			PublishIfLifecycleMatches(workerGeneration, stopRevision, new PhoneAudioSnapshot(PhoneAudioState.Stopped, "AUDIO_START_CANCELLED", "手机音频启动已取消", 0L, 0L));
			throw;
		}
		catch (OperationCanceledException) when (worker2.IsStopRequested)
		{
			await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			await StopWorkerAsync(worker2).ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		checked
		{
			PhoneAudioWorker worker;
			lock (_gate)
			{
				worker = _worker;
				if (worker != null)
				{
					_stopRevision++;
				}
			}
			if (worker == null)
			{
				if (Snapshot.State != PhoneAudioState.Stopped)
				{
					Publish(new PhoneAudioSnapshot(PhoneAudioState.Stopped, "AUDIO_STOPPED", "手机音频已停止", 0L, 0L));
				}
				return;
			}
			Publish(Snapshot with
			{
				State = PhoneAudioState.Stopping,
				ReasonCode = "AUDIO_STOPPING",
				Message = "正在停止手机音频"
			});
			await StopWorkerAsync(worker).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new PhoneAudioSnapshot(PhoneAudioState.Stopped, "AUDIO_STOPPED", "手机音频已停止", 0L, 0L));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task RunAsync(string? deviceKey, TaskCompletionSource started, CancellationToken cancellationToken)
	{
		for (int attempt = 1; attempt <= 3; attempt = checked(attempt + 1))
		{
			Publish(new PhoneAudioSnapshot(PhoneAudioState.Starting, "AUDIO_STARTING", (attempt == 1) ? "正在连接手机内部音频" : $"正在重试手机音频（{attempt}/{3}）", 0L, 0L));
			try
			{
				await RunSessionAsync(deviceKey, started, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				throw new AudioPipelineException("AUDIO_STREAM_ENDED", "手机音频流已结束");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				started.TrySetCanceled(cancellationToken);
				return;
			}
			catch (Exception ex2) when (IsExpectedAudioFailure(ex2))
			{
				var (reasonCode, text) = MapFailure(ex2);
				Publish(new PhoneAudioSnapshot(PhoneAudioState.Degraded, reasonCode, text + "；视频和控制继续可用", 0L, 0L));
				started.TrySetResult();
				IAndroidConnectionLogSink log = _log;
				DateTimeOffset utcNow = DateTimeOffset.UtcNow;
				string fullName = ex2.GetType().FullName;
				string message = ex2.Message;
				log.TryWrite(new AndroidConnectionLogEntry(utcNow, "audio_degraded", reasonCode, text, deviceKey, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, fullName, message));
				if (attempt < 3)
				{
					try
					{
						await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						return;
					}
				}
			}
		}
		Publish(new PhoneAudioSnapshot(PhoneAudioState.Faulted, "AUDIO_RECOVERY_EXHAUSTED", "手机音频自动恢复失败，请等待手机重连或重新打开浮窗", 0L, 0L));
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "audio_faulted", "AUDIO_RECOVERY_EXHAUSTED", "手机音频自动恢复次数已耗尽", deviceKey));
	}

	private async Task RunSessionAsync(string? deviceKey, TaskCompletionSource started, CancellationToken cancellationToken)
	{
		checked
		{
			await using IAndroidAudioSession session = await _audioSessions.OpenAsync(deviceKey, new AndroidAudioOptions(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (session.Codec != AndroidAudioCodec.Opus)
			{
				throw new AudioPipelineException("AUDIO_CODEC_UNSUPPORTED", $"当前音频编码 {session.Codec} 尚未接入播放");
			}
			await using IAudioDecoder decoder = AudioPipelineFactory.CreateOpusDecoder();
			await using IAudioSink sink = await AudioPipelineFactory.CreateDefaultWasapiSinkAsync().ConfigureAwait(continueOnCapturedContext: false);
			long decodedPackets = 0L;
			long droppedBuffers = 0L;
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				using AndroidAudioStreamItem item = await session.ReadNextAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (item.Kind == AndroidAudioStreamItemKind.Configuration)
				{
					continue;
				}
				using DecodedAudioFrame frame = decoder.Decode(item.Payload.Span, item.PresentationTimeMicroseconds.GetValueOrDefault());
				if (AudioBufferPolicy.ShouldReset(sink.BufferedDuration))
				{
					sink.Clear();
					droppedBuffers++;
				}
				await sink.WriteAsync(frame.Samples, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				decodedPackets++;
				PhoneAudioSnapshot snapshot = new PhoneAudioSnapshot(PhoneAudioState.Playing, "AUDIO_PLAYING", "手机内部音频正在播放", decodedPackets, droppedBuffers, sink.BufferedDuration, sink.OutputDeviceId);
				Publish(snapshot);
				started.TrySetResult();
			}
		}
	}

	private async ValueTask StopWorkerAsync(PhoneAudioWorker worker)
	{
		try
		{
			await worker.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			lock (_gate)
			{
				if (_worker == worker)
				{
					_worker = null;
				}
			}
		}
	}

	private void PublishIfLifecycleMatches(long expectedWorkerGeneration, long expectedStopRevision, PhoneAudioSnapshot snapshot)
	{
		bool flag;
		lock (_gate)
		{
			if (_workerGeneration != expectedWorkerGeneration || _stopRevision != expectedStopRevision)
			{
				return;
			}
			flag = IsMeaningfulChange(_snapshot, snapshot);
			_snapshot = snapshot;
			RetainDiagnosticSnapshot(snapshot);
		}
		if (flag)
		{
			StateChanged?.Invoke(this, new PhoneAudioChangedEventArgs(snapshot));
		}
	}

	private void Publish(PhoneAudioSnapshot snapshot)
	{
		bool flag;
		lock (_gate)
		{
			flag = IsMeaningfulChange(_snapshot, snapshot);
			_snapshot = snapshot;
			RetainDiagnosticSnapshot(snapshot);
		}
		if (flag)
		{
			StateChanged?.Invoke(this, new PhoneAudioChangedEventArgs(snapshot));
		}
	}

	private static bool IsMeaningfulChange(PhoneAudioSnapshot current, PhoneAudioSnapshot next)
	{
		if (current.State == next.State && !(current.ReasonCode != next.ReasonCode) && next.DecodedPackets != 1 && next.DecodedPackets / 100 == current.DecodedPackets / 100)
		{
			return next.DroppedBuffers != current.DroppedBuffers;
		}
		return true;
	}

	private void RetainDiagnosticSnapshot(PhoneAudioSnapshot snapshot)
	{
		if (snapshot.DecodedPackets > 0 || snapshot.DroppedBuffers > 0 || snapshot.BufferedDuration > TimeSpan.Zero || !string.IsNullOrWhiteSpace(snapshot.OutputDeviceId))
		{
			_diagnosticSnapshot = snapshot;
		}
	}

	private static bool IsExpectedAudioFailure(Exception exception)
	{
		if (exception is AndroidConnectionException || exception is AudioPipelineException || exception is IOException || exception is InvalidOperationException || exception is COMException)
		{
			return true;
		}
		return false;
	}

	private static (string Code, string Message) MapFailure(Exception exception)
	{
		if (!(exception is AndroidConnectionException ex))
		{
			if (!(exception is AudioPipelineException ex2))
			{
				if (exception is COMException)
				{
					return (Code: "AUDIO_OUTPUT_UNAVAILABLE", Message: "Windows 音频输出设备暂时不可用");
				}
				return (Code: "AUDIO_PIPELINE_FAILED", Message: "手机音频链路暂时不可用");
			}
			return (Code: ex2.ReasonCode, Message: ex2.Message);
		}
		return (Code: ex.ReasonCode, Message: ex.Message);
	}
}
