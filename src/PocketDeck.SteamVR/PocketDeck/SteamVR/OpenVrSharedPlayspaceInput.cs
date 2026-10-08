namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrSharedPlayspaceInput(bool PhonePollerActive, bool PlayspaceEnabled, bool DragPressed, bool ResetPressed, float Multiplier = 1f, OpenVrPlayspaceInputSample InputSample = default(OpenVrPlayspaceInputSample), long InputRevision = 0L);
