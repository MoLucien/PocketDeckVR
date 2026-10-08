using System;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrStaticSurface(CVROverlay overlay, string key, Func<int, int, D3D11ControlTexture>? textureFactory = null) : IDisposable
{
	private ulong _handle;

	private bool _visible;

	private bool _textureReady;

	private D3D11ControlTexture? _texture;

	private long _retryAfter;

	public long FailedUpdates { get; private set; }

	public bool CanUpdatePixels => Environment.TickCount64 >= _retryAfter;

	public bool SetPixels(byte[] pixels, int width, int height)
	{
		if (Environment.TickCount64 < _retryAfter)
		{
			return false;
		}
		checked
		{
			try
			{
				if (_handle == 0L)
				{
					Check(overlay.CreateOverlay(key, "VRPhoneScreen Overlay controls", ref _handle));
					Check(overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.None));
				}
				if (_texture == null)
				{
					_texture = (textureFactory ?? new Func<int, int, D3D11ControlTexture>(D3D11ControlTexture.CreateForSteamVr))(width, height);
				}
				if (_texture.Width != width || _texture.Height != height)
				{
					throw new ArgumentException("Control surface dimensions must remain fixed.");
				}
				_texture.Update(pixels);
				Texture_t pTexture = new Texture_t
				{
					handle = _texture.NativePointer,
					eType = ETextureType.DirectX,
					eColorSpace = EColorSpace.Auto
				};
				Check(overlay.SetOverlayTexture(_handle, ref pTexture));
				_texture.FlushSubmission();
				_textureReady = true;
				return true;
			}
			catch (Exception ex) when ((ex is SharpGenException || ex is COMException || ex is OpenVrOverlayException || ex is InvalidOperationException) ? true : false)
			{
				FailedUpdates++;
				_retryAfter = Environment.TickCount64 + 1000;
				return false;
			}
		}
	}

	public bool Show(HmdMatrix34_t transform, float width, float texelAspect = 1f)
	{
		if (!_textureReady)
		{
			return false;
		}
		if (!CanUpdatePixels)
		{
			return _visible;
		}
		checked
		{
			try
			{
				Check(overlay.SetOverlayWidthInMeters(_handle, width));
				Check(overlay.SetOverlayTexelAspect(_handle, texelAspect));
				Check(overlay.SetOverlayTransformAbsolute(_handle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform));
				if (!_visible)
				{
					Check(overlay.ShowOverlay(_handle));
					_visible = true;
				}
				return true;
			}
			catch (OpenVrOverlayException)
			{
				FailedUpdates++;
				_retryAfter = Environment.TickCount64 + 1000;
				return _visible;
			}
		}
	}

	public void Hide()
	{
		if (_visible)
		{
			overlay.HideOverlay(_handle);
			_visible = false;
		}
	}

	public void Dispose()
	{
		Hide();
		if (_handle != 0L)
		{
			overlay.ClearOverlayTexture(_handle);
			overlay.DestroyOverlay(_handle);
			_handle = 0uL;
		}
		_textureReady = false;
		_texture?.Dispose();
		_texture = null;
	}

	private static void Check(EVROverlayError error)
	{
		if (error != EVROverlayError.None)
		{
			throw new OpenVrOverlayException("OPENVR_STATIC_SURFACE_FAILED", "SteamVR 附属浮窗暂时不可用");
		}
	}
}
