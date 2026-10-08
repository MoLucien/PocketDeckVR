using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPhoneBackface(CVROverlay overlay) : IDisposable
{
	private readonly OpenVrStaticSurface _surface = new OpenVrStaticSurface(overlay, "io.github.vrphonescreen.overlay.phone.back");

	private bool _initialized;

	public void Update(bool visible, HmdMatrix34_t front, float width, float aspect)
	{
		if (!visible)
		{
			_surface.Hide();
			return;
		}
		if (!_initialized && _surface.CanUpdatePixels)
		{
			_initialized = _surface.SetPixels(new byte[4] { 0, 0, 0, 255 }, 1, 1);
		}
		_surface.Show(BackTransform(front), width, aspect);
	}

	internal static HmdMatrix34_t BackTransform(HmdMatrix34_t front)
	{
		HmdMatrix34_t right = OpenVrTransformMath.Identity();
		right.m0 = -1f;
		right.m10 = -1f;
		right.m11 = -0.001f;
		return OpenVrTransformMath.Multiply(front, right);
	}

	public void Dispose()
	{
		_surface.Dispose();
	}
}
