using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPhoneMenuView(CVROverlay overlay, Func<int, int, D3D11ControlTexture>? textureFactory = null) : IDisposable
{
	private readonly OpenVrStaticSurface _dot = new OpenVrStaticSurface(overlay, "io.github.vrphonescreen.overlay.phone.menu-dot", textureFactory);

	private readonly OpenVrStaticSurface _panel = new OpenVrStaticSurface(overlay, "io.github.vrphonescreen.overlay.phone.menu-panel", textureFactory);

	private readonly OpenVrStaticSurface _keypad = new OpenVrStaticSurface(overlay, "io.github.vrphonescreen.overlay.phone.unlock-keypad", textureFactory);

	private readonly byte[] _keypadPixels = new byte[460800];

	private int _paintedDigits = -1;

	private bool _keypadVisible;

	private HmdMatrix34_t _keypadTransform;

	private bool _dotUploaded;

	private bool _dotVisible;

	private bool _panelVisible;

	private HmdMatrix34_t _dotTransform;

	private HmdMatrix34_t _panelTransform;

	private OpenVrMenuVisual? _painted;

	private readonly byte[] _pixels = new byte[921600];

	public void Render(OpenVrPhoneMenuState state, HmdMatrix34_t phone, float width, float aspect, HmdMatrix34_t head, OpenVrSharedPlayspaceInput playspace)
	{
		if (!_dotUploaded && _dot.CanUpdatePixels)
		{
			_dotUploaded = _dot.SetPixels(OpenVrPhoneMenuPixels.Dot(), 64, 64);
		}
		OpenVrMenuVisual value = new OpenVrMenuVisual(state.PhoneHidden, state.OpacityPercent, playspace.PlayspaceEnabled, playspace.Multiplier, state.KeypadEnabled, 0, state.KeypadVisible);
		if (_painted != value && _panel.CanUpdatePixels)
		{
			OpenVrPhoneMenuPixels.PaintPanel(_pixels, value);
			if (_panel.SetPixels(_pixels, 360, 640))
			{
				_painted = value;
			}
		}
		if (_paintedDigits != state.EnteredDigitCount && _keypad.CanUpdatePixels)
		{
			OpenVrPhoneMenuPixels.PaintKeypad(_keypadPixels, state.EnteredDigitCount);
			if (_keypad.SetPixels(_keypadPixels, 360, 320))
			{
				_paintedDigits = state.EnteredDigitCount;
			}
		}
		if (!state.ControlsVisible)
		{
			Hide();
			return;
		}
		HmdMatrix34_t root = (state.PhoneHidden ? head : phone);
		_dotTransform = OpenVrPhoneGroupLayout.SurfacePose(OpenVrPhoneSurface.Dot, root, width, aspect, state.PhoneHidden);
		_dotVisible = _dot.Show(_dotTransform, 0.048f);
		_panelTransform = OpenVrPhoneGroupLayout.SurfacePose(OpenVrPhoneSurface.Menu, root, width, aspect, state.PhoneHidden);
		_panelVisible = state.SidebarVisible && _panel.Show(_panelTransform, 0.32f);
		if (!state.SidebarVisible)
		{
			_panel.Hide();
		}
		_keypadTransform = OpenVrPhoneGroupLayout.SurfacePose(OpenVrPhoneSurface.Keypad, phone, width, aspect, hidden: false);
		_keypadVisible = state.KeypadVisible && _keypad.Show(_keypadTransform, 0.26f);
		if (!state.KeypadVisible)
		{
			_keypad.Hide();
		}
	}

	public bool TryPick(HmdMatrix34_t pointer, bool keypad, out OpenVrPhoneMenuHit hit)
	{
		hit = default;
		if (_keypadVisible && OpenVrPhoneMenuLayout.TryIntersect(pointer, _keypadTransform, 0.26f, 1.125f, out var hit2) && OpenVrPhoneMenuLayout.TryMapKeypad(hit2.vUVs.v0, 1f - hit2.vUVs.v1, out var target))
		{
			hit = new OpenVrPhoneMenuHit(target, 0f, hit2, OpenVrPhoneSurface.Keypad);
			return true;
		}
		if (_dotVisible && OpenVrPhoneMenuLayout.TryIntersect(pointer, _dotTransform, 0.048f, 1f, out var hit3) && OpenVrPhoneMenuLayout.InsideDot(hit3.vUVs.v0, hit3.vUVs.v1))
		{
			hit = new OpenVrPhoneMenuHit(OpenVrMenuTarget.Dot, 0f, hit3, OpenVrPhoneSurface.Dot);
			return true;
		}
		if (_panelVisible && OpenVrPhoneMenuLayout.TryIntersect(pointer, _panelTransform, 0.32f, 0.5625f, out var hit4) && OpenVrPhoneMenuLayout.TryMap(hit4.vUVs.v0, 1f - hit4.vUVs.v1, keypad, out var target2, out var fraction))
		{
			hit = new OpenVrPhoneMenuHit(target2, fraction, hit4, OpenVrPhoneSurface.Menu);
			return true;
		}
		return false;
	}

	public void Hide()
	{
		_dot.Hide();
		_panel.Hide();
		_keypad.Hide();
		_keypadVisible = false;
		_dotVisible = false;
		_panelVisible = false;
	}

	public void Dispose()
	{
		_dot.Dispose();
		_panel.Dispose();
		_keypad.Dispose();
	}
}
