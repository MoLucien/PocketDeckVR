using System;
using PocketDeck.Contracts;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPhoneMenuState
{
	private bool _previousPressed = true;

	private bool _sliderCaptured;

	private bool _ready;

	private bool _recalled;

	private bool _previousRecall = true;

	private bool? _locked;

	private bool _keypadExpanded;

	public bool PhoneHidden { get; private set; }

	public bool Expanded { get; private set; }

	public bool DashboardVisible { get; private set; }

	public bool KeypadEnabled { get; private set; }

	public int OpacityPercent { get; private set; } = 100;

	public int EnteredDigitCount { get; private set; }

	public int InputRevision { get; private set; }

	public bool SuppressPhoneTouch { get; private set; } = true;

	public bool ControlsVisible
	{
		get
		{
			if (_ready && !DashboardVisible)
			{
				if (PhoneHidden)
				{
					return _recalled;
				}
				return true;
			}
			return false;
		}
	}

	public bool PhoneVisible
	{
		get
		{
			if (_ready && !DashboardVisible)
			{
				return !PhoneHidden;
			}
			return false;
		}
	}

	public bool SidebarVisible
	{
		get
		{
			if (ControlsVisible)
			{
				return Expanded;
			}
			return false;
		}
	}

	public bool KeypadVisible
	{
		get
		{
			if (PhoneVisible && KeypadEnabled)
			{
				return _keypadExpanded;
			}
			return false;
		}
	}

	public bool IsSurfaceVisible(OpenVrPhoneSurface surface)
	{
		return surface switch
		{
			OpenVrPhoneSurface.Dot => ControlsVisible, 
			OpenVrPhoneSurface.Menu => SidebarVisible, 
			OpenVrPhoneSurface.Keypad => KeypadVisible, 
			_ => PhoneVisible, 
		};
	}

	public void SetLocked(bool? locked)
	{
		if (!KeypadEnabled)
		{
			_locked = null;
		}
		else if (locked.HasValue && locked != _locked)
		{
			_locked = locked;
			_keypadExpanded = locked.Value;
			EnteredDigitCount = 0;
		}
	}

	public bool ProcessRecall(bool pressed)
	{
		if (!_ready || DashboardVisible)
		{
			_previousRecall = true;
			return false;
		}
		bool flag = pressed && !_previousRecall;
		_previousRecall = pressed;
		if (!flag || !PhoneHidden)
		{
			return false;
		}
		_recalled = true;
		Expanded = true;
		ResetInput();
		checked
		{
			InputRevision++;
			return true;
		}
	}

	public void SetEnvironment(bool ready, bool dashboard, bool keypadEnabled)
	{
		checked
		{
			if (DashboardVisible != dashboard || _ready != ready)
			{
				ResetInput();
				InputRevision++;
			}
			_ready = ready;
			DashboardVisible = dashboard;
			KeypadEnabled = keypadEnabled;
			if (!keypadEnabled)
			{
				EnteredDigitCount = 0;
				_keypadExpanded = false;
				_locked = null;
			}
		}
	}

	public void ResetInput()
	{
		_previousPressed = true;
		_sliderCaptured = false;
		SuppressPhoneTouch = true;
	}

	public OpenVrMenuChange ProcessInput(bool validRay, bool insideMenu, OpenVrMenuTarget target, float sliderFraction, bool pressed, OpenVrSharedPlayspaceInput playspace)
	{
		if (!ControlsVisible || !validRay)
		{
			ResetInput();
			return default;
		}
		bool flag = pressed && !_previousPressed;
		_previousPressed = pressed;
		if (!pressed)
		{
			_sliderCaptured = false;
			SuppressPhoneTouch = false;
			return default;
		}
		if (!insideMenu)
		{
			if (flag && Expanded)
			{
				Expanded = false;
				DismissHiddenEntry();
				SuppressPhoneTouch = true;
				return new OpenVrMenuChange(Consumed: true);
			}
			return default;
		}
		checked
		{
			if (target == OpenVrMenuTarget.Opacity && Expanded && (flag || _sliderCaptured))
			{
				_sliderCaptured = true;
				SuppressPhoneTouch = true;
				int num = (float.IsFinite(sliderFraction) ? Math.Clamp((int)MathF.Round(sliderFraction * 100f), 0, 100) : OpacityPercent);
				bool opacityChanged = num != OpacityPercent;
				OpacityPercent = num;
				return new OpenVrMenuChange(Consumed: true, VisibilityChanged: false, opacityChanged);
			}
			if (!flag)
			{
				return default;
			}
			SuppressPhoneTouch = true;
			switch (target)
			{
			case OpenVrMenuTarget.Dot:
				Expanded = !Expanded;
				if (!Expanded)
				{
					DismissHiddenEntry();
				}
				return new OpenVrMenuChange(Consumed: true);
			case OpenVrMenuTarget.KeypadToggle:
				if ((Expanded || KeypadVisible) && KeypadEnabled && !PhoneHidden)
				{
					_keypadExpanded = !_keypadExpanded;
					EnteredDigitCount = 0;
					return new OpenVrMenuChange(Consumed: true, VisibilityChanged: false, OpacityChanged: false, null, _keypadExpanded ? new OpenVrPhoneInputCommand?(new OpenVrPhoneInputCommand(PhoneInputCommandKind.WakeScreen)) : ((OpenVrPhoneInputCommand?)null));
				}
				break;
			}
			bool flag2 = target >= OpenVrMenuTarget.Digit0 && target <= OpenVrMenuTarget.Confirm;
			if (!Expanded && !(KeypadVisible & flag2))
			{
				return default;
			}
			switch (target)
			{
			case OpenVrMenuTarget.PhoneToggle:
				PhoneHidden = !PhoneHidden;
				if (PhoneHidden)
				{
					Expanded = false;
					_recalled = false;
					_previousRecall = true;
					_keypadExpanded = false;
					EnteredDigitCount = 0;
				}
				InputRevision++;
				return new OpenVrMenuChange(Consumed: true, VisibilityChanged: true);
			case OpenVrMenuTarget.PlayspaceToggle:
				return new OpenVrMenuChange(Consumed: true, VisibilityChanged: false, OpacityChanged: false, new OpenVrPlayspaceControlRequest(!playspace.PlayspaceEnabled, playspace.Multiplier));
			case OpenVrMenuTarget.MultiplierDown:
			case OpenVrMenuTarget.MultiplierUp:
				return new OpenVrMenuChange(Consumed: true, VisibilityChanged: false, OpacityChanged: false, new OpenVrPlayspaceControlRequest(playspace.PlayspaceEnabled, NextMultiplier(playspace.Multiplier, target == OpenVrMenuTarget.MultiplierUp)));
			default:
				if (KeypadVisible && target >= OpenVrMenuTarget.Digit0 && target <= OpenVrMenuTarget.Confirm)
				{
					if (target <= OpenVrMenuTarget.Digit9)
					{
						EnteredDigitCount = Math.Min(16, EnteredDigitCount + 1);
					}
					else if (target == OpenVrMenuTarget.Backspace)
					{
						EnteredDigitCount = Math.Max(0, EnteredDigitCount - 1);
					}
					else
					{
						EnteredDigitCount = 0;
					}
					unchecked
					{
						return new OpenVrMenuChange(Consumed: true, VisibilityChanged: false, OpacityChanged: false, null, target switch
						{
							OpenVrMenuTarget.Backspace => new OpenVrPhoneInputCommand(PhoneInputCommandKind.UnlockBackspace), 
							OpenVrMenuTarget.Confirm => new OpenVrPhoneInputCommand(PhoneInputCommandKind.UnlockConfirm), 
							_ => new OpenVrPhoneInputCommand(PhoneInputCommandKind.UnlockDigit, 0f, 0f, 0f, (int)checked(target - 8)), 
						});
					}
				}
				return new OpenVrMenuChange(Consumed: true);
			}
		}
	}

	private void DismissHiddenEntry()
	{
		checked
		{
			if (PhoneHidden)
			{
				_recalled = false;
				_keypadExpanded = false;
				EnteredDigitCount = 0;
				InputRevision++;
			}
		}
	}

	internal static int NextMultiplier(float current, bool increase)
	{
		ReadOnlySpan<int> readOnlySpan = new int[5] { 1, 5, 10, 20, 40 };
		if (increase)
		{
			ReadOnlySpan<int> readOnlySpan2 = readOnlySpan;
			for (int i = 0; i < readOnlySpan2.Length; i++)
			{
				int num = readOnlySpan2[i];
				if ((float)num > current)
				{
					return num;
				}
			}
			return 40;
		}
		checked
		{
			for (int num2 = readOnlySpan.Length - 1; num2 >= 0; num2--)
			{
				if ((float)readOnlySpan[num2] < current)
				{
					return readOnlySpan[num2];
				}
			}
			return 1;
		}
	}
}
