using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpyControlProtocolWriter(Stream stream)
{
	private const byte _injectKeycode = 0;

	private const byte _injectTouch = 2;

	private const byte _injectScroll = 3;

	private const byte _expandSettingsPanel = 6;

	private const byte _setDisplayPower = 10;

	private const byte _userActivity = 127;

	private const int _keyActionDown = 0;

	private const int _keyActionUp = 1;

	private const int _motionActionDown = 0;

	private const int _motionActionUp = 1;

	private const int _motionActionMove = 2;

	private const int _motionActionCancel = 3;

	private const int _keycodeHome = 3;

	private const int _keycodeBack = 4;

	private const int _keycodeScreenshot = 120;

	private const int _keycodeAppSwitch = 187;

	private const int _keycodeDigit0 = 7;

	private const int _keycodeEnter = 66;

	private const int _keycodeDelete = 67;

	private readonly Stream _stream = stream ?? throw new ArgumentNullException("stream");

	private readonly byte[] _buffer = new byte[32];

	public async ValueTask WriteAsync(PhoneInputCommand command, CancellationToken cancellationToken)
	{
		int length = Serialize(command, _buffer);
		await _stream.WriteAsync(_buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static int Serialize(PhoneInputCommand command, Span<byte> destination)
	{
		if (destination.Length < 32)
		{
			throw new ArgumentException("The destination must contain at least 32 bytes.", "destination");
		}
		return command.Kind switch
		{
			PhoneInputCommandKind.PointerDown => SerializeTouch(command, 0, ushort.MaxValue, destination), 
			PhoneInputCommandKind.PointerMove => SerializeTouch(command, 2, ushort.MaxValue, destination), 
			PhoneInputCommandKind.PointerUp => SerializeTouch(command, 1, 0, destination), 
			PhoneInputCommandKind.PointerCancel => SerializeTouch(command, 3, 0, destination), 
			PhoneInputCommandKind.Back => SerializeKeyPress(4, destination), 
			PhoneInputCommandKind.Home => SerializeKeyPress(3, destination), 
			PhoneInputCommandKind.RecentApps => SerializeKeyPress(187, destination), 
			PhoneInputCommandKind.OpenControlPanel => SerializeEmpty(6, destination), 
			PhoneInputCommandKind.Screenshot => SerializeKeyPress(120, destination), 
			PhoneInputCommandKind.Scroll => SerializeScroll(command, destination), 
			PhoneInputCommandKind.WakeScreen => SerializeDisplayPowerOn(destination), 
			PhoneInputCommandKind.UserActivity => SerializeEmpty(127, destination), 
			PhoneInputCommandKind.UnlockDigit => SerializeUnlockDigit(command, destination), 
			PhoneInputCommandKind.UnlockBackspace => SerializeKeyPress(67, destination), 
			PhoneInputCommandKind.UnlockConfirm => SerializeKeyPress(66, destination), 
			_ => throw new ArgumentOutOfRangeException("command", command.Kind, "Unknown phone input command."), 
		};
	}

	private static int SerializeUnlockDigit(PhoneInputCommand command, Span<byte> destination)
	{
		int unlockDigit = command.UnlockDigit;
		if ((unlockDigit < 0 || unlockDigit > 9) ? true : false)
		{
			throw new ArgumentOutOfRangeException("command", "Unlock digit must be between zero and nine.");
		}
		return SerializeKeyPress(checked(7 + command.UnlockDigit), destination);
	}

	private static int SerializeDisplayPowerOn(Span<byte> destination)
	{
		destination[0] = 10;
		destination[1] = 1;
		return 2;
	}

	private static int SerializeScroll(PhoneInputCommand command, Span<byte> destination)
	{
		ValidatePosition(command);
		if (!float.IsFinite(command.ScrollDelta))
		{
			throw new ArgumentOutOfRangeException("command", "Scroll delta must be finite.");
		}
		int value = MapCoordinate(command.NormalizedX, command.ScreenWidth);
		int value2 = MapCoordinate(command.NormalizedY, command.ScreenHeight);
		destination[0] = 3;
		BinaryPrimitives.WriteInt32BigEndian(destination.Slice(1, 4), value);
		BinaryPrimitives.WriteInt32BigEndian(destination.Slice(5, 4), value2);
		checked
		{
			BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(9, 2), (ushort)command.ScreenWidth);
			BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(11, 2), (ushort)command.ScreenHeight);
			BinaryPrimitives.WriteInt16BigEndian(destination.Slice(13, 2), 0);
			BinaryPrimitives.WriteInt16BigEndian(destination.Slice(15, 2), EncodeScroll(command.ScrollDelta));
			BinaryPrimitives.WriteInt32BigEndian(destination.Slice(17, 4), 0);
			return 21;
		}
	}

	private static int SerializeTouch(PhoneInputCommand command, int action, ushort pressure, Span<byte> destination)
	{
		ValidateTouch(command);
		int value = MapCoordinate(command.NormalizedX, command.ScreenWidth);
		int value2 = MapCoordinate(command.NormalizedY, command.ScreenHeight);
		destination[0] = 2;
		checked
		{
			destination[1] = (byte)action;
			BinaryPrimitives.WriteInt64BigEndian(destination.Slice(2, 8), command.PointerId);
			BinaryPrimitives.WriteInt32BigEndian(destination.Slice(10, 4), value);
			BinaryPrimitives.WriteInt32BigEndian(destination.Slice(14, 4), value2);
			BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(18, 2), (ushort)command.ScreenWidth);
			BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(20, 2), (ushort)command.ScreenHeight);
			BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(22, 2), pressure);
			BinaryPrimitives.WriteInt32BigEndian(destination.Slice(24, 4), 0);
			BinaryPrimitives.WriteInt32BigEndian(destination.Slice(28, 4), 0);
			return 32;
		}
	}

	private static int SerializeKeyPress(int keycode, Span<byte> destination)
	{
		SerializeKeyEvent(0, keycode, destination);
		SerializeKeyEvent(1, keycode, destination.Slice(14));
		return 28;
	}

	private static void SerializeKeyEvent(int action, int keycode, Span<byte> destination)
	{
		destination[0] = 0;
		destination[1] = checked((byte)action);
		BinaryPrimitives.WriteInt32BigEndian(destination.Slice(2, 4), keycode);
		BinaryPrimitives.WriteInt32BigEndian(destination.Slice(6, 4), 0);
		BinaryPrimitives.WriteInt32BigEndian(destination.Slice(10, 4), 0);
	}

	private static int SerializeEmpty(byte messageType, Span<byte> destination)
	{
		destination[0] = messageType;
		return 1;
	}

	private static void ValidateTouch(PhoneInputCommand command)
	{
		ValidatePosition(command);
	}

	private static void ValidatePosition(PhoneInputCommand command)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(command.ScreenWidth, 1, "command.ScreenWidth");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(command.ScreenWidth, 65535, "command.ScreenWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(command.ScreenHeight, 1, "command.ScreenHeight");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(command.ScreenHeight, 65535, "command.ScreenHeight");
		if (!float.IsFinite(command.NormalizedX) || !float.IsFinite(command.NormalizedY))
		{
			throw new ArgumentOutOfRangeException("command", "Pointer coordinates must be finite.");
		}
	}

	private static int MapCoordinate(float normalized, int dimension)
	{
		return checked((int)MathF.Round(Math.Clamp(normalized, 0f, 1f) * (float)(dimension - 1)));
	}

	private static short EncodeScroll(float value)
	{
		float num = Math.Clamp(value, -16f, 16f);
		if (num <= -16f)
		{
			return short.MinValue;
		}
		return checked((short)MathF.Round(num / 16f * 32767f));
	}
}
