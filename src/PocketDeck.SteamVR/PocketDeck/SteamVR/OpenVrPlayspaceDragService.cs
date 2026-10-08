using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Valve.VR;

namespace PocketDeck.SteamVR;

public sealed class OpenVrPlayspaceDragService(OpenVrControllerHand phoneControllerHand, float multiplier) : IOpenVrPlayspaceDragService, IDisposable
{
	private const string _leftDragPath = "/actions/main/in/LeftHandSpaceDrag";

	private const string _rightDragPath = "/actions/main/in/RightHandSpaceDrag";

	private const string _resetOffsetsPath = "/actions/main/in/ResetOffsets";

	private readonly object _stateGate = new object();

	private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(initialState: false);

	private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[64];

	private OpenVrPlayspaceDragSnapshot _snapshot = new OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState.Stopped, Enabled: false, ValidateMultiplier(multiplier), "PLAYSPACE_DRAG_NOT_STARTED", "独立空间拖拽服务未运行");

	private Thread? _worker;

	private int _phoneControllerHand = (int)phoneControllerHand;

	private int _handVersion;

	private float _multiplier = ValidateMultiplier(multiplier);

	private volatile bool _enabled;

	private volatile bool _disposed;

	public OpenVrPlayspaceDragSnapshot Snapshot
	{
		get
		{
			lock (_stateGate)
			{
				return _snapshot;
			}
		}
	}

	public event EventHandler<OpenVrPlayspaceDragChangedEventArgs>? StateChanged;

	public event EventHandler<OpenVrPlayspaceDragDiagnosticEventArgs>? DiagnosticRecorded;

	public void Start()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		lock (_stateGate)
		{
			Thread worker = _worker;
			if (worker == null || !worker.IsAlive)
			{
				_stopSignal.Reset();
				OpenVrRuntimeHost.ActionUpdates.SetPlayspaceEnabled(_enabled);
				OpenVrRuntimeHost.ActionUpdates.SetPlayspaceMultiplier(_multiplier);
				PublishLocked(new OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState.Starting, _enabled, Volatile.Read(in _multiplier), "PLAYSPACE_DRAG_STARTING", "正在连接 SteamVR 独立空间拖拽"));
				_worker = new Thread(Run)
				{
					IsBackground = true,
					Name = "VRPhoneScreen playspace drag"
				};
				_worker.Start();
			}
		}
	}

	public void StopService()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		StopWorker(resetStopSignal: true);
	}

	public void SetEnabled(bool enabled)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		lock (_stateGate)
		{
			_enabled = enabled;
			OpenVrRuntimeHost.ActionUpdates.SetPlayspaceEnabled(enabled);
			PublishDiagnostic("PLAYSPACE_DRAG_ENABLED_CHANGED", $"enabled={enabled}");
			Publish(Snapshot with
			{
				Enabled = enabled,
				ReasonCode = (enabled ? "PLAYSPACE_DRAG_ENABLED" : "PLAYSPACE_DRAG_DISABLED"),
				Message = (enabled ? "独立空间拖拽已开启" : "独立空间拖拽已关闭")
			});
		}
	}

	public void SetMultiplier(float multiplier)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		float num = ValidateMultiplier(multiplier);
		lock (_stateGate)
		{
			float num2 = Interlocked.Exchange(ref _multiplier, num);
			OpenVrRuntimeHost.ActionUpdates.SetPlayspaceMultiplier(num);
			PublishDiagnostic("PLAYSPACE_DRAG_MULTIPLIER_APPLIED", FormattableString.Invariant($"previous={num2:0}; applied={num:0}"));
			Publish(Snapshot with
			{
				Multiplier = num,
				ReasonCode = "PLAYSPACE_DRAG_MULTIPLIER_CHANGED",
				Message = $"空间拖拽倍率已设为 {num:0}x"
			});
		}
	}

	public void SetPhoneControllerHand(OpenVrControllerHand hand)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		Interlocked.Exchange(ref _phoneControllerHand, (int)hand);
		Interlocked.Increment(ref _handVersion);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			StopWorker(resetStopSignal: false);
			_stopSignal.Dispose();
		}
	}

	private void Run()
	{
		while (!_stopSignal.IsSet)
		{
			try
			{
				RunSession();
			}
			catch (Exception ex) when ((ex is InvalidOperationException || ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException || ex is System.IO.InvalidDataException || ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is FormatException || ex is System.Text.Json.JsonException) ? true : false)
			{
				// 任何「SteamVR 输入/绑定」准备失败都必须降级为「本功能不可用」，
				// 绝不能让异常逃出后台线程把整个进程带走（绑定数据损坏时会触发）。
				bool bindingInvalid = ex is System.IO.InvalidDataException;
				string reason = bindingInvalid ? "PLAYSPACE_DRAG_BINDING_INVALID" : "PLAYSPACE_DRAG_STEAMVR_UNAVAILABLE";
				string message = bindingInvalid
					? ("SteamVR 手柄绑定数据无效，已跳过空间拖拽：" + ex.Message)
					: "SteamVR 独立空间拖拽暂不可用，正在重试";
				Publish(new OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState.Faulted, _enabled, Volatile.Read(in _multiplier), reason, message));
				_stopSignal.Wait(TimeSpan.FromSeconds(3L));
			}
		}
	}

	private void RunSession()
	{
		ulong pHandle = 0uL;
		ulong pHandle2 = 0uL;
		ulong pHandle3 = 0uL;
		CVRSystem orStart;
		ulong actionSet;
		lock (OpenVrRuntimeHost.ApiGate)
		{
			EVRInitError initializationError = EVRInitError.None;
			orStart = OpenVrRuntimeHost.GetOrStart(ref initializationError);
			if (initializationError != EVRInitError.None)
			{
				throw new InvalidOperationException($"SteamVR initialization failed: {initializationError}");
			}
			EVRInputError eVRInputError = OpenVrRuntimeHost.EnsureActionManifestSubmitted();
			if ((eVRInputError != EVRInputError.None && eVRInputError != EVRInputError.IPCError) || 1 == 0)
			{
				throw new InvalidOperationException($"SteamVR action manifest failed: {eVRInputError}");
			}
			actionSet = OpenVrInputManifest.GetActionSet();
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/LeftHandSpaceDrag", ref pHandle));
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/RightHandSpaceDrag", ref pHandle2));
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/ResetOffsets", ref pHandle3));
		}
		OpenVrPlayspaceDragController openVrPlayspaceDragController = null;
		try
		{
			int num = -1;
			ulong pHandle4 = 0uL;
			VRActiveActionSet_t[] array = new VRActiveActionSet_t[1];
			OpenVrDashboardInputGate openVrDashboardInputGate = new OpenVrDashboardInputGate();
			long num2 = -1L;
			Publish(new OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState.Ready, _enabled, Volatile.Read(in _multiplier), "PLAYSPACE_DRAG_READY", "SteamVR 独立空间拖拽已就绪"));
			PublishDiagnostic("PLAYSPACE_DRAG_SESSION_READY", FormattableString.Invariant($"multiplier={Volatile.Read(in _multiplier):0}; enabled={_enabled}"));
			bool flag = false;
			int num3 = 0;
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			float num8 = 0f;
			float num9 = 0f;
			float num10 = 0f;
			float num11 = 0f;
			float num12 = 0f;
			float num13 = 0f;
			float num14 = 0f;
			float num15 = 0f;
			float num16 = 0f;
			float num17 = 0f;
			long num18 = 0L;
			while (!_stopSignal.IsSet)
			{
				lock (_stateGate)
				{
					OpenVrPlayspaceControlRequest openVrPlayspaceControlRequest = OpenVrRuntimeHost.ActionUpdates.TakeControls();
					if ((object)openVrPlayspaceControlRequest != null)
					{
						if (openVrPlayspaceControlRequest.Enabled != _enabled)
						{
							SetEnabled(openVrPlayspaceControlRequest.Enabled);
						}
						if (openVrPlayspaceControlRequest.Multiplier != Volatile.Read(in _multiplier))
						{
							SetMultiplier(openVrPlayspaceControlRequest.Multiplier);
						}
					}
				}
				lock (OpenVrRuntimeHost.ApiGate)
				{
					int num19 = Volatile.Read(in _handVersion);
					if (openVrPlayspaceDragController == null || num19 != num)
					{
						openVrPlayspaceDragController?.Restore();
						OpenVrControllerHand hand = (OpenVrControllerHand)Volatile.Read(in _phoneControllerHand);
						OpenVrControllerHand hand2 = OpenVrControllerHandRouting.Opposite(hand);
						string pchInputSourcePath = OpenVrControllerHandRouting.InputSourcePath(hand2);
						pHandle4 = 0uL;
						Check(OpenVR.Input.GetInputSourceHandle(pchInputSourcePath, ref pHandle4));
						openVrPlayspaceDragController = new OpenVrPlayspaceDragController(orStart, OpenVR.ChaperoneSetup, hand2);
						PublishCoordinates(0f, 0f, 0f);
						num15 = 0f;
						num16 = 0f;
						num17 = 0f;
						array[0].ulActionSet = actionSet;
						array[0].ulRestrictedToDevice = pHandle4;
						num = num19;
					}
					OpenVrSharedPlayspaceInput openVrSharedPlayspaceInput = OpenVrRuntimeHost.ActionUpdates.Read();
					bool flag2 = OpenVR.Overlay.IsDashboardVisible();
					checked
					{
						bool sampleIsFresh;
						OpenVrPlayspaceInputSample sample;
						if (openVrSharedPlayspaceInput.PhonePollerActive)
						{
							sample = openVrSharedPlayspaceInput.InputSample;
							sampleIsFresh = openVrSharedPlayspaceInput.InputRevision != num2;
							num2 = openVrSharedPlayspaceInput.InputRevision;
						}
						else
						{
							array[0].nPriority = ((_enabled && !flag2) ? 16777216 : 0);
							CVRInput cVRInput = OpenVR.Input ?? throw new InvalidOperationException("SteamVR input interface became unavailable.");
							Check(cVRInput.UpdateActionState(array, (uint)Marshal.SizeOf<VRActiveActionSet_t>()));
							sample = OpenVrPlayspaceInputSample.Read(cVRInput, pHandle, pHandle2, pHandle3, pHandle4, flag2);
							sample = sample with
							{
								DashboardVisible = (flag2 || OpenVR.Overlay.IsDashboardVisible())
							};
							sampleIsFresh = true;
						}
						openVrDashboardInputGate.Apply(flag2, sample, sampleIsFresh, out var drag, out var reset);
						drag &= _enabled;
						float num20 = Volatile.Read(in _multiplier);
						if (drag && !flag)
						{
							num3 = 0;
							num4 = 0f;
							num5 = 0f;
							num6 = 0f;
							num7 = 0f;
							num8 = 0f;
							num9 = 0f;
							num10 = 0f;
							num11 = 0f;
							num12 = 0f;
							num13 = 0f;
							num14 = 0f;
							PublishDiagnostic("PLAYSPACE_DRAG_GESTURE_STARTED", FormattableString.Invariant($"multiplier={num20:0}"));
						}
						orStart.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, _poses);
						OpenVrPlayspaceDragUpdate openVrPlayspaceDragUpdate = openVrPlayspaceDragController.Update(drag, reset, num20, _poses);
						float offsetX = openVrPlayspaceDragController.OffsetX;
						float offsetY = openVrPlayspaceDragController.OffsetY;
						float offsetZ = openVrPlayspaceDragController.OffsetZ;
						if (openVrPlayspaceDragUpdate.OffsetApplied)
						{
							num3++;
							num4 += openVrPlayspaceDragUpdate.SourceDistance;
							num5 += openVrPlayspaceDragUpdate.AppliedDistance;
							num6 = openVrPlayspaceDragUpdate.OffsetX;
							num7 = openVrPlayspaceDragUpdate.OffsetY;
							num8 = openVrPlayspaceDragUpdate.OffsetZ;
							num9 += openVrPlayspaceDragUpdate.SourceDeltaX;
							num10 += openVrPlayspaceDragUpdate.SourceDeltaY;
							num11 += openVrPlayspaceDragUpdate.SourceDeltaZ;
							num12 += openVrPlayspaceDragUpdate.AppliedDeltaX;
							num13 += openVrPlayspaceDragUpdate.AppliedDeltaY;
							num14 += openVrPlayspaceDragUpdate.AppliedDeltaZ;
						}
						long tickCount = Environment.TickCount64;
						bool flag3 = offsetX == 0f && offsetY == 0f && offsetZ == 0f && (num15 != 0f || num16 != 0f || num17 != 0f);
						bool flag4 = !drag & flag;
						if ((flag3 | flag4) || (openVrPlayspaceDragUpdate.OffsetApplied && tickCount >= num18))
						{
							PublishCoordinates(offsetX, offsetY, offsetZ);
							num15 = offsetX;
							num16 = offsetY;
							num17 = offsetZ;
							num18 = tickCount + 100;
						}
						if (flag4)
						{
							CultureInfo invariantCulture = CultureInfo.InvariantCulture;
							InlineArray13<object> buffer = default;
							buffer[0] = num20;
							buffer[1] = num3;
							buffer[2] = num4;
							buffer[3] = num5;
							buffer[4] = num9;
							buffer[5] = num10;
							buffer[6] = num11;
							buffer[7] = num12;
							buffer[8] = num13;
							buffer[9] = num14;
							buffer[10] = num6;
							buffer[11] = num7;
							buffer[12] = num8;
							PublishDiagnostic("PLAYSPACE_DRAG_GESTURE_COMPLETED", string.Format((IFormatProvider?)invariantCulture, "multiplier={0:0}; frames={1}; sourcePathMeters={2:0.0000}; appliedPathMeters={3:0.0000}; sourceDeltaMeters={4:0.0000},{5:0.0000},{6:0.0000}; appliedDeltaMeters={7:0.0000},{8:0.0000},{9:0.0000}; offsetMeters={10:0.0000},{11:0.0000},{12:0.0000}", (ReadOnlySpan<object?>)buffer));
						}
						flag = drag;
					}
				}
				_stopSignal.Wait(TimeSpan.FromMilliseconds(4L));
			}
		}
		finally
		{
			lock (OpenVrRuntimeHost.ApiGate)
			{
				openVrPlayspaceDragController?.Restore();
			}
			PublishCoordinates(0f, 0f, 0f);
		}
	}

	private void StopWorker(bool resetStopSignal)
	{
		_stopSignal.Set();
		Thread worker;
		lock (_stateGate)
		{
			worker = _worker;
		}
		if (worker != null && worker.IsAlive && !worker.Join(TimeSpan.FromSeconds(5L)))
		{
			throw new InvalidOperationException("SteamVR playspace drag worker did not stop within five seconds.");
		}
		lock (_stateGate)
		{
			if (_worker == worker)
			{
				_worker = null;
			}
		}
		Publish(new OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState.Stopped, _enabled, Volatile.Read(in _multiplier), "PLAYSPACE_DRAG_STOPPED", "独立空间拖拽服务已停止并恢复空间"));
		if (resetStopSignal)
		{
			_stopSignal.Reset();
		}
	}

	private static float ValidateMultiplier(float multiplier)
	{
		if (!float.IsFinite(multiplier) || multiplier <= 0f || multiplier > 40f)
		{
			throw new ArgumentOutOfRangeException("multiplier");
		}
		return multiplier;
	}

	private static void Check(EVRInputError error)
	{
		if (error != EVRInputError.None)
		{
			throw new InvalidOperationException($"SteamVR input failed: {error}");
		}
	}

	private void Publish(OpenVrPlayspaceDragSnapshot snapshot)
	{
		lock (_stateGate)
		{
			PublishLocked(snapshot);
		}
	}

	private void PublishLocked(OpenVrPlayspaceDragSnapshot snapshot)
	{
		snapshot = snapshot with
		{
			Enabled = _enabled,
			Multiplier = _multiplier
		};
		_snapshot = snapshot;
		StateChanged?.Invoke(this, new OpenVrPlayspaceDragChangedEventArgs(snapshot));
	}

	private void PublishCoordinates(float x, float y, float z)
	{
		Publish(Snapshot with
		{
			OffsetX = x,
			OffsetY = y,
			OffsetZ = z
		});
	}

	private void PublishDiagnostic(string reasonCode, string message)
	{
		DiagnosticRecorded?.Invoke(this, new OpenVrPlayspaceDragDiagnosticEventArgs(new OpenVrPlayspaceDragDiagnostic(reasonCode, message)));
	}
}
