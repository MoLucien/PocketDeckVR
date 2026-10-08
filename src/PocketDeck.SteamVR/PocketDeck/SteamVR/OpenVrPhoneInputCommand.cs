using PocketDeck.Contracts;

namespace PocketDeck.SteamVR;

public readonly record struct OpenVrPhoneInputCommand(PhoneInputCommandKind Kind, float NormalizedX = 0f, float NormalizedY = 0f, float ScrollDelta = 0f, int UnlockDigit = -1);
