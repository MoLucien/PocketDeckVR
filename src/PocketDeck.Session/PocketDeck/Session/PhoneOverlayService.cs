using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;
using PocketDeck.Contracts;
using PocketDeck.Media;
using PocketDeck.SteamVR;

namespace PocketDeck.Session;

public sealed class PhoneOverlayService(IAndroidVideoSessionFactory videoSessions, IPhoneControlService phoneControl, IAndroidKeyguardStateService keyguard, IAndroidConnectionLogSink log) : IPhoneOverlayService, IAsyncDisposable
{
	private readonly object _gate = new object();

	private readonly IAndroidVideoSessionFactory _videoSessions = videoSessions;

	private readonly IPhoneControlService _phoneControl = phoneControl;

	private readonly PhoneKeyguardMonitor _keyguardMonitor = new PhoneKeyguardMonitor(keyguard, log);

	private readonly IAndroidConnectionLogSink _log = log;

	private PhoneOverlaySnapshot _snapshot = new PhoneOverlaySnapshot(PhoneOverlayState.Stopped, "VIDEO_OVERLAY_NOT_STARTED", "手机浮窗未运行", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L);

	private PhoneOverlaySnapshot _diagnosticSnapshot = new PhoneOverlaySnapshot(PhoneOverlayState.Stopped, "VIDEO_OVERLAY_NOT_STARTED", "手机浮窗未运行", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L);

	private CancellationTokenSource? _runLifetime;

	private Task? _runTask;

	private IOpenVrSceneOverlay? _activeOverlay;

	private bool _keepAwakeWhileGrabbed;

	private volatile bool _unlockKeypadEnabled;

	public PhoneOverlaySnapshot Snapshot
	{
		get
		{
			lock (_gate)
			{
				return _snapshot;
			}
		}
	}

	public PhoneOverlaySnapshot DiagnosticSnapshot
	{
		get
		{
			lock (_gate)
			{
				return _diagnosticSnapshot;
			}
		}
	}

	public event EventHandler<PhoneOverlayChangedEventArgs>? StateChanged;

	public void ConfigureLockScreenFeatures(bool keepAwakeWhileGrabbed, bool unlockKeypadEnabled)
	{
		IOpenVrSceneOverlay activeOverlay;
		lock (_gate)
		{
			_keepAwakeWhileGrabbed = keepAwakeWhileGrabbed;
			_unlockKeypadEnabled = unlockKeypadEnabled;
			activeOverlay = _activeOverlay;
		}
		try
		{
			activeOverlay?.ConfigureLockScreenFeatures(keepAwakeWhileGrabbed, unlockKeypadEnabled);
		}
		catch (ObjectDisposedException)
		{
		}
	}

	public bool HasSavedBindingChanged()
	{
		return OpenVrBindingRecovery.HasSavedBindingChanged();
	}

	public OpenVrBindingResult OpenBindingUi()
	{
		IOpenVrSceneOverlay activeOverlay;
		lock (_gate)
		{
			activeOverlay = _activeOverlay;
		}
		if (activeOverlay == null)
		{
			return OpenVrBindingUiLauncher.Open();
		}
		try
		{
			return activeOverlay.OpenBindingUi();
		}
		catch (ObjectDisposedException)
		{
			return new OpenVrBindingResult(Succeeded: false, "OPENVR_INPUT_STOPPING", "SteamVR 手机浮窗正在关闭，请重新打开后再进入手柄绑定页面");
		}
	}

	public OpenVrBindingResult ReloadLocalBinding()
	{
		bool flag;
		lock (_gate)
		{
			int num;
			if (_activeOverlay == null)
			{
				Task runTask = _runTask;
				num = ((runTask != null && !runTask.IsCompleted) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			flag = (byte)num != 0;
		}
		if (flag)
		{
			return new OpenVrBindingResult(Succeeded: false, "OPENVR_BINDING_OVERLAY_ACTIVE", "请先完整关闭 SteamVR 手机浮窗，再重新加载本地绑定");
		}
		OpenVrBindingResult openVrBindingResult = OpenVrBindingRecovery.PrepareLocalBinding();
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "steamvr_binding_recovery", openVrBindingResult.ReasonCode, openVrBindingResult.Message));
		return openVrBindingResult;
	}

	public async ValueTask StartAsync(string? deviceKey, AndroidVideoOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options, "options");
		TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_gate)
		{
			Task runTask = _runTask;
			if (runTask != null && !runTask.IsCompleted)
			{
				if (_snapshot.State == PhoneOverlayState.Running)
				{
					return;
				}
				throw new PhoneOverlayServiceException("VIDEO_OVERLAY_ALREADY_STARTING", "手机浮窗正在启动");
			}
			_runLifetime?.Dispose();
			CancellationTokenSource lifetime = new CancellationTokenSource();
			_runLifetime = lifetime;
			_runTask = Task.Run(() => RunAsync(deviceKey, options, started, lifetime.Token), CancellationToken.None);
		}
		try
		{
			await started.Task.WaitAsync(TimeSpan.FromSeconds(35L), cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await StopAsync(CancellationToken.None);
			throw;
		}
		catch (TimeoutException inner)
		{
			await StopAsync(CancellationToken.None);
			throw new PhoneOverlayServiceException("VIDEO_OVERLAY_START_TIMEOUT", "35 秒内没有在 SteamVR 中显示手机画面", inner);
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		CancellationTokenSource runLifetime;
		Task runTask;
		lock (_gate)
		{
			runLifetime = _runLifetime;
			runTask = _runTask;
		}
		if (runTask == null)
		{
			Publish(new PhoneOverlaySnapshot(PhoneOverlayState.Stopped, "VIDEO_OVERLAY_NOT_STARTED", "手机浮窗未运行", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L));
			return;
		}
		Publish(Snapshot with
		{
			State = PhoneOverlayState.Stopping,
			ReasonCode = "VIDEO_OVERLAY_STOPPING",
			Message = "正在关闭手机浮窗"
		});
		if (runLifetime != null)
		{
			await runLifetime.CancelAsync();
		}
		try
		{
			await runTask;
		}
		finally
		{
			lock (_gate)
			{
				if (_runTask == runTask)
				{
					_runTask = null;
					_runLifetime?.Dispose();
					_runLifetime = null;
				}
			}
			Publish(new PhoneOverlaySnapshot(PhoneOverlayState.Stopped, "VIDEO_OVERLAY_STOPPED", "手机浮窗已关闭", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None);
		OpenVrBindingUiLauncher.Shutdown();
	}

	private async Task RunAsync(string? deviceKey, AndroidVideoOptions options, TaskCompletionSource started, CancellationToken cancellationToken)
	{
		Publish(new PhoneOverlaySnapshot(PhoneOverlayState.Starting, "VIDEO_OVERLAY_STARTING", "正在连接手机视频并创建 SteamVR 浮窗", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L));
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "video_overlay", "VIDEO_OVERLAY_STARTING", "正在连接手机视频并创建 SteamVR 浮窗", deviceKey));
		try
		{
			await using IAndroidVideoSession session = await _videoSessions.OpenAsync(deviceKey, options, cancellationToken);
			if (session.Codec != AndroidVideoCodec.H264)
			{
				throw new PhoneOverlayServiceException("VIDEO_OVERLAY_CODEC_UNSUPPORTED", $"当前视频编码 {session.Codec} 尚未接入浮窗播放");
			}
			int width = 0;
			int height = 0;
			int generation = -1;
			PhoneOverlayMetrics metrics = new PhoneOverlayMetrics();
			PhoneOverlayLogGate logGate = new PhoneOverlayLogGate();
			Stopwatch rateClock = Stopwatch.StartNew();
			Stopwatch sessionClock = Stopwatch.StartNew();
			IH264VideoDecoder decoder = null;
			IGpuVideoFramePresenter presenter = null;
			IOpenVrSceneOverlay overlay = null;
			using CancellationTokenSource keyguardLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			Task keyguardTask = Task.CompletedTask;
			try
			{
				overlay = OpenVrSceneOverlayFactory.Create();
				overlay.PhoneInputReceived += OnPhoneInputReceived;
				lock (_gate)
				{
					_activeOverlay = overlay;
					overlay.ConfigureLockScreenFeatures(_keepAwakeWhileGrabbed, _unlockKeypadEnabled);
				}
				await using LatestAndroidVideoFrameReader latestFrames = new LatestAndroidVideoFrameReader(session, cancellationToken);
				keyguardTask = _keyguardMonitor.RunAsync(deviceKey, () => _unlockKeypadEnabled, overlay.SetPhoneLocked, keyguardLifetime.Token);
				while (true)
				{
					AndroidLatestVideoFrame androidLatestVideoFrame = await latestFrames.ReadLatestAsync(cancellationToken);
					if (androidLatestVideoFrame == null)
					{
						break;
					}
					cancellationToken.ThrowIfCancellationRequested();
					using (androidLatestVideoFrame)
					{
						if (generation != androidLatestVideoFrame.Generation)
						{
							bool flag = width > 0 && height > 0 && (width != androidLatestVideoFrame.Width || height != androidLatestVideoFrame.Height);
							generation = androidLatestVideoFrame.Generation;
							width = androidLatestVideoFrame.Width;
							height = androidLatestVideoFrame.Height;
							decoder?.Dispose();
							decoder = VideoDecoderFactory.CreateMediaFoundationH264(width, height, 60, androidLatestVideoFrame.Configuration.Span);
							if (flag)
							{
								Publish(Snapshot with
								{
									State = PhoneOverlayState.Starting,
									ReasonCode = "VIDEO_OVERLAY_RECONFIGURING",
									Message = $"手机方向变化，正在切换到 {width}×{height}"
								});
								IAndroidConnectionLogSink log = _log;
								DateTimeOffset utcNow = DateTimeOffset.UtcNow;
								string deviceKey2 = session.DeviceKey;
								int? videoWidth = width;
								int? videoHeight = height;
							long? reconfigPacketCount = metrics.SubmittedFrames;
							log.TryWrite(new AndroidConnectionLogEntry(utcNow, "video_overlay", "VIDEO_OVERLAY_RECONFIGURING", "检测到手机画面尺寸变化，正在重建解码阶段", deviceKey2, null, null, null, null, null, null, null, null, videoWidth, videoHeight, reconfigPacketCount));
							}
						}
						IH264VideoDecoder iH264VideoDecoder = decoder ?? throw new InvalidOperationException("Video decoder was not initialized for the current stream generation.");
						using DecodedVideoFrame decodedVideoFrame = iH264VideoDecoder.DecodePacket(androidLatestVideoFrame.Payload.Span, androidLatestVideoFrame.PresentationTimeMicroseconds, androidLatestVideoFrame.IsKeyFrame);
						if (decodedVideoFrame == null)
						{
							continue;
						}
						if (presenter == null)
						{
							presenter = GpuVideoFramePresenterFactory.CreateD3D11(overlay.GraphicsAdapterLuid);
						}
						GpuVideoFrame gpuVideoFrame = presenter.Present(decodedVideoFrame);
						overlay.Submit(gpuVideoFrame);
						long? packetCount = androidLatestVideoFrame.EstimatedCaptureTimestamp;
						double? latencyMilliseconds;
						if (packetCount.HasValue)
						{
							long valueOrDefault = packetCount.GetValueOrDefault();
							latencyMilliseconds = Stopwatch.GetElapsedTime(valueOrDefault, Stopwatch.GetTimestamp()).TotalMilliseconds;
						}
						else
						{
							latencyMilliseconds = null;
						}
						metrics.RecordSubmittedFrame(latencyMilliseconds);
						if (metrics.TrySampleThroughput(rateClock.Elapsed, sessionClock.Elapsed, latestFrames.EncodedPayloadBytes))
						{
							rateClock.Restart();
						}
						OpenVrPhoneInteractionSnapshot interaction = overlay.Interaction;
						OpenVrBindingHealthSnapshot bindingHealth = overlay.BindingHealth;
						if (logGate.ShouldLogInteraction(interaction))
						{
							IAndroidConnectionLogSink log2 = _log;
							DateTimeOffset utcNow2 = DateTimeOffset.UtcNow;
							string reasonCode = interaction.ReasonCode;
							string message = $"{interaction.Message}；世界固定={interaction.WorldAnchored}；抓取={interaction.Grabbed}；命中={interaction.Hovered}；活动手柄姿态有效={interaction.ControllerPoseValid}；" + "射线来源=程序有限矩形求交";
							string deviceKey3 = session.DeviceKey;
							int? videoHeight = gpuVideoFrame.Width;
							int? videoWidth = gpuVideoFrame.Height;
							string diagnosticExceptionType = interaction.DiagnosticExceptionType;
							string diagnosticExceptionMessage = interaction.DiagnosticExceptionMessage;
							log2.TryWrite(new AndroidConnectionLogEntry(utcNow2, "steamvr_phone_input", reasonCode, message, deviceKey3, null, null, null, null, null, null, null, null, videoHeight, videoWidth, null, null, null, null, null, null, diagnosticExceptionType, diagnosticExceptionMessage));
						}
						if (logGate.ShouldLogBindingHealth(bindingHealth.ReasonCode))
						{
							_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "steamvr_binding", bindingHealth.ReasonCode, bindingHealth.Message, session.DeviceKey));
						}
						PhoneOverlaySnapshot snapshot = new PhoneOverlaySnapshot(PhoneOverlayState.Running, "VIDEO_OVERLAY_RUNNING", "手机画面正在 SteamVR 中播放", gpuVideoFrame.Width, gpuVideoFrame.Height, metrics.SubmittedFrames, latestFrames.DroppedFrames, metrics.FramesPerSecond, interaction.InputReady, interaction.WorldAnchored, interaction.Hovered, interaction.Grabbed, interaction.UnlockKeypadExpanded, interaction.ReasonCode, bindingHealth.State, bindingHealth.ShowNotice, bindingHealth.ReasonCode, bindingHealth.Message, options.ResolutionPercent, options.VideoBitrateBitsPerSecond / 1000000, options.MaximumFramesPerSecond, metrics.AverageBitrateMbps, metrics.PeakBitrateMbps, metrics.AverageLatencyMilliseconds, iH264VideoDecoder.BackendName, presenter.AdapterBackend, overlay.GraphicsAdapterLuid, interaction.HeadsetModel, interaction.ControllerType, interaction.PointerPoseBound, interaction.PointerPoseActive, interaction.TouchBound, interaction.TouchActive, interaction.GrabBound, interaction.GrabActive, interaction.ScaleBound, interaction.ScaleActive);
						Publish(snapshot);
						if (logGate.ShouldLogSubmittedSize(gpuVideoFrame.Width, gpuVideoFrame.Height))
						{
							IAndroidConnectionLogSink log3 = _log;
							DateTimeOffset utcNow3 = DateTimeOffset.UtcNow;
							string message2 = $"手机视频已提交 SteamVR；可见 {gpuVideoFrame.Width}x{gpuVideoFrame.Height}；解码缓冲 {decodedVideoFrame.Width}x{decodedVideoFrame.Height}；步幅 {decodedVideoFrame.Stride}";
							string deviceKey4 = session.DeviceKey;
							int? videoWidth = gpuVideoFrame.Width;
							int? videoHeight = gpuVideoFrame.Height;
							packetCount = metrics.SubmittedFrames;
							log3.TryWrite(new AndroidConnectionLogEntry(utcNow3, "video_overlay", "VIDEO_OVERLAY_RUNNING", message2, deviceKey4, null, null, null, null, null, null, null, null, videoWidth, videoHeight, packetCount));
						}
						if (logGate.ShouldLogQualitySample(sessionClock.Elapsed, TimeSpan.FromSeconds(5L)))
						{
							IAndroidConnectionLogSink log4 = _log;
							DateTimeOffset utcNow4 = DateTimeOffset.UtcNow;
							string message3 = $"请求 {options.ResolutionPercent}% / {options.VideoBitrateBitsPerSecond / 1000000} Mbps / {options.MaximumFramesPerSecond} FPS；实际平均 {metrics.AverageBitrateMbps:F2} Mbps，峰值 {metrics.PeakBitrateMbps:F2} Mbps，提交 {metrics.FramesPerSecond:F1} FPS，端到端平均延迟 " + ((metrics.AverageLatencyMilliseconds > 0.0) ? $"{metrics.AverageLatencyMilliseconds:F0} ms" : "不可用");
							string deviceKey5 = session.DeviceKey;
							int? videoHeight = gpuVideoFrame.Width;
							int? videoWidth = gpuVideoFrame.Height;
							packetCount = metrics.SubmittedFrames;
							long? payloadBytes = metrics.TotalEncodedBytes;
							log4.TryWrite(new AndroidConnectionLogEntry(utcNow4, "video_quality", "VIDEO_QUALITY_SAMPLE", message3, deviceKey5, null, null, null, null, null, null, null, null, videoHeight, videoWidth, packetCount, payloadBytes));
						}
						goto IL_0e65;
					}
					IL_0e65:
					started.TrySetResult();
				}
			}
			finally
			{
				await keyguardLifetime.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
				try
				{
					await keyguardTask.ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException) when (keyguardLifetime.IsCancellationRequested)
				{
				}
				if (overlay != null)
				{
					overlay.PhoneInputReceived -= OnPhoneInputReceived;
					lock (_gate)
					{
						if (_activeOverlay == overlay)
						{
							_activeOverlay = null;
						}
					}
					overlay.Dispose();
				}
				presenter?.Dispose();
				decoder?.Dispose();
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			started.TrySetCanceled(cancellationToken);
		}
		catch (Exception ex3)
		{
			PhoneOverlayServiceException ex4 = MapFailure(ex3);
			Publish(new PhoneOverlaySnapshot(PhoneOverlayState.Faulted, ex4.ReasonCode, ex4.Message, 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L));
			started.TrySetException(ex4);
			IAndroidConnectionLogSink log5 = _log;
			DateTimeOffset utcNow5 = DateTimeOffset.UtcNow;
			string reasonCode2 = ex4.ReasonCode;
			string message4 = ex4.Message;
			string diagnosticExceptionMessage = DescribeExceptionChain(ex3);
			string diagnosticExceptionType = ex3.ToString();
			log5.TryWrite(new AndroidConnectionLogEntry(utcNow5, "video_overlay", reasonCode2, message4, deviceKey, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, diagnosticExceptionType, diagnosticExceptionMessage));
		}
		finally
		{
			if (cancellationToken.IsCancellationRequested)
			{
				Publish(new PhoneOverlaySnapshot(PhoneOverlayState.Stopped, "VIDEO_OVERLAY_STOPPED", "手机浮窗已关闭", 0, 0, 0L, 0L, 0.0, SteamVrInputReady: false, WorldAnchored: false, ControllerHovered: false, OverlayGrabbed: false, UnlockKeypadExpanded: false, "OPENVR_INPUT_STOPPED", OpenVrBindingHealthState.Stopped, ShowBindingNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行", 100, 16, 60, 0.0, 0.0, 0.0, null, null, 0L));
				_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "video_overlay", "VIDEO_OVERLAY_STOPPED", "手机浮窗已关闭", deviceKey));
			}
		}
	}

	private void Publish(PhoneOverlaySnapshot snapshot)
	{
		bool flag;
		lock (_gate)
		{
			flag = _snapshot.State != snapshot.State || _snapshot.ReasonCode != snapshot.ReasonCode || _snapshot.Width != snapshot.Width || _snapshot.Height != snapshot.Height || _snapshot.SteamVrInputReady != snapshot.SteamVrInputReady || _snapshot.WorldAnchored != snapshot.WorldAnchored || _snapshot.ControllerHovered != snapshot.ControllerHovered || _snapshot.OverlayGrabbed != snapshot.OverlayGrabbed || _snapshot.UnlockKeypadExpanded != snapshot.UnlockKeypadExpanded || _snapshot.InputReasonCode != snapshot.InputReasonCode || _snapshot.BindingState != snapshot.BindingState || _snapshot.ShowBindingNotice != snapshot.ShowBindingNotice || _snapshot.BindingReasonCode != snapshot.BindingReasonCode || _snapshot.RequestedResolutionPercent != snapshot.RequestedResolutionPercent || _snapshot.RequestedBitrateMbps != snapshot.RequestedBitrateMbps || _snapshot.RequestedMaximumFramesPerSecond != snapshot.RequestedMaximumFramesPerSecond || snapshot.SubmittedFrames == 1 || snapshot.SubmittedFrames / 60 != _snapshot.SubmittedFrames / 60;
			_snapshot = snapshot;
			if (snapshot.SubmittedFrames > 0 || !string.IsNullOrWhiteSpace(snapshot.VideoDecoderBackend) || !string.IsNullOrWhiteSpace(snapshot.ControllerType))
			{
				_diagnosticSnapshot = snapshot;
			}
		}
		if (flag)
		{
			StateChanged?.Invoke(this, new PhoneOverlayChangedEventArgs(snapshot));
		}
	}

	private void OnPhoneInputReceived(OpenVrPhoneInputCommand command)
	{
		PhoneOverlaySnapshot snapshot = Snapshot;
		if (_phoneControl.Snapshot.State == PhoneControlState.Ready)
		{
			PhoneInputCommandKind kind = command.Kind;
			bool flag = (((uint)kind <= 3u || kind == PhoneInputCommandKind.Scroll) ? true : false);
			bool flag2 = flag;
			if (!flag2 || (snapshot.Width >= 1 && snapshot.Height >= 1))
			{
				_phoneControl.QueueFromSteamVr(command.Kind, command.NormalizedX, command.NormalizedY, flag2 ? snapshot.Width : 0, flag2 ? snapshot.Height : 0, command.ScrollDelta, command.UnlockDigit);
			}
		}
	}

	private static string DescribeExceptionChain(Exception exception)
	{
		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		for (Exception current = exception; current != null; current = current.InnerException)
		{
			if (builder.Length > 0)
			{
				builder.Append(" <- ");
			}
			builder.Append(current.GetType().FullName);
			if (!string.IsNullOrEmpty(current.Message))
			{
				builder.Append(": ").Append(current.Message.Replace('\r', ' ').Replace('\n', ' '));
			}
		}
		return builder.ToString();
	}

	private static PhoneOverlayServiceException MapFailure(Exception exception)
	{
		if (!(exception is PhoneOverlayServiceException result))
		{
			if (!(exception is AndroidConnectionException ex))
			{
				if (!(exception is MediaDecoderException ex2))
				{
					if (!(exception is GpuVideoPresenterException ex3))
					{
						if (exception is OpenVrOverlayException ex4)
						{
							return new PhoneOverlayServiceException(ex4.ReasonCode, ex4.Message, ex4);
						}
						return new PhoneOverlayServiceException("VIDEO_OVERLAY_UNEXPECTED_FAILURE", "手机浮窗遇到未预期错误", exception);
					}
					return new PhoneOverlayServiceException(ex3.ReasonCode, ex3.Message, ex3);
				}
				return new PhoneOverlayServiceException(ex2.ReasonCode, ex2.Message, ex2);
			}
			return new PhoneOverlayServiceException(ex.ReasonCode, ex.Message, ex);
		}
		return result;
	}
}
