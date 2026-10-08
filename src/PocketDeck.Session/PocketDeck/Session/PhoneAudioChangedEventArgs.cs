using System;

namespace PocketDeck.Session;

public sealed class PhoneAudioChangedEventArgs(PhoneAudioSnapshot snapshot) : EventArgs
{
	public PhoneAudioSnapshot Snapshot { get; } = snapshot;
}
