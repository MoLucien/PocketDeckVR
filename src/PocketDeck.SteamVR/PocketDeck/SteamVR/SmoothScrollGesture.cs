using System;
using PocketDeck.Contracts;

namespace PocketDeck.SteamVR;

internal sealed class SmoothScrollGesture
{
	private const float _deadzone = 0.28f;

	private const float _minimumY = 0.08f;

	private const float _maximumY = 0.92f;

	private const float _minimumSpeed = 0.18f;

	private const float _linearSpeedRange = 0.82f;

	private const float _fullStrengthBoost = 0.8f;

	private const long _moveIntervalMilliseconds = 12L;

	private const long _restartDelayMilliseconds = 20L;

	private bool _active;

	private float _x;

	private float _y;

	private long _lastMoveTimestamp;

	private long _nextStartTimestamp;

	public bool Active => _active;

	public int Update(float axisY, bool enabled, float pointerX, float pointerY, long timestamp, Span<SmoothScrollCommand> commands)
	{
		if (!enabled || !float.IsFinite(axisY) || MathF.Abs(axisY) < 0.28f)
		{
			return Stop(commands);
		}
		if (!_active)
		{
			if (timestamp < _nextStartTimestamp)
			{
				return 0;
			}
			_active = true;
			_x = Math.Clamp(pointerX, 0f, 1f);
			_y = Math.Clamp(pointerY, 0.25f, 0.75f);
			_lastMoveTimestamp = timestamp;
			commands[0] = new SmoothScrollCommand(PhoneInputCommandKind.PointerDown, _x, _y);
			return 1;
		}
		checked
		{
			long num = timestamp - _lastMoveTimestamp;
			if (num < 12)
			{
				return 0;
			}
			float num2 = Math.Clamp((float)num / 1000f, 0.001f, 0.05f);
			float strength = Math.Clamp((MathF.Abs(axisY) - 0.28f) / 0.72f, 0f, 1f);
			float x = CalculateSpeed(strength);
			float num3 = _y + MathF.CopySign(x, axisY) * num2;
			_lastMoveTimestamp = timestamp;
			if ((num3 < 0.08f || num3 > 0.92f) ? true : false)
			{
				_y = Math.Clamp(num3, 0.08f, 0.92f);
				commands[0] = new SmoothScrollCommand(PhoneInputCommandKind.PointerUp, _x, _y);
				_active = false;
				_nextStartTimestamp = timestamp + 20;
				return 1;
			}
			_y = num3;
			commands[0] = new SmoothScrollCommand(PhoneInputCommandKind.PointerMove, _x, _y);
			return 1;
		}
	}

	public int Stop(Span<SmoothScrollCommand> commands)
	{
		return Finish(PhoneInputCommandKind.PointerUp, commands);
	}

	public int Cancel(Span<SmoothScrollCommand> commands)
	{
		return Finish(PhoneInputCommandKind.PointerCancel, commands);
	}

	internal static float CalculateSpeed(float strength)
	{
		float num = Math.Clamp(strength, 0f, 1f);
		return 0.18f + 0.82f * num + 0.8f * num * num;
	}

	private int Finish(PhoneInputCommandKind kind, Span<SmoothScrollCommand> commands)
	{
		if (!_active)
		{
			return 0;
		}
		commands[0] = new SmoothScrollCommand(kind, _x, _y);
		_active = false;
		_nextStartTimestamp = 0L;
		return 1;
	}
}
