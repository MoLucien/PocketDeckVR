using System;

namespace PocketDeck.Session;

public sealed class PhoneControlChangedEventArgs(PhoneControlSnapshot snapshot) : EventArgs
{
	public PhoneControlSnapshot Snapshot { get; } = snapshot;
}
