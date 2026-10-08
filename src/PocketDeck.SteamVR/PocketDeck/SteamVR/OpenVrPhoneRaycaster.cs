using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneRaycaster
{
	private const float _minimumDistanceMeters = 0.03f;

	private const float _maximumDistanceMeters = 5f;

	private const float _edgeToleranceMeters = 0.0001f;

	private const float _minimumAxisLength = 0.0001f;

	public static bool TryIntersect(HmdMatrix34_t pointer, HmdMatrix34_t overlayTransform, float overlayWidthMeters, float overlayAspectRatio, out VROverlayIntersectionResults_t hit)
	{
		hit = default;
		if (!float.IsFinite(overlayWidthMeters) || !float.IsFinite(overlayAspectRatio) || overlayWidthMeters <= 0f || overlayAspectRatio <= 0f)
		{
			return false;
		}
		float m = pointer.m3;
		float m2 = pointer.m7;
		float m3 = pointer.m11;
		float x = 0f - pointer.m2;
		float y = 0f - pointer.m6;
		float z = 0f - pointer.m10;
		if (!Normalize(ref x, ref y, ref z))
		{
			return false;
		}
		float x2 = overlayTransform.m0;
		float y2 = overlayTransform.m4;
		float z2 = overlayTransform.m8;
		if (!Normalize(ref x2, ref y2, ref z2))
		{
			return false;
		}
		float m4 = overlayTransform.m1;
		float m5 = overlayTransform.m5;
		float m6 = overlayTransform.m9;
		float num = x2 * m4 + y2 * m5 + z2 * m6;
		m4 -= num * x2;
		m5 -= num * y2;
		m6 -= num * z2;
		if (!Normalize(ref m4, ref m5, ref m6))
		{
			return false;
		}
		float x3 = y2 * m6 - z2 * m5;
		float y3 = z2 * m4 - x2 * m6;
		float z3 = x2 * m5 - y2 * m4;
		if (!Normalize(ref x3, ref y3, ref z3))
		{
			return false;
		}
		float num2 = x * x3 + y * y3 + z * z3;
		if (MathF.Abs(num2) < 0.0001f)
		{
			return false;
		}
		float m7 = overlayTransform.m3;
		float m8 = overlayTransform.m7;
		float m9 = overlayTransform.m11;
		float num3 = ((m7 - m) * x3 + (m8 - m2) * y3 + (m9 - m3) * z3) / num2;
		if ((num3 <= 0.03f || num3 >= 5f) ? true : false)
		{
			return false;
		}
		float num4 = m + x * num3;
		float num5 = m2 + y * num3;
		float num6 = m3 + z * num3;
		float num7 = num4 - m7;
		float num8 = num5 - m8;
		float num9 = num6 - m9;
		float num10 = num7 * x2 + num8 * y2 + num9 * z2;
		float num11 = num7 * m4 + num8 * m5 + num9 * m6;
		float num12 = overlayWidthMeters / overlayAspectRatio;
		float num13 = overlayWidthMeters * 0.5f;
		float num14 = num12 * 0.5f;
		if (MathF.Abs(num10) > num13 + 0.0001f || MathF.Abs(num11) > num14 + 0.0001f)
		{
			return false;
		}
		float num15 = MathF.Sqrt((m7 - m) * (m7 - m) + (m8 - m2) * (m8 - m2) + (m9 - m3) * (m9 - m3));
		float num16 = MathF.Sqrt(num13 * num13 + num14 * num14);
		float num17 = MathF.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
		if (!float.IsFinite(num17) || num17 > num16 + 0.0001f || num3 > num15 + num16 + 0.0001f)
		{
			return false;
		}
		hit.vPoint.v0 = num4;
		hit.vPoint.v1 = num5;
		hit.vPoint.v2 = num6;
		hit.vNormal.v0 = x3;
		hit.vNormal.v1 = y3;
		hit.vNormal.v2 = z3;
		hit.vUVs.v0 = Math.Clamp(num10 / overlayWidthMeters + 0.5f, 0f, 1f);
		hit.vUVs.v1 = Math.Clamp(num11 / num12 + 0.5f, 0f, 1f);
		hit.fDistance = num3;
		return true;
	}

	private static bool Normalize(ref float x, ref float y, ref float z)
	{
		float num = MathF.Sqrt(x * x + y * y + z * z);
		if (!float.IsFinite(num) || num < 0.0001f)
		{
			return false;
		}
		x /= num;
		y /= num;
		z /= num;
		return true;
	}
}
