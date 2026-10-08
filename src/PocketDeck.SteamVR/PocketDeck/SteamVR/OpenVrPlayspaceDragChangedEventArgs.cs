using System;

namespace PocketDeck.SteamVR;

public sealed class OpenVrPlayspaceDragChangedEventArgs(OpenVrPlayspaceDragSnapshot snapshot) : EventArgs
{
	public OpenVrPlayspaceDragSnapshot Snapshot { get; } = snapshot;
}
