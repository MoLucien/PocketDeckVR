using System;
using System.Collections.Generic;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrPlayspaceDragController(CVRSystem system, CVRChaperoneSetup chaperoneSetup, OpenVrControllerHand hand)
{
	private readonly CVRSystem _system = system;

	private readonly CVRChaperoneSetup _chaperoneSetup = chaperoneSetup;

	private readonly ETrackedControllerRole _role = OpenVrControllerHandRouting.TrackedRole(hand);

	private HmdMatrix34_t _basis;

	private bool _basisReady;

	private bool _dragTracking;

	private bool _previousReset;

	private bool _previewVisible;

	private float _offsetX;

	private float _offsetY;

	private float _offsetZ;

	private float _lastX;

	private float _lastY;

	private float _lastZ;

	public bool BasisReady => _basisReady;

	public float OffsetX => _offsetX;

	public float OffsetY => _offsetY;

	public float OffsetZ => _offsetZ;

	public OpenVrPlayspaceDragUpdate Update(bool dragPressed, bool resetPressed, float multiplier, IReadOnlyList<TrackedDevicePose_t> poses)
	{
		if (!_basisReady)
		{
			_basisReady = _chaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref _basis);
		}
		if (!_basisReady)
		{
			_dragTracking = false;
			_previousReset = resetPressed;
			return default;
		}
		if (resetPressed && !_previousReset)
		{
			ResetOffsets();
		}
		_previousReset = resetPressed;
		if (!dragPressed)
		{
			_dragTracking = false;
			return default;
		}
		uint trackedDeviceIndexForControllerRole = _system.GetTrackedDeviceIndexForControllerRole(_role);
		if (trackedDeviceIndexForControllerRole == uint.MaxValue || trackedDeviceIndexForControllerRole >= poses.Count)
		{
			_dragTracking = false;
			return default;
		}
		TrackedDevicePose_t trackedDevicePose_t = poses[checked((int)trackedDeviceIndexForControllerRole)];
		if (!trackedDevicePose_t.bPoseIsValid || !trackedDevicePose_t.bDeviceIsConnected || trackedDevicePose_t.eTrackingResult != ETrackingResult.Running_OK)
		{
			_dragTracking = false;
			return default;
		}
		float num = trackedDevicePose_t.mDeviceToAbsoluteTracking.m3 + _offsetX;
		float num2 = trackedDevicePose_t.mDeviceToAbsoluteTracking.m7 + _offsetY;
		float num3 = trackedDevicePose_t.mDeviceToAbsoluteTracking.m11 + _offsetZ;
		OpenVrPlayspaceDragUpdate result = default;
		if (_dragTracking)
		{
			var (num4, num5, num6) = CalculateDelta(num, num2, num3, _lastX, _lastY, _lastZ, 1f);
			var (num7, num8, num9) = CalculateDelta(num, num2, num3, _lastX, _lastY, _lastZ, multiplier);
			if (IsSafeDelta(num7, num8, num9))
			{
				_offsetX += num7;
				_offsetY += num8;
				_offsetZ += num9;
				ApplyOffsets();
				result = new OpenVrPlayspaceDragUpdate(OffsetApplied: true, VectorLength(num4, num5, num6), VectorLength(num7, num8, num9), _offsetX, _offsetY, _offsetZ, num4, num5, num6, num7, num8, num9);
			}
			else
			{
				ResetOffsets();
			}
		}
		_lastX = num;
		_lastY = num2;
		_lastZ = num3;
		_dragTracking = true;
		return result;
	}

	public void Restore()
	{
		_dragTracking = false;
		_previousReset = false;
		if (_basisReady && _previewVisible)
		{
			HmdMatrix34_t pMatStandingZeroPoseToRawTrackingPose = _basis;
			_chaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref pMatStandingZeroPoseToRawTrackingPose);
		}
		if (_previewVisible)
		{
			_chaperoneSetup.HideWorkingSetPreview();
		}
		_previewVisible = false;
		_basisReady = false;
		_offsetX = 0f;
		_offsetY = 0f;
		_offsetZ = 0f;
	}

	internal static (float X, float Y, float Z) CalculateDelta(float currentX, float currentY, float currentZ, float previousX, float previousY, float previousZ, float multiplier)
	{
		return (X: (currentX - previousX) * multiplier, Y: (currentY - previousY) * multiplier, Z: (currentZ - previousZ) * multiplier);
	}

	internal static bool IsSafeDelta(float x, float y, float z)
	{
		if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) && MathF.Abs(x) < 100f && MathF.Abs(y) < 100f)
		{
			return MathF.Abs(z) < 100f;
		}
		return false;
	}

	private static float VectorLength(float x, float y, float z)
	{
		return MathF.Sqrt(x * x + y * y + z * z);
	}

	internal static HmdMatrix34_t ApplyStandingOffset(HmdMatrix34_t basis, float offsetX, float offsetY, float offsetZ)
	{
		basis.m3 += basis.m0 * offsetX + basis.m1 * offsetY + basis.m2 * offsetZ;
		basis.m7 += basis.m4 * offsetX + basis.m5 * offsetY + basis.m6 * offsetZ;
		basis.m11 += basis.m8 * offsetX + basis.m9 * offsetY + basis.m10 * offsetZ;
		return basis;
	}

	private void ApplyOffsets()
	{
		HmdMatrix34_t pMatStandingZeroPoseToRawTrackingPose = ApplyStandingOffset(_basis, _offsetX, _offsetY, _offsetZ);
		_chaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref pMatStandingZeroPoseToRawTrackingPose);
		_chaperoneSetup.ShowWorkingSetPreview();
		_previewVisible = true;
	}

	private void ResetOffsets()
	{
		_offsetX = 0f;
		_offsetY = 0f;
		_offsetZ = 0f;
		_dragTracking = false;
		HmdMatrix34_t pMatStandingZeroPoseToRawTrackingPose = _basis;
		_chaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref pMatStandingZeroPoseToRawTrackingPose);
		_chaperoneSetup.ShowWorkingSetPreview();
		_previewVisible = true;
	}
}
