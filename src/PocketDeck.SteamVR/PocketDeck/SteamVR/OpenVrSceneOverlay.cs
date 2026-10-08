using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrSceneOverlay : IOpenVrSceneOverlay, IDisposable
{
	private readonly object _gate = OpenVrRuntimeHost.ApiGate;

	private readonly float _shortEdgeMeters;

	private readonly OpenVrRetainedVideoTexture _lastTexture = new OpenVrRetainedVideoTexture();

	private OpenVrPhoneInteraction? _interaction;

	private CVROverlay? _overlay;

	private ulong _handle;

	private bool _disposed;

	public bool IsVisible => _interaction?.PhonePresented ?? false;

	public long GraphicsAdapterLuid { get; }

	public OpenVrPhoneInteractionSnapshot Interaction => _interaction?.Snapshot ?? OpenVrPhoneInteractionSnapshot.Stopped;

	public OpenVrBindingHealthSnapshot BindingHealth => _interaction?.BindingHealth ?? OpenVrBindingHealthSnapshot.Stopped;

	public event OpenVrPhoneInputSink? PhoneInputReceived;

	public void SetPhoneLocked(bool? locked)
	{
		lock (_gate)
		{
			_interaction?.SetPhoneLocked(locked);
		}
	}

	public OpenVrSceneOverlay(OpenVrOverlaySettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings, "settings");
		if (string.IsNullOrWhiteSpace(settings.Key) || string.IsNullOrWhiteSpace(settings.Name))
		{
			throw new ArgumentException("OpenVR overlay key and name must not be empty.", "settings");
		}
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.WidthMeters, 0f, "settings.WidthMeters");
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.DistanceMeters, 0f, "settings.DistanceMeters");
		_shortEdgeMeters = settings.WidthMeters;
		try
		{
			EVRInitError initializationError = EVRInitError.None;
			CVRSystem orStart = OpenVrRuntimeHost.GetOrStart(ref initializationError);
			if (initializationError != EVRInitError.None)
			{
				throw new OpenVrOverlayException("OPENVR_INITIALIZATION_FAILED", $"SteamVR 初始化失败：{initializationError}");
			}
			ulong pnDevice = 0uL;
			orStart.GetOutputDevice(ref pnDevice, ETextureType.DirectX, IntPtr.Zero);
			GraphicsAdapterLuid = (long)pnDevice;
			_overlay = OpenVR.Overlay ?? throw new OpenVrOverlayException("OPENVR_OVERLAY_INTERFACE_MISSING", "SteamVR 没有返回浮窗接口");
			Check(_overlay.CreateOverlay(settings.Key, settings.Name, ref _handle), "OPENVR_OVERLAY_CREATE_FAILED", "SteamVR 手机浮窗创建失败");
			Check(_overlay.SetOverlayWidthInMeters(_handle, settings.WidthMeters), "OPENVR_OVERLAY_WIDTH_FAILED", "SteamVR 手机浮窗尺寸设置失败");
			Check(_overlay.SetOverlayAlpha(_handle, 1f), "OPENVR_OVERLAY_ALPHA_FAILED", "SteamVR 手机浮窗透明度设置失败");
			Check(_overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.None), "OPENVR_OVERLAY_INPUT_FAILED", "SteamVR 手机浮窗输入模式设置失败");
			_interaction = new OpenVrPhoneInteraction(_gate, orStart, _overlay, _handle, settings.DistanceMeters, SubmitRetainedTexture);
			_interaction.PhoneInputReceived += OnPhoneInputReceived;
		}
		catch (OpenVrOverlayException)
		{
			Dispose();
			throw;
		}
		catch (Exception ex2) when ((ex2 is DllNotFoundException || ex2 is EntryPointNotFoundException || ex2 is BadImageFormatException) ? true : false)
		{
			Dispose();
			throw new OpenVrOverlayException("OPENVR_NATIVE_LIBRARY_FAILED", "OpenVR 运行库加载失败", ex2);
		}
		catch (Exception inner)
		{
			Dispose();
			throw new OpenVrOverlayException("OPENVR_INITIALIZATION_UNEXPECTED", "SteamVR 浮窗初始化遇到未预期错误", inner);
		}
	}

	public void ConfigureLockScreenFeatures(bool keepAwakeWhileGrabbed, bool unlockKeypadEnabled)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_interaction?.ConfigureLockScreenFeatures(keepAwakeWhileGrabbed, unlockKeypadEnabled);
	}

	public void Submit(GpuVideoFrame frame)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (frame.NativeTexturePointer == IntPtr.Zero)
		{
			throw new ArgumentException("GPU frame texture pointer must not be zero.", "frame");
		}
		lock (_gate)
		{
			_lastTexture.Update(frame.NativeTexturePointer);
			float baseWidthMeters = CalculateOverlayWidthMeters(_shortEdgeMeters, frame.Width, frame.Height);
			_interaction.UpdateFrameGeometry(baseWidthMeters, frame.Width, frame.Height);
			_interaction.PrepareVideoSubmission();
			if (IsVisible)
			{
				SubmitRetainedTexture();
			}
		}
	}

	private void SubmitRetainedTexture()
	{
		if (_lastTexture.Pointer != 0)
		{
			Texture_t pTexture = new Texture_t
			{
				handle = _lastTexture.Pointer,
				eType = ETextureType.DirectX,
				eColorSpace = EColorSpace.Auto
			};
			Check(_overlay.SetOverlayTexture(_handle, ref pTexture), "OPENVR_OVERLAY_TEXTURE_FAILED", "SteamVR 手机画面提交失败");
		}
	}

	internal static float CalculateOverlayWidthMeters(float shortEdgeMeters, int frameWidth, int frameHeight)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shortEdgeMeters, 0f, "shortEdgeMeters");
		ArgumentOutOfRangeException.ThrowIfLessThan(frameWidth, 1, "frameWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(frameHeight, 1, "frameHeight");
		if (frameWidth > frameHeight)
		{
			return shortEdgeMeters * (float)frameWidth / (float)frameHeight;
		}
		return shortEdgeMeters;
	}

	public void Hide()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		lock (_gate)
		{
			_interaction?.StopPresentation();
		}
	}

	public OpenVrBindingResult OpenBindingUi()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _interaction?.OpenBindingUi() ?? new OpenVrBindingResult(Succeeded: false, "OPENVR_INPUT_NOT_READY", "SteamVR 手柄输入尚未就绪");
	}

	public void Dispose()
	{
		OpenVrPhoneInteraction interaction;
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			interaction = _interaction;
			_interaction = null;
			if (interaction != null)
			{
				interaction.PhoneInputReceived -= OnPhoneInputReceived;
			}
		}
		interaction?.Dispose();
		lock (_gate)
		{
			if (_overlay != null && _handle != 0L)
			{
				_overlay.HideOverlay(_handle);
				_overlay.ClearOverlayTexture(_handle);
				_overlay.DestroyOverlay(_handle);
				_handle = 0uL;
			}
			_overlay = null;
			_lastTexture.Dispose();
		}
	}

	private void OnPhoneInputReceived(OpenVrPhoneInputCommand command)
	{
		PhoneInputReceived?.Invoke(command);
	}

	private static void Check(EVROverlayError error, string reasonCode, string message)
	{
		if (error != EVROverlayError.None)
		{
			throw new OpenVrOverlayException(reasonCode, $"{message}：{error}");
		}
	}
}
