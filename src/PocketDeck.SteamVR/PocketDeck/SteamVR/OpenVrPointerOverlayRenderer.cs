using System;
using System.Runtime.InteropServices;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPointerOverlayRenderer(CVROverlay overlay) : IDisposable
{
	private const float _cursorWidthMeters = 0.014f;

	private readonly CVROverlay _overlay = overlay;

	private ulong _cursorOverlayHandle;

	private ulong _rayOverlayHandle;

	private bool _cursorVisible;

	private bool _rayVisible;

	public bool TryUpdate(HmdMatrix34_t pointer, VROverlayIntersectionResults_t hit, float viewerX, float viewerY, float viewerZ)
	{
		if (!EnsureCursor() || !EnsureRay())
		{
			Hide();
			return false;
		}
		float x = viewerX - hit.vPoint.v0;
		float y = viewerY - hit.vPoint.v1;
		float z = viewerZ - hit.vPoint.v2;
		NormalizeOrFallback(ref x, ref y, ref z, hit.vNormal.v0, hit.vNormal.v1, hit.vNormal.v2);
		HmdMatrix34_t pmatTrackingOriginToOverlayTransform = CreateBillboardTransform(hit.vPoint.v0 + x * 0.0012f, hit.vPoint.v1 + y * 0.0012f, hit.vPoint.v2 + z * 0.0012f, x, y, z);
		if (_overlay.SetOverlayTransformAbsolute(_cursorOverlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref pmatTrackingOriginToOverlayTransform) != EVROverlayError.None)
		{
			DestroyCursor();
			Hide();
			return false;
		}
		float m = pointer.m3;
		float m2 = pointer.m7;
		float m3 = pointer.m11;
		float num = hit.vPoint.v0 - m;
		float num2 = hit.vPoint.v1 - m2;
		float num3 = hit.vPoint.v2 - m3;
		float num4 = MathF.Sqrt(num * num + num2 * num2 + num3 * num3);
		if (num4 > 0.001f)
		{
			num /= num4;
			num2 /= num4;
			num3 /= num4;
			float num5 = (m + hit.vPoint.v0) * 0.5f;
			float num6 = (m2 + hit.vPoint.v1) * 0.5f;
			float num7 = (m3 + hit.vPoint.v2) * 0.5f;
			float num8 = viewerX - num5;
			float num9 = viewerY - num6;
			float num10 = viewerZ - num7;
			float num11 = num8 * num + num9 * num2 + num10 * num3;
			num8 -= num11 * num;
			num9 -= num11 * num2;
			num10 -= num11 * num3;
			NormalizeOrFallback(ref num8, ref num9, ref num10, hit.vNormal.v0, hit.vNormal.v1, hit.vNormal.v2);
			float yAxisX = num9 * num3 - num10 * num2;
			float yAxisY = num10 * num - num8 * num3;
			float yAxisZ = num8 * num2 - num9 * num;
			HmdMatrix34_t pmatTrackingOriginToOverlayTransform2 = CreateTransform(num, num2, num3, yAxisX, yAxisY, yAxisZ, num8, num9, num10, num5, num6, num7);
			if (_overlay.SetOverlayTransformAbsolute(_rayOverlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref pmatTrackingOriginToOverlayTransform2) != EVROverlayError.None)
			{
				Dispose();
				return false;
			}
			if (_overlay.SetOverlayWidthInMeters(_rayOverlayHandle, num4) != EVROverlayError.None)
			{
				Dispose();
				return false;
			}
		}
		if (!_cursorVisible)
		{
			if (_overlay.ShowOverlay(_cursorOverlayHandle) != EVROverlayError.None)
			{
				Hide();
				return false;
			}
			_cursorVisible = true;
		}
		if (!_rayVisible)
		{
			if (_overlay.ShowOverlay(_rayOverlayHandle) != EVROverlayError.None)
			{
				Hide();
				return false;
			}
			_rayVisible = true;
		}
		if (_cursorVisible)
		{
			return _rayVisible;
		}
		return false;
	}

	public void Hide()
	{
		HideCursor();
		if (_rayVisible && _rayOverlayHandle != 0L)
		{
			_overlay.HideOverlay(_rayOverlayHandle);
			_rayVisible = false;
		}
	}

	public void Dispose()
	{
		DestroyCursor();
		if (_rayOverlayHandle != 0L)
		{
			_overlay.HideOverlay(_rayOverlayHandle);
			_overlay.ClearOverlayTexture(_rayOverlayHandle);
			_overlay.DestroyOverlay(_rayOverlayHandle);
			_rayOverlayHandle = 0uL;
			_rayVisible = false;
		}
	}

	private bool EnsureCursor()
	{
		if (_cursorOverlayHandle != 0L)
		{
			return true;
		}
		if (_overlay.CreateOverlay("io.github.vrphonescreen.overlay.pointer", "VRPhoneScreen Overlay - Pointer", ref _cursorOverlayHandle) != EVROverlayError.None)
		{
			_cursorOverlayHandle = 0uL;
			return false;
		}
		_overlay.SetOverlayInputMethod(_cursorOverlayHandle, VROverlayInputMethod.None);
		_overlay.SetOverlayAlpha(_cursorOverlayHandle, 0.95f);
		_overlay.SetOverlayWidthInMeters(_cursorOverlayHandle, 0.014f);
		_overlay.SetOverlaySortOrder(_cursorOverlayHandle, 100u);
		byte[] value = CreatePointerPixels();
		GCHandle gCHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
		try
		{
			if (_overlay.SetOverlayRaw(_cursorOverlayHandle, gCHandle.AddrOfPinnedObject(), 32u, 32u, 4u) != EVROverlayError.None)
			{
				DestroyCursor();
				return false;
			}
		}
		finally
		{
			gCHandle.Free();
		}
		return true;
	}

	private bool EnsureRay()
	{
		if (_rayOverlayHandle != 0L)
		{
			return true;
		}
		if (_overlay.CreateOverlay("io.github.vrphonescreen.overlay.ray", "VRPhoneScreen Overlay - Ray", ref _rayOverlayHandle) != EVROverlayError.None)
		{
			_rayOverlayHandle = 0uL;
			return false;
		}
		_overlay.SetOverlayInputMethod(_rayOverlayHandle, VROverlayInputMethod.None);
		_overlay.SetOverlayAlpha(_rayOverlayHandle, 0.8f);
		_overlay.SetOverlaySortOrder(_rayOverlayHandle, 99u);
		byte[] value = CreateRayPixels();
		GCHandle gCHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
		try
		{
			if (_overlay.SetOverlayRaw(_rayOverlayHandle, gCHandle.AddrOfPinnedObject(), 512u, 4u, 4u) != EVROverlayError.None)
			{
				Dispose();
				return false;
			}
		}
		finally
		{
			gCHandle.Free();
		}
		return true;
	}

	private void HideCursor()
	{
		if (_cursorVisible && _cursorOverlayHandle != 0L)
		{
			_overlay.HideOverlay(_cursorOverlayHandle);
			_cursorVisible = false;
		}
	}

	private void DestroyCursor()
	{
		if (_cursorOverlayHandle != 0L)
		{
			_overlay.HideOverlay(_cursorOverlayHandle);
			_overlay.ClearOverlayTexture(_cursorOverlayHandle);
			_overlay.DestroyOverlay(_cursorOverlayHandle);
			_cursorOverlayHandle = 0uL;
			_cursorVisible = false;
		}
	}

	private static byte[] CreatePointerPixels()
	{
		byte[] array = new byte[4096];
		checked
		{
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num = (float)j - 15.5f;
					float num2 = (float)i - 15.5f;
					float num3 = num * num + num2 * num2;
					int num4 = (i * 32 + j) * 4;
					bool flag = num3 <= 156.25f;
					bool flag2 = num3 <= 25f;
					array[num4] = unchecked((byte)(flag2 ? 72 : byte.MaxValue));
					array[num4 + 1] = unchecked((byte)(flag2 ? 180 : byte.MaxValue));
					array[num4 + 2] = byte.MaxValue;
					array[num4 + 3] = unchecked((byte)(flag ? 245 : 0));
				}
			}
			return array;
		}
	}

	private static byte[] CreateRayPixels()
	{
		byte[] array = new byte[8192];
		checked
		{
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 512; j++)
				{
					int num = (i * 512 + j) * 4;
					array[num] = 72;
					array[num + 1] = 190;
					array[num + 2] = byte.MaxValue;
					byte[] array2 = array;
					int num2 = num + 3;
					unchecked
					{
						bool flag = (uint)(i - 1) <= 1u;
						array2[num2] = (byte)(flag ? 230 : 90);
					}
				}
			}
			return array;
		}
	}

	private static HmdMatrix34_t CreateBillboardTransform(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
	{
		float x = normalZ;
		float y = 0f;
		float z = 0f - normalX;
		NormalizeOrFallback(ref x, ref y, ref z, 1f, 0f, 0f);
		float yAxisX = normalY * z - normalZ * y;
		float yAxisY = normalZ * x - normalX * z;
		float yAxisZ = normalX * y - normalY * x;
		return CreateTransform(x, y, z, yAxisX, yAxisY, yAxisZ, normalX, normalY, normalZ, positionX, positionY, positionZ);
	}

	private static HmdMatrix34_t CreateTransform(float xAxisX, float xAxisY, float xAxisZ, float yAxisX, float yAxisY, float yAxisZ, float zAxisX, float zAxisY, float zAxisZ, float positionX, float positionY, float positionZ)
	{
		return new HmdMatrix34_t
		{
			m0 = xAxisX,
			m1 = yAxisX,
			m2 = zAxisX,
			m3 = positionX,
			m4 = xAxisY,
			m5 = yAxisY,
			m6 = zAxisY,
			m7 = positionY,
			m8 = xAxisZ,
			m9 = yAxisZ,
			m10 = zAxisZ,
			m11 = positionZ
		};
	}

	private static void NormalizeOrFallback(ref float x, ref float y, ref float z, float fallbackX, float fallbackY, float fallbackZ)
	{
		float num = MathF.Sqrt(x * x + y * y + z * z);
		if (num < 1E-05f)
		{
			x = fallbackX;
			y = fallbackY;
			z = fallbackZ;
			num = MathF.Sqrt(x * x + y * y + z * z);
		}
		if (num < 1E-05f)
		{
			x = 0f;
			y = 0f;
			z = 1f;
		}
		else
		{
			x /= num;
			y /= num;
			z /= num;
		}
	}
}
