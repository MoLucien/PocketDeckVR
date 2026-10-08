using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrTransformMath
{
	public static HmdMatrix34_t Identity()
	{
		return new HmdMatrix34_t
		{
			m0 = 1f,
			m5 = 1f,
			m10 = 1f
		};
	}

	public static HmdMatrix34_t Multiply(HmdMatrix34_t left, HmdMatrix34_t right)
	{
		return new HmdMatrix34_t
		{
			m0 = left.m0 * right.m0 + left.m1 * right.m4 + left.m2 * right.m8,
			m1 = left.m0 * right.m1 + left.m1 * right.m5 + left.m2 * right.m9,
			m2 = left.m0 * right.m2 + left.m1 * right.m6 + left.m2 * right.m10,
			m3 = left.m0 * right.m3 + left.m1 * right.m7 + left.m2 * right.m11 + left.m3,
			m4 = left.m4 * right.m0 + left.m5 * right.m4 + left.m6 * right.m8,
			m5 = left.m4 * right.m1 + left.m5 * right.m5 + left.m6 * right.m9,
			m6 = left.m4 * right.m2 + left.m5 * right.m6 + left.m6 * right.m10,
			m7 = left.m4 * right.m3 + left.m5 * right.m7 + left.m6 * right.m11 + left.m7,
			m8 = left.m8 * right.m0 + left.m9 * right.m4 + left.m10 * right.m8,
			m9 = left.m8 * right.m1 + left.m9 * right.m5 + left.m10 * right.m9,
			m10 = left.m8 * right.m2 + left.m9 * right.m6 + left.m10 * right.m10,
			m11 = left.m8 * right.m3 + left.m9 * right.m7 + left.m10 * right.m11 + left.m11
		};
	}

	public static HmdMatrix34_t InverseRigid(HmdMatrix34_t value)
	{
		HmdMatrix34_t result = new HmdMatrix34_t
		{
			m0 = value.m0,
			m1 = value.m4,
			m2 = value.m8,
			m4 = value.m1,
			m5 = value.m5,
			m6 = value.m9,
			m8 = value.m2,
			m9 = value.m6,
			m10 = value.m10
		};
		result.m3 = 0f - (result.m0 * value.m3 + result.m1 * value.m7 + result.m2 * value.m11);
		result.m7 = 0f - (result.m4 * value.m3 + result.m5 * value.m7 + result.m6 * value.m11);
		result.m11 = 0f - (result.m8 * value.m3 + result.m9 * value.m7 + result.m10 * value.m11);
		return result;
	}

	public static HmdMatrix34_t Smooth(HmdMatrix34_t current, HmdMatrix34_t target, float elapsedSeconds)
	{
		float num = 1f - MathF.Exp((0f - elapsedSeconds) / 0.025f);
		current.m3 += (target.m3 - current.m3) * num;
		current.m7 += (target.m7 - current.m7) * num;
		current.m11 += (target.m11 - current.m11) * num;
		float amount = 1f - MathF.Exp((0f - elapsedSeconds) / 0.04f);
		float x = Lerp(current.m0, target.m0, amount);
		float y = Lerp(current.m4, target.m4, amount);
		float z = Lerp(current.m8, target.m8, amount);
		Normalize(ref x, ref y, ref z);
		float num2 = Lerp(current.m1, target.m1, amount);
		float num3 = Lerp(current.m5, target.m5, amount);
		float num4 = Lerp(current.m9, target.m9, amount);
		float num5 = x * num2 + y * num3 + z * num4;
		num2 -= num5 * x;
		num3 -= num5 * y;
		num4 -= num5 * z;
		Normalize(ref num2, ref num3, ref num4);
		current.m0 = x;
		current.m4 = y;
		current.m8 = z;
		current.m1 = num2;
		current.m5 = num3;
		current.m9 = num4;
		current.m2 = y * num4 - z * num3;
		current.m6 = z * num2 - x * num4;
		current.m10 = x * num3 - y * num2;
		return current;
	}

	private static float Lerp(float from, float to, float amount)
	{
		return from + (to - from) * amount;
	}

	private static void Normalize(ref float x, ref float y, ref float z)
	{
		float num = MathF.Sqrt(x * x + y * y + z * z);
		if (!(num < 1E-06f))
		{
			x /= num;
			y /= num;
			z /= num;
		}
	}
}
