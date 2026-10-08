namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrPlayspaceDragUpdate(bool OffsetApplied, float SourceDistance, float AppliedDistance, float OffsetX, float OffsetY, float OffsetZ, float SourceDeltaX, float SourceDeltaY, float SourceDeltaZ, float AppliedDeltaX, float AppliedDeltaY, float AppliedDeltaZ);
