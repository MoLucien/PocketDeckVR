using System;

namespace PocketDeck.Session;

public sealed class PhoneMediaSessionChangedEventArgs(PhoneMediaSessionSnapshot snapshot) : EventArgs
{
	public PhoneMediaSessionSnapshot Snapshot { get; } = snapshot;
}
