namespace PocketDeck.SteamVR;

public sealed record OpenVrPlayspaceDragSnapshot(OpenVrPlayspaceDragState State, bool Enabled, float Multiplier, string ReasonCode, string Message, float OffsetX = 0f, float OffsetY = 0f, float OffsetZ = 0f);
