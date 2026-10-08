using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PocketDeck.Contracts;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPhoneInteraction : IDisposable
{
	private const string _leftSpaceDragPath = "/actions/main/in/LeftHandSpaceDrag";

	private const string _rightSpaceDragPath = "/actions/main/in/RightHandSpaceDrag";

	private const string _resetOffsetsPath = "/actions/main/in/ResetOffsets";

	private const string _phoneBackPath = "/actions/main/in/PhoneBack";

	private const string _phoneHomePath = "/actions/main/in/PhoneHome";

	private const string _phoneRecentsPath = "/actions/main/in/PhoneRecents";

	private const string _phoneControlPanelPath = "/actions/main/in/PhoneControlPanel";

	private const string _phoneScreenshotPath = "/actions/main/in/PhoneScreenshot";

	private const string _phoneGrabPath = "/actions/main/in/PhoneOverlayGrab";

	private const string _phoneScalePath = "/actions/main/in/PhoneOverlayScale";

	private const string _phoneTouchPath = "/actions/main/in/PhoneOverlayTouch";

	private const string _phonePointerPosePath = "/actions/pointer/in/PhonePointerPose";

	private const string _menuDismissPath = "/actions/pointer/in/MenuDismiss";

	private const string _phoneButtonStatePath = "/actions/phonebuttonstate/in/AnyPhoneInputPressed";

	private const int _pollIntervalMilliseconds = 8;

	private const int _maximumCommandsPerPoll = 8;

	private readonly object _openVrGate;

	private readonly OpenVrControllerHand _controllerHand;

	private readonly string _controllerInputPath;

	private readonly string _playspaceInputPath;

	private readonly object _stateGate = new object();

	private readonly OpenVrBindingHealthMonitor _bindingHealth = new OpenVrBindingHealthMonitor();

	private readonly OpenVrPhoneActionSetGate _phoneActionSetGate = new OpenVrPhoneActionSetGate();

	private readonly OpenVrGrabAwakePolicy _grabAwakePolicy = new OpenVrGrabAwakePolicy();

	private readonly OpenVrPointerPoseResolver _pointerPoseResolver = new OpenVrPointerPoseResolver();

	private readonly OpenVrPointerSmoother _pointerSmoother = new OpenVrPointerSmoother();

	private readonly OpenVrPointerOverlayRenderer _pointerRenderer;

	private readonly OpenVrPhoneBackface _backface;

	private readonly OpenVrPhoneMenuState _menu = new OpenVrPhoneMenuState();

	private readonly OpenVrPhoneMenuView _menuView;

	private bool _presentationStopped;

	private volatile bool _phonePresented;

	private int _appliedInputRevision = -1;

	private readonly OpenVrPlacementStore _placementStore;

	private readonly OpenVrPhoneTouchState _touchState = new OpenVrPhoneTouchState();

	private readonly CVRSystem _system;

	private readonly CVROverlay _overlay;

	private readonly ulong _overlayHandle;

	private readonly float _initialDistanceMeters;

	private readonly Action _submitVideoTexture;

	private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(initialState: false);

	private readonly Thread _worker;

	private readonly SmoothScrollGesture _smoothScroll = new SmoothScrollGesture();

	private readonly VRActiveActionSet_t[] _activeActionSets = new VRActiveActionSet_t[6];

	private bool _inputOverridesAvailable;

	private readonly TrackedDevicePose_t[] _trackedPoses = new TrackedDevicePose_t[64];

	private HmdMatrix34_t _overlayTransform;

	private HmdMatrix34_t _grabRelativeTransform;

	private OpenVrPhoneSurface _grabSurface;

	private bool _grabHidden;

	private ulong _actionSet;

	private ulong _pointerActionSet;

	private ulong _phoneButtonStateActionSet;

	private ulong _leftSpaceDragAction;

	private ulong _rightSpaceDragAction;

	private ulong _resetOffsetsAction;

	private ulong _phoneBackAction;

	private ulong _phoneHomeAction;

	private ulong _phoneRecentsAction;

	private ulong _phoneControlPanelAction;

	private ulong _phoneScreenshotAction;

	private ulong _phoneGrabAction;

	private ulong _phoneScaleAction;

	private ulong _phoneTouchAction;

	private ulong _phonePointerPoseAction;

	private ulong _menuDismissAction;

	private ulong _menuRecallAction;

	private volatile int _phoneLockState;

	private HmdMatrix34_t _recalledHead;

	private ulong _phoneButtonStateAction;

	private ulong _controllerInputSource;

	private ulong _playspaceInputSource;

	private IDisposable? _actionUpdateLease;

	private bool _previousBack;

	private bool _previousHome;

	private bool _previousRecents;

	private bool _previousControlPanel;

	private bool _previousScreenshot;

	private bool _previousGrab;

	private bool _phoneActionsEnabled;

	private bool _grabbed;

	private bool _worldAnchored;

	private bool _bindingHealthStarted;

	private float _baseWidthMeters = 0.65f;

	private float _appliedWidthMeters = float.NaN;

	private float _frameAspectRatio = 1f;

	private float _scaleFactor = 1f;

	private float _grabUvX = 0.5f;

	private float _grabUvY = 0.5f;

	private long _lastGrabTimestamp;

	private volatile bool _keepAwakeWhileGrabbed;

	private volatile bool _unlockKeypadEnabled;

	private volatile bool _frameReady;

	private string? _headsetModel;

	private string? _controllerType;

	private uint _controllerDeviceIndex = uint.MaxValue;

	private bool _pointerPoseBound;

	private bool _pointerPoseActive;

	private bool _touchBound;

	private bool _touchActive;

	private bool _grabBound;

	private bool _grabActive;

	private bool _scaleBound;

	private bool _scaleActive;

	private volatile bool _stopping;

	private bool _disposed;

	private HmdMatrix34_t GrabRoot
	{
		get
		{
			if (!_grabHidden)
			{
				return _overlayTransform;
			}
			return _recalledHead;
		}
	}

	public OpenVrPhoneInteractionSnapshot Snapshot { get; private set; }

	public OpenVrBindingHealthSnapshot BindingHealth => _bindingHealth.Snapshot;

	public bool PhonePresented => _phonePresented;

	private float CurrentWidthMeters => _baseWidthMeters * _scaleFactor;

	public event OpenVrPhoneInputSink? PhoneInputReceived;

	public OpenVrPhoneInteraction(object openVrGate, CVRSystem system, CVROverlay overlay, ulong overlayHandle, float initialDistanceMeters, Action submitVideoTexture)
	{
		_openVrGate = openVrGate;
		_system = system;
		_overlay = overlay;
		_overlayHandle = overlayHandle;
		_initialDistanceMeters = initialDistanceMeters;
		_submitVideoTexture = submitVideoTexture;
		_controllerHand = OpenVrControllerPreferences.Load();
		_controllerInputPath = OpenVrControllerHandRouting.InputSourcePath(_controllerHand);
		OpenVrControllerHand hand = OpenVrControllerHandRouting.Opposite(_controllerHand);
		_playspaceInputPath = OpenVrControllerHandRouting.InputSourcePath(hand);
		_pointerRenderer = new OpenVrPointerOverlayRenderer(overlay);
		_backface = new OpenVrPhoneBackface(overlay);
		_menuView = new OpenVrPhoneMenuView(overlay);
		using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2L)))
		{
			_scaleFactor = OpenVrPlacementStore.LoadAsync(OpenVrPlacementStore.DefaultPath, cancellationTokenSource.Token).GetAwaiter().GetResult()?.Scale ?? 1f;
		}
		Snapshot = new OpenVrPhoneInteractionSnapshot(InputReady: false, WorldAnchored: false, Hovered: false, Grabbed: false, "OPENVR_INPUT_STARTING", "正在准备 SteamVR 手柄输入");
		SetInitialTransform();
		_placementStore = new OpenVrPlacementStore(OpenVrPlacementStore.DefaultPath);
		_worker = new Thread(Run)
		{
			IsBackground = true,
			Name = "VRPhoneScreen SteamVR input"
		};
		_worker.Start();
	}

	public void ConfigureLockScreenFeatures(bool keepAwakeWhileGrabbed, bool unlockKeypadEnabled)
	{
		_keepAwakeWhileGrabbed = keepAwakeWhileGrabbed;
		_unlockKeypadEnabled = unlockKeypadEnabled;
	}

	public OpenVrBindingResult OpenBindingUi()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		OpenVrPhoneInteractionSnapshot snapshot = GetSnapshot();
		if (!snapshot.InputReady || _actionSet == 0L)
		{
			return new OpenVrBindingResult(Succeeded: false, snapshot.ReasonCode, snapshot.Message);
		}
		lock (_openVrGate)
		{
			return OpenVrInputManifest.OpenBindingUi(_actionSet);
		}
	}

	public void SetPhoneLocked(bool? locked)
	{
		int phoneLockState = (locked.HasValue ? ((locked != true) ? 1 : 2) : 0);
		_phoneLockState = phoneLockState;
	}

	public void UpdateFrameGeometry(float baseWidthMeters, int frameWidth, int frameHeight)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseWidthMeters, 0f, "baseWidthMeters");
		ArgumentOutOfRangeException.ThrowIfLessThan(frameWidth, 1, "frameWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(frameHeight, 1, "frameHeight");
		_baseWidthMeters = baseWidthMeters;
		_frameAspectRatio = (float)frameWidth / (float)frameHeight;
		_frameReady = true;
		ApplyOverlayWidth();
	}

	public void PrepareVideoSubmission()
	{
		_system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, _trackedPoses);
		EnsureWorldAnchored();
		RenderPresentation(OpenVrRuntimeHost.ActionUpdates.Read());
		if (!_menu.ControlsVisible)
		{
			_menuView.Hide();
			_backface.Update(visible: false, _overlayTransform, CurrentWidthMeters, _frameAspectRatio);
			_pointerRenderer.Hide();
		}
	}

	public void StopPresentation()
	{
		_presentationStopped = true;
		_menu.SetEnvironment(ready: false, _menu.DashboardVisible, _unlockKeypadEnabled);
		_overlay.HideOverlay(_overlayHandle);
		_phonePresented = false;
		_menuView.Hide();
		_backface.Update(visible: false, _overlayTransform, CurrentWidthMeters, _frameAspectRatio);
		_pointerRenderer.Hide();
	}

	private void UpdatePresentationEnvironment()
	{
		bool bPoseIsValid = _trackedPoses[0].bPoseIsValid;
		_menu.SetEnvironment(((_frameReady && _worldAnchored) & bPoseIsValid) && !_presentationStopped, _overlay.IsDashboardVisible(), _unlockKeypadEnabled);
		OpenVrPhoneMenuState menu = _menu;
		menu.SetLocked(_phoneLockState switch
		{
			2 => (bool?)true, 
			1 => false, 
			_ => null, 
		});
	}

	private void SynchronizePhoneVisibility()
	{
		if (_phonePresented != _menu.PhoneVisible)
		{
			Check(_menu.PhoneVisible ? _overlay.ShowOverlay(_overlayHandle) : _overlay.HideOverlay(_overlayHandle), "同步手机浮窗显示状态");
			_phonePresented = _menu.PhoneVisible;
			if (_phonePresented)
			{
				_submitVideoTexture();
			}
		}
	}

	private void RenderPresentation(OpenVrSharedPlayspaceInput playspace)
	{
		UpdatePresentationEnvironment();
		if (!_phonePresented && _menu.PhoneVisible)
		{
			_overlayTransform = OpenVrPhonePlacement.InFrontOf(_trackedPoses[0].mDeviceToAbsoluteTracking, _initialDistanceMeters);
			Check(_overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _overlayTransform), "将手机浮窗放到当前头显前方");
		}
		_menuView.Render(_menu, _overlayTransform, CurrentWidthMeters, _frameAspectRatio, _menu.PhoneHidden ? _recalledHead : _trackedPoses[0].mDeviceToAbsoluteTracking, playspace);
		SynchronizePhoneVisibility();
		_backface.Update(_phonePresented, _overlayTransform, CurrentWidthMeters, _frameAspectRatio);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_stopping = true;
			_stopSignal.Set();
			bool flag = _worker.Join(TimeSpan.FromSeconds(2L));
			if (!flag)
			{
				Publish(Snapshot with
				{
					InputReady = false,
					ReasonCode = "OPENVR_INPUT_STOP_TIMEOUT",
					Message = "SteamVR 手柄输入线程未在期限内停止"
				});
			}
			if (_touchState.TryCancel(out var command))
			{
				PhoneInputReceived?.Invoke(command);
			}
			CancelSmoothScroll();
			lock (_openVrGate)
			{
				_pointerRenderer.Dispose();
				_backface.Dispose();
				_menuView.Dispose();
				QueuePlacement();
			}
			_placementStore.Dispose();
			Publish(OpenVrPhoneInteractionSnapshot.Stopped);
			if (flag)
			{
				_stopSignal.Dispose();
			}
		}
	}

	private void Run()
	{
		while (!_stopping)
		{
			bool flag = false;
			try
			{
				InitializeInput();
				flag = true;
				while (!_stopping && PollInput() && !_stopSignal.Wait(8))
				{
				}
			}
			catch (Exception ex)
			{
				ReleaseActionUpdateLease();
				long tickCount = Environment.TickCount64;
				_bindingHealth.Advance(tickCount);
				if (!flag)
				{
					_bindingHealth.RecordInitializationFailure(tickCount);
				}
				CancelPointer();
				CancelSmoothScroll();
				_phoneActionSetGate.Reset();
				_phoneActionsEnabled = false;
				_grabbed = false;
				lock (_openVrGate)
				{
					_pointerRenderer.Hide();
					_menu.ResetInput();
				}
				Publish(new OpenVrPhoneInteractionSnapshot(InputReady: false, _worldAnchored, Hovered: false, Grabbed: false, "OPENVR_INPUT_RECONNECTING", "SteamVR 手柄输入暂时不可用，正在重试", ControllerPoseValid: false, UnlockKeypadExpanded: false, ex.GetType().FullName, ex.Message));
				if (_stopSignal.Wait(TimeSpan.FromSeconds(1L)))
				{
					break;
				}
			}
		}
		ReleaseActionUpdateLease();
	}

	private void CancelPointer()
	{
		if (_touchState.TryCancel(out var command))
		{
			PhoneInputReceived?.Invoke(command);
		}
	}

	private void ReleaseActionUpdateLease()
	{
		Interlocked.Exchange(ref _actionUpdateLease, null)?.Dispose();
	}

	private void InitializeInput()
	{
		lock (_openVrGate)
		{
			if (!_bindingHealthStarted)
			{
				_bindingHealth.Begin(Environment.TickCount64);
				_bindingHealthStarted = true;
			}
			EVRInputError eVRInputError = OpenVrRuntimeHost.EnsureActionManifestSubmitted();
			if (eVRInputError != EVRInputError.IPCError)
			{
				Check(eVRInputError, "动作清单");
			}
			_actionSet = OpenVrInputManifest.GetActionSet();
			_pointerActionSet = OpenVrInputManifest.GetPointerActionSet();
			_phoneButtonStateActionSet = OpenVrInputManifest.GetPhoneButtonStateActionSet();
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/LeftHandSpaceDrag", ref _leftSpaceDragAction), "左手空间拖拽");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/RightHandSpaceDrag", ref _rightSpaceDragAction), "右手空间拖拽");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/ResetOffsets", ref _resetOffsetsAction), "重置空间偏移");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneBack", ref _phoneBackAction), "手机返回");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneHome", ref _phoneHomeAction), "手机桌面");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneRecents", ref _phoneRecentsAction), "最近任务");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneControlPanel", ref _phoneControlPanelAction), "控制栏");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneScreenshot", ref _phoneScreenshotAction), "截屏");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneOverlayGrab", ref _phoneGrabAction), "抓取浮窗");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneOverlayScale", ref _phoneScaleAction), "调整远近");
			Check(OpenVR.Input.GetActionHandle("/actions/main/in/PhoneOverlayTouch", ref _phoneTouchAction), "触控手机");
			Check(OpenVR.Input.GetActionHandle("/actions/pointer/in/PhonePointerPose", ref _phonePointerPoseAction), "手机指针姿态");
			Check(OpenVR.Input.GetActionHandle("/actions/pointer/in/MenuDismiss", ref _menuDismissAction), "菜单外点击");
			Check(OpenVR.Input.GetActionHandle("/actions/phonerecall/in/RecallMenu", ref _menuRecallAction), "截图键唤回菜单");
			Check(OpenVR.Input.GetActionHandle("/actions/phonebuttonstate/in/AnyPhoneInputPressed", ref _phoneButtonStateAction), "手机输入原始状态");
			Check(OpenVR.Input.GetInputSourceHandle(_controllerInputPath, ref _controllerInputSource), "手机操作手输入源");
			Check(OpenVR.Input.GetInputSourceHandle(_playspaceInputPath, ref _playspaceInputSource), "空间拖拽手输入源");
			_activeActionSets[0].ulActionSet = _actionSet;
			_activeActionSets[0].ulRestrictedToDevice = _playspaceInputSource;
			_activeActionSets[1].ulActionSet = _pointerActionSet;
			_activeActionSets[1].ulRestrictedToDevice = _controllerInputSource;
			_activeActionSets[2].ulActionSet = _actionSet;
			_activeActionSets[2].ulRestrictedToDevice = _controllerInputSource;
			_activeActionSets[3].ulActionSet = _phoneButtonStateActionSet;
			_activeActionSets[3].ulRestrictedToDevice = _controllerInputSource;
			ulong pHandle = 0uL;
			Check(OpenVR.Input.GetActionSetHandle("/actions/phonecapture", ref pHandle), "手机输入阻断动作集");
			_activeActionSets[4].ulActionSet = pHandle;
			_activeActionSets[4].ulRestrictedToDevice = _controllerInputSource;
			ulong pHandle2 = 0uL;
			Check(OpenVR.Input.GetActionSetHandle("/actions/phonerecall", ref pHandle2), "隐藏时唤回动作集");
			_activeActionSets[5].ulActionSet = pHandle2;
			_activeActionSets[5].ulRestrictedToDevice = _controllerInputSource;
			EVRSettingsError peError = EVRSettingsError.None;
			OpenVR.Settings.SetBool("steamvr", "globalActionSetPriority", bValue: true, ref peError);
			_inputOverridesAvailable = peError == EVRSettingsError.None && OpenVR.Settings.GetBool("steamvr", "globalActionSetPriority", ref peError) && peError == EVRSettingsError.None;
			if (_actionUpdateLease == null)
			{
				_actionUpdateLease = OpenVrRuntimeHost.ActionUpdates.AcquirePhonePoller();
			}
		}
		Publish(new OpenVrPhoneInteractionSnapshot(InputReady: true, _worldAnchored, Hovered: false, Grabbed: false, "OPENVR_INPUT_READY", "SteamVR 手柄输入已就绪"));
	}

	private bool PollInput()
	{
		Span<OpenVrPhoneInputCommand> commands = stackalloc OpenVrPhoneInputCommand[8];
		int commandCount = 0;
		checked
		{
			lock (_openVrGate)
			{
				EnsureWorldAnchored();
				OpenVrPhoneInteractionSnapshot snapshot = GetSnapshot();
				bool flag = TryGetControllerPose(out var pose);
				OpenVrSharedPlayspaceInput playspace = OpenVrRuntimeHost.ActionUpdates.Read();
				RenderPresentation(playspace);
				if (!_menu.ControlsVisible || _appliedInputRevision != _menu.InputRevision || (_grabbed && !_menu.IsSurfaceVisible(_grabSurface)))
				{
					_appliedInputRevision = _menu.InputRevision;
					_phoneActionsEnabled = false;
					_phoneActionSetGate.Reset();
					if (_grabbed)
					{
						QueuePlacement();
					}
					_grabbed = false;
					_pointerRenderer.Hide();
					StopSmoothScroll(commands, ref commandCount);
					if (_touchState.TryCancel(out var command))
					{
						commands[commandCount++] = command;
					}
				}
				bool dashboardVisible = _menu.DashboardVisible;
				_activeActionSets[0].nPriority = ((playspace.PlayspaceEnabled && !dashboardVisible) ? 16777216 : 0);
				_activeActionSets[0].ulRestrictedToDevice = _playspaceInputSource;
				_activeActionSets[1].nPriority = 1;
				_activeActionSets[1].ulRestrictedToDevice = _controllerInputSource;
				_activeActionSets[2].nPriority = (_phoneActionsEnabled ? 16777218 : 0);
				_activeActionSets[2].ulRestrictedToDevice = _controllerInputSource;
				_activeActionSets[3].nPriority = ((!_phoneActionsEnabled) ? ((!snapshot.Hovered || !_menu.ControlsVisible) ? 1 : 16777217) : 0);
				_activeActionSets[3].ulRestrictedToDevice = _controllerInputSource;
				_activeActionSets[4].nPriority = CapturePriority(_inputOverridesAvailable && _menu.ControlsVisible, flag, snapshot.Hovered, _grabbed);
				_activeActionSets[5].nPriority = ((_menu.PhoneHidden && !_menu.DashboardVisible) ? 16777218 : 0);
				Check(OpenVR.Input.UpdateActionState(_activeActionSets, (uint)Marshal.SizeOf<VRActiveActionSet_t>()), "更新动作状态");
				if (_menu.ProcessRecall(ReadDigital(_menuRecallAction)))
				{
					if (_grabbed)
					{
						QueuePlacement();
					}
					_grabbed = false;
					_phoneActionsEnabled = false;
					_phoneActionSetGate.Reset();
					_recalledHead = _trackedPoses[0].mDeviceToAbsoluteTracking;
					RenderPresentation(playspace);
				}
				bool rawStateActive = !_phoneActionsEnabled && IsDigitalActionActive(_phoneButtonStateAction);
				bool rawPressed = !_phoneActionsEnabled && ReadDigital(_phoneButtonStateAction);
				OpenVrPlayspaceInputSample openVrPlayspaceInputSample = OpenVrPlayspaceInputSample.Read(OpenVR.Input, _leftSpaceDragAction, _rightSpaceDragAction, _resetOffsetsAction, _playspaceInputSource, dashboardVisible);
				openVrPlayspaceInputSample = openVrPlayspaceInputSample with
				{
					DashboardVisible = (dashboardVisible || _overlay.IsDashboardVisible())
				};
				OpenVrRuntimeHost.ActionUpdates.PublishPlayspaceInput(openVrPlayspaceInputSample);
				long tickCount = Environment.TickCount64;
				if (DrainBindingEvents())
				{
					_bindingHealth.ObserveBindingLoadFailed(tickCount);
				}
				_bindingHealth.Advance(tickCount);
				InputPoseActionData_t inputPoseActionData_t = ReadPose(_phonePointerPoseAction);
				if (_bindingHealth.ShouldProbe(tickCount) && !_menu.DashboardVisible)
				{
					_pointerPoseBound = HasBinding(_phonePointerPoseAction);
					_pointerPoseActive = inputPoseActionData_t.bActive;
					_touchBound = HasBinding(_phoneTouchAction);
					_touchActive = IsDigitalActionActive(_phoneTouchAction);
					_grabBound = HasBinding(_phoneGrabAction);
					_grabActive = IsDigitalActionActive(_phoneGrabAction);
					_scaleBound = HasBinding(_phoneScaleAction);
					_scaleActive = IsAnalogActionActive(_phoneScaleAction);
					// 健康判定只看"绑定是否就位"：指针姿势可用 + 触控/握把动作已绑定。
					// 绝不能把 _touchActive/_grabActive（用户此刻是否正按着/抓着）算进来，
					// 否则只要用户 3 秒没动作就会被误判成"绑定加载失败、射线和控制不可用"。
					bool bindingsReady = _pointerPoseBound && _pointerPoseActive && _touchBound && _grabBound;
					_bindingHealth.RecordProbe(tickCount, flag, bindingsReady);
				}
				OpenVrBindingHealthSnapshot snapshot2 = _bindingHealth.Snapshot;
				bool actionPoseValid = inputPoseActionData_t.bActive && inputPoseActionData_t.pose.bPoseIsValid && inputPoseActionData_t.pose.bDeviceIsConnected;
				bool flag2 = _pointerPoseResolver.TryResolve(actionPoseValid, inputPoseActionData_t.pose.mDeviceToAbsoluteTracking, flag, pose, out var pointerPose);
				VROverlayIntersectionResults_t hit = default;
				bool flag3 = flag2 && _menu.PhoneVisible && TryIntersect(pointerPose, out hit);
				InputAnalogActionData_t inputAnalogActionData_t = ReadAnalog(_phoneScaleAction);
				bool flag4 = ReadDigital(_phoneGrabAction);
				if (OpenVrInteractionGate.ShouldEndGrab(flag4, _grabbed, flag))
				{
					_grabbed = false;
					QueuePlacement();
				}
				VROverlayIntersectionResults_t vROverlayIntersectionResults_t = default;
				OpenVrPhoneMenuHit hit2 = default;
				bool flag5 = (_menu.ControlsVisible & flag2) && !_grabbed && _menuView.TryPick(pointerPose, _menu.KeypadEnabled, out hit2);
				bool flag6 = flag3 && !_grabbed;
				if (flag6)
				{
					vROverlayIntersectionResults_t = _pointerSmoother.Update(hit, Environment.TickCount64);
				}
				else
				{
					_pointerSmoother.Reset();
				}
				bool flag7 = OpenVrInteractionGate.IsPhoneHit(flag6 && !flag5, _grabbed);
				bool flag8 = false;
				if (flag7 | flag5)
				{
					GetViewerPosition(out var x, out var y, out var z);
					flag8 = _pointerRenderer.TryUpdate(pointerPose, flag5 ? hit2.Intersection : vROverlayIntersectionResults_t, x, y, z);
				}
				else
				{
					_pointerRenderer.Hide();
				}
				bool flag9 = OpenVrInteractionGate.IsVisiblePhoneTarget(flag7, flag8, _grabbed);
				bool flag10 = flag5 & flag8;
				bool flag11 = OpenVrInteractionGate.CanStartGrab(flag4, _previousGrab, _grabbed, flag9 | flag10, flag) && !_menu.SuppressPhoneTouch;
				bool flag12 = ReadDigital(_phoneTouchAction);
				bool pressed = flag12 || (!flag5 && ReadDigital(_menuDismissAction));
				if (flag11)
				{
					_menu.ResetInput();
				}
				OpenVrMenuChange openVrMenuChange = (flag11 ? default(OpenVrMenuChange) : _menu.ProcessInput(flag2 && !_grabbed && (!flag5 | flag8), flag10, flag10 ? hit2.Target : OpenVrMenuTarget.None, hit2.SliderFraction, pressed, playspace));
				OpenVrPlayspaceControlRequest playspace2 = openVrMenuChange.Playspace;
				if ((object)playspace2 != null)
				{
					OpenVrRuntimeHost.ActionUpdates.RequestControls(playspace2.Enabled, playspace2.Multiplier);
				}
				OpenVrPhoneInputCommand? phoneCommand = openVrMenuChange.PhoneCommand;
				if (phoneCommand.HasValue)
				{
					OpenVrPhoneInputCommand valueOrDefault = phoneCommand.GetValueOrDefault();
					commands[commandCount++] = valueOrDefault;
				}
				if (openVrMenuChange.OpacityChanged)
				{
					Check(_overlay.SetOverlayAlpha(_overlayHandle, (float)_menu.OpacityPercent / 100f), "调整手机浮窗透明度");
				}
				if (openVrMenuChange.Consumed)
				{
					flag9 = false;
					RenderPresentation(playspace);
				}
				if (openVrMenuChange.VisibilityChanged)
				{
					_grabbed = false;
					_phoneActionsEnabled = false;
					_phoneActionSetGate.Reset();
				}
				bool flag13 = (flag9 & flag12) && !_menu.SuppressPhoneTouch;
				if (flag9 && !_menu.SuppressPhoneTouch)
				{
					float pointerX = Math.Clamp(vROverlayIntersectionResults_t.vUVs.v0, 0f, 1f);
					float pointerY = Math.Clamp(1f - vROverlayIntersectionResults_t.vUVs.v1, 0f, 1f);
					UpdateSmoothScroll(inputAnalogActionData_t, !flag13 && !flag4, pointerX, pointerY, commands, ref commandCount);
					if (_touchState.TryUpdate(flag13, pointerX, pointerY, Environment.TickCount64, out var command2))
					{
						commands[commandCount++] = command2;
					}
				}
				else
				{
					StopSmoothScroll(commands, ref commandCount);
					if (_touchState.TryRelease(out var command3))
					{
						commands[commandCount++] = command3;
					}
				}
				if (flag4 && !_previousGrab && _touchState.TryCancel(out var command4))
				{
					commands[commandCount++] = command4;
				}
				UpdateGrab(pose, flag, flag11, flag10 ? hit2.Intersection : vROverlayIntersectionResults_t, flag4, inputAnalogActionData_t, flag10 ? hit2.Surface : OpenVrPhoneSurface.Phone);
				if (_grabbed)
				{
					RenderPresentation(playspace);
				}
				OpenVrGrabAwakeAction openVrGrabAwakeAction = _grabAwakePolicy.NextAction(_grabbed && _menu.PhoneVisible, _keepAwakeWhileGrabbed, tickCount);
				if (openVrGrabAwakeAction != OpenVrGrabAwakeAction.None)
				{
					commands[commandCount++] = new OpenVrPhoneInputCommand((openVrGrabAwakeAction == OpenVrGrabAwakeAction.Wake) ? PhoneInputCommandKind.WakeScreen : PhoneInputCommandKind.UserActivity);
				}
				if (_grabbed)
				{
					_pointerSmoother.Reset();
					_pointerRenderer.Hide();
				}
				bool acceptsButtons = OpenVrInteractionGate.CanAcceptPhoneButtons(flag9 && !_menu.SuppressPhoneTouch, _grabbed);
				AppendButtonCommand(_phoneBackAction, PhoneInputCommandKind.Back, acceptsButtons, ref _previousBack, commands, ref commandCount);
				AppendButtonCommand(_phoneHomeAction, PhoneInputCommandKind.Home, acceptsButtons, ref _previousHome, commands, ref commandCount);
				AppendButtonCommand(_phoneRecentsAction, PhoneInputCommandKind.RecentApps, acceptsButtons, ref _previousRecents, commands, ref commandCount);
				AppendButtonCommand(_phoneControlPanelAction, PhoneInputCommandKind.OpenControlPanel, acceptsButtons, ref _previousControlPanel, commands, ref commandCount);
				AppendButtonCommand(_phoneScreenshotAction, PhoneInputCommandKind.Screenshot, acceptsButtons, ref _previousScreenshot, commands, ref commandCount);
				_previousGrab = flag4;
				_touchState.RecordTouchState(flag13);
				bool flag14 = flag9 | flag10;
				if (_grabbed || _touchState.IsDown)
				{
					_phoneActionsEnabled = true;
				}
				else if (_phoneActionsEnabled)
				{
					if (!flag14)
					{
						_phoneActionsEnabled = false;
						_phoneActionSetGate.Reset();
					}
				}
				else if (_phoneActionSetGate.ShouldEnable(flag14, rawStateActive, rawPressed))
				{
					_phoneActionsEnabled = true;
				}
				OpenVrPhoneInteraction openVrPhoneInteraction = this;
				bool worldAnchored = _worldAnchored;
				bool flag15 = flag14;
				bool grabbed = _grabbed;
				string text;
				if (_menu.DashboardVisible)
				{
					text = "OPENVR_DASHBOARD_SUSPENDED";
				}
				else if (_inputOverridesAvailable)
				{
					if (_worldAnchored)
					{
						if (snapshot2.State == OpenVrBindingHealthState.Failed)
						{
							text = snapshot2.ReasonCode;
						}
						else
						{
							text = ((!flag2) ? "OPENVR_POINTER_POSE_WAITING" : "OPENVR_INPUT_READY");
						}
					}
					else
					{
						text = "OPENVR_WAITING_FOR_HMD_POSE";
					}
				}
				else
				{
					text = "OPENVR_INPUT_OVERRIDE_UNAVAILABLE";
				}
				OpenVrPhoneInteraction openVrPhoneInteraction2 = openVrPhoneInteraction;
				bool worldAnchored2 = worldAnchored;
				bool hovered = flag15;
				bool grabbed2 = grabbed;
				string reasonCode = text;
				string message;
				if (_menu.DashboardVisible)
				{
					message = "SteamVR 菜单已打开，浮窗暂时隐藏";
				}
				else if (_inputOverridesAvailable)
				{
					if (_worldAnchored)
					{
						if (snapshot2.State == OpenVrBindingHealthState.Failed)
						{
							message = snapshot2.Message;
						}
						else if (flag2)
						{
							if (_grabbed)
							{
								message = "正在拖动手机浮窗";
							}
							else
							{
								message = (flag14 ? "手柄已指向手机控件" : "SteamVR 手柄输入已就绪");
							}
						}
						else
						{
							message = "正在等待已绑定的手柄射线姿态";
						}
					}
					else
					{
						message = "正在等待头显定位后固定手机浮窗";
					}
				}
				else
				{
					message = "SteamVR 未允许覆盖游戏输入，请在开发者设置启用实验性浮窗输入覆盖";
				}
				openVrPhoneInteraction2.Publish(new OpenVrPhoneInteractionSnapshot(InputReady: true, worldAnchored2, hovered, grabbed2, reasonCode, message, flag, _menu.KeypadVisible, null, null, _headsetModel, _controllerType, _pointerPoseBound, _pointerPoseActive, _touchBound, _touchActive, _grabBound, _grabActive, _scaleBound, _scaleActive));
			}
			for (int i = 0; i < commandCount; i++)
			{
				PhoneInputReceived?.Invoke(commands[i]);
			}
			return true;
		}
	}

	internal static int CapturePriority(bool available, bool validController, bool visibleTarget, bool grabbed)
	{
		if (!(available & validController) || !(visibleTarget | grabbed))
		{
			return 0;
		}
		return 16777216;
	}

	private bool DrainBindingEvents()
	{
		bool result = false;
		VREvent_t pEvent = default;
		uint uncbVREvent = checked((uint)Marshal.SizeOf<VREvent_t>());
		for (int i = 0; i < 32; i = checked(i + 1))
		{
			if (!_system.PollNextEvent(ref pEvent, uncbVREvent))
			{
				break;
			}
			EVREventType eVREventType = (EVREventType)checked((int)pEvent.eventType);
			if (eVREventType == EVREventType.VREvent_Input_BindingLoadFailed)
			{
				result = true;
			}
		}
		return result;
	}

	private static bool HasBinding(ulong action)
	{
		InputBindingInfo_t pOriginInfo = default;
		uint punReturnedBindingInfoCount = 0u;
		if (OpenVR.Input.GetActionBindingInfo(action, ref pOriginInfo, checked((uint)Marshal.SizeOf<InputBindingInfo_t>()), 1u, ref punReturnedBindingInfoCount) == EVRInputError.None)
		{
			return punReturnedBindingInfoCount != 0;
		}
		return false;
	}

	private bool IsDigitalActionActive(ulong action)
	{
		InputDigitalActionData_t pActionData = default;
		if (OpenVR.Input.GetDigitalActionData(action, ref pActionData, checked((uint)Marshal.SizeOf<InputDigitalActionData_t>()), _controllerInputSource) == EVRInputError.None)
		{
			return pActionData.bActive;
		}
		return false;
	}

	private bool IsAnalogActionActive(ulong action)
	{
		InputAnalogActionData_t pActionData = default;
		if (OpenVR.Input.GetAnalogActionData(action, ref pActionData, checked((uint)Marshal.SizeOf<InputAnalogActionData_t>()), _controllerInputSource) == EVRInputError.None)
		{
			return pActionData.bActive;
		}
		return false;
	}

	private bool UpdateGrab(HmdMatrix34_t controllerPose, bool validControllerPose, bool hovered, VROverlayIntersectionResults_t hit, bool grab, InputAnalogActionData_t scale, OpenVrPhoneSurface surface)
	{
		bool result = false;
		if (OpenVrInteractionGate.CanStartGrab(grab, _previousGrab, _grabbed, hovered, validControllerPose))
		{
			_grabSurface = surface;
			_grabHidden = _menu.PhoneHidden;
			_grabRelativeTransform = OpenVrTransformMath.Multiply(OpenVrTransformMath.InverseRigid(controllerPose), GrabRoot);
			_grabUvX = Math.Clamp(hit.vUVs.v0, 0f, 1f);
			_grabUvY = Math.Clamp(hit.vUVs.v1, 0f, 1f);
			_grabbed = true;
			result = true;
			_lastGrabTimestamp = Environment.TickCount64;
		}
		if (!_grabbed)
		{
			return result;
		}
		long tickCount = Environment.TickCount64;
		float num = Math.Clamp((float)checked(tickCount - _lastGrabTimestamp) / 1000f, 0.001f, 0.05f);
		_lastGrabTimestamp = tickCount;
		if (scale.bActive && MathF.Abs(scale.y) > 0.2f)
		{
			float currentWidthMeters = CurrentWidthMeters;
			float num2 = MathF.Exp(scale.y * num * 1.25f);
			_scaleFactor = OpenVrPhoneScaleRange.Clamp(_scaleFactor * num2);
			float currentWidthMeters2 = CurrentWidthMeters;
			if (MathF.Abs(currentWidthMeters - currentWidthMeters2) > 0.0001f)
			{
				SetGrabRoot(OpenVrPhoneGroupLayout.AnchorScale(_grabSurface, GrabRoot, currentWidthMeters, currentWidthMeters2, _frameAspectRatio, _grabHidden, _grabUvX, _grabUvY));
				ApplyOverlayWidth();
				_grabRelativeTransform = OpenVrTransformMath.Multiply(OpenVrTransformMath.InverseRigid(controllerPose), GrabRoot);
			}
		}
		HmdMatrix34_t target = OpenVrTransformMath.Multiply(controllerPose, _grabRelativeTransform);
		SetGrabRoot(OpenVrTransformMath.Smooth(GrabRoot, target, num));
		Check(_overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _overlayTransform), "拖动手机浮窗");
		return result;
	}

	private void UpdateSmoothScroll(InputAnalogActionData_t analog, bool enabled, float pointerX, float pointerY, Span<OpenVrPhoneInputCommand> commands, ref int commandCount)
	{
		Span<SmoothScrollCommand> commands2 = stackalloc SmoothScrollCommand[2];
		int num = _smoothScroll.Update(analog.bActive ? analog.y : 0f, enabled && !_grabbed, pointerX, pointerY, Environment.TickCount64, commands2);
		checked
		{
			for (int i = 0; i < num; i++)
			{
				SmoothScrollCommand smoothScrollCommand = commands2[i];
				commands[commandCount++] = new OpenVrPhoneInputCommand(smoothScrollCommand.Kind, smoothScrollCommand.NormalizedX, smoothScrollCommand.NormalizedY);
			}
		}
	}

	private void StopSmoothScroll(Span<OpenVrPhoneInputCommand> commands, ref int commandCount)
	{
		Span<SmoothScrollCommand> commands2 = stackalloc SmoothScrollCommand[1];
		int num = _smoothScroll.Stop(commands2);
		if (num == 1)
		{
			SmoothScrollCommand smoothScrollCommand = commands2[0];
			commands[checked(commandCount++)] = new OpenVrPhoneInputCommand(smoothScrollCommand.Kind, smoothScrollCommand.NormalizedX, smoothScrollCommand.NormalizedY);
		}
	}

	private void ApplyOverlayWidth()
	{
		float currentWidthMeters = CurrentWidthMeters;
		if (!float.IsFinite(_appliedWidthMeters) || !(MathF.Abs(currentWidthMeters - _appliedWidthMeters) <= 0.0001f))
		{
			Check(_overlay.SetOverlayWidthInMeters(_overlayHandle, currentWidthMeters), "调整手机浮窗尺寸");
			_appliedWidthMeters = currentWidthMeters;
		}
	}

	private void SetGrabRoot(HmdMatrix34_t value)
	{
		if (_grabHidden)
		{
			HmdMatrix34_t left = OpenVrTransformMath.Multiply(value, OpenVrTransformMath.InverseRigid(_recalledHead));
			_overlayTransform = OpenVrTransformMath.Multiply(left, _overlayTransform);
			_recalledHead = value;
		}
		else
		{
			_overlayTransform = value;
		}
	}

	private void AppendButtonCommand(ulong action, PhoneInputCommandKind kind, bool acceptsButtons, ref bool previous, Span<OpenVrPhoneInputCommand> commands, ref int commandCount)
	{
		bool flag = ReadDigital(action);
		if (OpenVrInteractionGate.ShouldEmitPhoneButton(acceptsButtons, flag, previous))
		{
			commands[checked(commandCount++)] = new OpenVrPhoneInputCommand(kind);
		}
		previous = flag;
	}

	private bool TryIntersect(HmdMatrix34_t pointer, out VROverlayIntersectionResults_t hit)
	{
		if (!_worldAnchored)
		{
			hit = default;
			return false;
		}
		return OpenVrPhoneRaycaster.TryIntersect(pointer, _overlayTransform, CurrentWidthMeters, _frameAspectRatio, out hit);
	}

	private void SetInitialTransform()
	{
		HmdMatrix34_t pmatTrackedDeviceToOverlayTransform = OpenVrTransformMath.Identity();
		pmatTrackedDeviceToOverlayTransform.m11 = 0f - _initialDistanceMeters;
		lock (_openVrGate)
		{
			_system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, _trackedPoses);
			if (_trackedPoses[0].bPoseIsValid)
			{
				_overlayTransform = OpenVrTransformMath.Multiply(_trackedPoses[0].mDeviceToAbsoluteTracking, pmatTrackedDeviceToOverlayTransform);
				Check(_overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _overlayTransform), "固定手机浮窗");
				_worldAnchored = true;
			}
			else
			{
				_overlayTransform = pmatTrackedDeviceToOverlayTransform;
				Check(_overlay.SetOverlayTransformTrackedDeviceRelative(_overlayHandle, 0u, ref pmatTrackedDeviceToOverlayTransform), "设置手机浮窗初始位置");
			}
		}
	}

	private void EnsureWorldAnchored()
	{
		if (!_worldAnchored)
		{
			_system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, _trackedPoses);
			if (_trackedPoses[0].bPoseIsValid)
			{
				_overlayTransform = OpenVrTransformMath.Multiply(_trackedPoses[0].mDeviceToAbsoluteTracking, _overlayTransform);
				Check(_overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref _overlayTransform), "固定手机浮窗");
				_worldAnchored = true;
			}
		}
	}

	private void QueuePlacement()
	{
		if (_frameReady)
		{
			_placementStore.Queue(new OpenVrSavedPlacement(_scaleFactor));
		}
	}

	private bool ReadDigital(ulong action)
	{
		return ReadDigital(action, _controllerInputSource);
	}

	private static bool ReadDigital(ulong action, ulong inputSource)
	{
		InputDigitalActionData_t pActionData = default;
		if (OpenVR.Input.GetDigitalActionData(action, ref pActionData, checked((uint)Marshal.SizeOf<InputDigitalActionData_t>()), inputSource) == EVRInputError.None && pActionData.bActive)
		{
			return pActionData.bState;
		}
		return false;
	}

	private InputAnalogActionData_t ReadAnalog(ulong action)
	{
		InputAnalogActionData_t pActionData = default;
		OpenVR.Input.GetAnalogActionData(action, ref pActionData, checked((uint)Marshal.SizeOf<InputAnalogActionData_t>()), _controllerInputSource);
		return pActionData;
	}

	private bool TryGetControllerPose(out HmdMatrix34_t pose)
	{
		pose = default;
		_system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, _trackedPoses);
		if (_headsetModel == null)
		{
			_headsetModel = ReadTrackedDeviceProperty(0u, ETrackedDeviceProperty.Prop_ModelNumber_String);
		}
		ETrackedControllerRole unDeviceType = OpenVrControllerHandRouting.TrackedRole(_controllerHand);
		uint trackedDeviceIndexForControllerRole = _system.GetTrackedDeviceIndexForControllerRole(unDeviceType);
		if (trackedDeviceIndexForControllerRole == uint.MaxValue || trackedDeviceIndexForControllerRole >= _trackedPoses.Length)
		{
			return false;
		}
		if (_controllerDeviceIndex != trackedDeviceIndexForControllerRole)
		{
			_controllerDeviceIndex = trackedDeviceIndexForControllerRole;
			_controllerType = ReadTrackedDeviceProperty(trackedDeviceIndexForControllerRole, ETrackedDeviceProperty.Prop_ControllerType_String);
		}
		TrackedDevicePose_t trackedDevicePose_t = _trackedPoses[trackedDeviceIndexForControllerRole];
		if (!trackedDevicePose_t.bPoseIsValid || !trackedDevicePose_t.bDeviceIsConnected)
		{
			return false;
		}
		pose = trackedDevicePose_t.mDeviceToAbsoluteTracking;
		return true;
	}

	private string? ReadTrackedDeviceProperty(uint deviceIndex, ETrackedDeviceProperty property)
	{
		ETrackedPropertyError pError = ETrackedPropertyError.TrackedProp_Success;
		StringBuilder pchValue = new StringBuilder(1);
		uint stringTrackedDeviceProperty = _system.GetStringTrackedDeviceProperty(deviceIndex, property, pchValue, 0u, ref pError);
		bool flag = stringTrackedDeviceProperty <= 1;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((pError == ETrackedPropertyError.TrackedProp_Success || pError == ETrackedPropertyError.TrackedProp_BufferTooSmall) ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder(checked((int)stringTrackedDeviceProperty));
		pError = ETrackedPropertyError.TrackedProp_Success;
		_system.GetStringTrackedDeviceProperty(deviceIndex, property, stringBuilder, stringTrackedDeviceProperty, ref pError);
		if (pError != ETrackedPropertyError.TrackedProp_Success || stringBuilder.Length <= 0)
		{
			return null;
		}
		return stringBuilder.ToString();
	}

	private void GetViewerPosition(out float x, out float y, out float z)
	{
		TrackedDevicePose_t trackedDevicePose_t = _trackedPoses[0];
		if (trackedDevicePose_t.bPoseIsValid)
		{
			x = trackedDevicePose_t.mDeviceToAbsoluteTracking.m3;
			y = trackedDevicePose_t.mDeviceToAbsoluteTracking.m7;
			z = trackedDevicePose_t.mDeviceToAbsoluteTracking.m11;
		}
		else
		{
			x = _overlayTransform.m3 + _overlayTransform.m2;
			y = _overlayTransform.m7 + _overlayTransform.m6;
			z = _overlayTransform.m11 + _overlayTransform.m10;
		}
	}

	private void CancelSmoothScroll()
	{
		Span<SmoothScrollCommand> commands = stackalloc SmoothScrollCommand[1];
		if (_smoothScroll.Cancel(commands) == 1)
		{
			SmoothScrollCommand smoothScrollCommand = commands[0];
			PhoneInputReceived?.Invoke(new OpenVrPhoneInputCommand(smoothScrollCommand.Kind, smoothScrollCommand.NormalizedX, smoothScrollCommand.NormalizedY));
		}
	}

	private InputPoseActionData_t ReadPose(ulong action)
	{
		InputPoseActionData_t pActionData = default;
		OpenVR.Input.GetPoseActionDataForNextFrame(action, ETrackingUniverseOrigin.TrackingUniverseStanding, ref pActionData, checked((uint)Marshal.SizeOf<InputPoseActionData_t>()), _controllerInputSource);
		return pActionData;
	}

	private OpenVrPhoneInteractionSnapshot GetSnapshot()
	{
		lock (_stateGate)
		{
			return Snapshot;
		}
	}

	private void Publish(OpenVrPhoneInteractionSnapshot snapshot)
	{
		lock (_stateGate)
		{
			Snapshot = snapshot;
		}
	}

	private static void Check(EVRInputError error, string operation)
	{
		if (error != EVRInputError.None)
		{
			throw new InvalidOperationException($"{operation}失败：{error}");
		}
	}

	private static void Check(EVROverlayError error, string operation)
	{
		if (error != EVROverlayError.None)
		{
			throw new InvalidOperationException($"{operation}失败：{error}");
		}
	}
}
