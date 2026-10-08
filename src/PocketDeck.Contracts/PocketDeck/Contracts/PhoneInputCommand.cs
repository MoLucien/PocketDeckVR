using System;

namespace PocketDeck.Contracts;

public readonly record struct PhoneInputCommand(long Sequence, PhoneInputCommandKind Kind, long PointerId, float NormalizedX, float NormalizedY, int ScreenWidth, int ScreenHeight, DateTimeOffset CreatedAt, float ScrollDelta = 0f, int UnlockDigit = -1);
