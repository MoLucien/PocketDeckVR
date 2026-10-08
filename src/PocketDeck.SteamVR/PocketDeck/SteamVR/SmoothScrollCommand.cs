using PocketDeck.Contracts;

namespace PocketDeck.SteamVR;

internal readonly record struct SmoothScrollCommand(PhoneInputCommandKind Kind, float NormalizedX, float NormalizedY);
