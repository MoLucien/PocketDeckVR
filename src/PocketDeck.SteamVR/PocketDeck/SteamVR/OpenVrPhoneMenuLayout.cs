using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneMenuLayout
{
	public const int Width = 360;

	public const int Height = 640;

	public const float DotWidth = 0.048f;

	public const float PanelWidth = 0.32f;

	public const float PanelAspect = 0.5625f;

	public const int KeypadHeight = 320;

	public const float KeypadWidth = 0.26f;

	public const float KeypadAspect = 1.125f;

	public static HmdMatrix34_t DotTransform(bool hidden, HmdMatrix34_t phone, float width, float aspect, HmdMatrix34_t head)
	{
		HmdMatrix34_t right = OpenVrTransformMath.Identity();
		if (hidden)
		{
			right.m7 = 0.26f;
			right.m11 = -0.85f;
		}
		else
		{
			right.m3 = width / 2f + 0.024f;
			right.m7 = width / aspect / 2f - 0.024f;
		}
		return OpenVrTransformMath.Multiply(hidden ? head : phone, right);
	}

	public static HmdMatrix34_t PanelTransform(HmdMatrix34_t dot)
	{
		HmdMatrix34_t right = OpenVrTransformMath.Identity();
		right.m3 = 0.184f;
		right.m7 = -0.26044446f;
		return OpenVrTransformMath.Multiply(dot, right);
	}

	public static HmdMatrix34_t KeypadTransform(HmdMatrix34_t phone, float width, float aspect)
	{
		HmdMatrix34_t right = OpenVrTransformMath.Identity();
		right.m3 = width / 2f + 0.13f;
		right.m7 = (0f - width) / aspect / 2f + 0.115555555f;
		float num = width / aspect / 2f - 0.33422223f;
		right.m7 = Math.Min(right.m7, num - 0.115555555f - 0.008f);
		return OpenVrTransformMath.Multiply(phone, right);
	}

	public static bool InsideDot(float x, float y)
	{
		if (float.IsFinite(x) && float.IsFinite(y) && x >= 0f && x <= 1f)
		{
			if (y >= 0f)
			{
				return y <= 1f;
			}
			return false;
		}
		return false;
	}

	public static bool TryMap(float x, float y, bool keypad, out OpenVrMenuTarget target, out float fraction)
	{
		target = OpenVrMenuTarget.None;
		fraction = 0f;
		bool flag = !float.IsFinite(x) || !float.IsFinite(y);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((x < 0f || x > 1f) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || y < 0f || y * 640f > (float)(keypad ? 376 : 312))
		{
			return false;
		}
		float num = x * 360f;
		float num2 = y * 640f;
		if (num2 >= 16f && num2 < 76f)
		{
			target = OpenVrMenuTarget.PhoneToggle;
		}
		else if (num2 >= 124f && num2 <= 162f && num >= 24f && num <= 336f)
		{
			target = OpenVrMenuTarget.Opacity;
			fraction = (num - 24f) / 312f;
		}
		else if (num2 >= 174f && num2 < 234f)
		{
			target = OpenVrMenuTarget.PlayspaceToggle;
		}
		else if (num2 >= 242f && num2 < 306f && num >= 176f && num < 226f)
		{
			target = OpenVrMenuTarget.MultiplierDown;
		}
		else if (num2 >= 242f && num2 < 306f && num >= 290f && num <= 344f)
		{
			target = OpenVrMenuTarget.MultiplierUp;
		}
		else if (keypad && num2 >= 318f && num2 < 374f)
		{
			target = OpenVrMenuTarget.KeypadToggle;
		}
		return true;
	}

	public static bool TryMapKeypad(float x, float y, out OpenVrMenuTarget target)
	{
		target = OpenVrMenuTarget.None;
		if (!InsideDot(x, y))
		{
			return false;
		}
		float num = x * 360f;
		float num2 = y * 320f;
		if (num2 < 48f)
		{
			target = OpenVrMenuTarget.KeypadToggle;
		}
		else if (num2 >= 50f && num2 < 298f && num >= 20f && num < 340f)
		{
			int num3;
			int num4;
			int num5;
			checked
			{
				num3 = (int)((num - 20f) / 106.666664f);
				num4 = (int)((num2 - 50f) / 62f);
				num5 = num4 * 3 + num3 + 1;
			}
			OpenVrMenuTarget openVrMenuTarget = ((num4 != 3) ? ((OpenVrMenuTarget)checked(8 + num5)) : (num3 switch
			{
				0 => OpenVrMenuTarget.Backspace, 
				1 => OpenVrMenuTarget.Digit0, 
				_ => OpenVrMenuTarget.Confirm, 
			}));
			target = openVrMenuTarget;
		}
		return true;
	}

	public static bool TryIntersect(HmdMatrix34_t pointer, HmdMatrix34_t panel, float width, float aspect, out VROverlayIntersectionResults_t hit)
	{
		hit = default;
		float num = pointer.m2 * panel.m2 + pointer.m6 * panel.m6 + pointer.m10 * panel.m10;
		if (num > 0f)
		{
			return OpenVrPhoneRaycaster.TryIntersect(pointer, panel, width, aspect, out hit);
		}
		return false;
	}
}
