using System;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPointerSmoother
{
	private const float _deadZone = 0.0015f;

	private const float _slowTimeConstantSeconds = 0.045f;

	private const float _fastTimeConstantSeconds = 0.008f;

	private const float _fullSpeedDistance = 0.05f;

	private bool _initialized;

	private long _lastTimestamp;

	private VROverlayIntersectionResults_t _filtered;

	public VROverlayIntersectionResults_t Update(VROverlayIntersectionResults_t raw, long timestamp)
	{
		if (!_initialized)
		{
			_initialized = true;
			_lastTimestamp = timestamp;
			_filtered = raw;
			return raw;
		}
		float num = Math.Clamp((float)checked(timestamp - _lastTimestamp) / 1000f, 0.001f, 0.05f);
		_lastTimestamp = timestamp;
		float num2 = raw.vUVs.v0 - _filtered.vUVs.v0;
		float num3 = raw.vUVs.v1 - _filtered.vUVs.v1;
		float num4 = MathF.Sqrt(num2 * num2 + num3 * num3);
		if (num4 <= 0.0015f)
		{
			return _filtered;
		}
		float num5 = Math.Clamp((num4 - 0.0015f) / 0.0485f, 0f, 1f);
		float num6 = 0.045f + -0.037f * num5;
		float amount = 1f - MathF.Exp((0f - num) / num6);
		_filtered.vPoint.v0 = Lerp(_filtered.vPoint.v0, raw.vPoint.v0, amount);
		_filtered.vPoint.v1 = Lerp(_filtered.vPoint.v1, raw.vPoint.v1, amount);
		_filtered.vPoint.v2 = Lerp(_filtered.vPoint.v2, raw.vPoint.v2, amount);
		_filtered.vNormal = raw.vNormal;
		_filtered.vUVs.v0 = Lerp(_filtered.vUVs.v0, raw.vUVs.v0, amount);
		_filtered.vUVs.v1 = Lerp(_filtered.vUVs.v1, raw.vUVs.v1, amount);
		_filtered.fDistance = Lerp(_filtered.fDistance, raw.fDistance, amount);
		return _filtered;
	}

	public void Reset()
	{
		_initialized = false;
		_lastTimestamp = 0L;
		_filtered = default;
	}

	private static float Lerp(float from, float to, float amount)
	{
		return from + (to - from) * amount;
	}
}
