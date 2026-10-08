using System;

namespace PocketDeck.Session;

public sealed class PhoneOverlayChangedEventArgs(PhoneOverlaySnapshot snapshot) : EventArgs
{
	public PhoneOverlaySnapshot Snapshot { get; } = snapshot;
}
