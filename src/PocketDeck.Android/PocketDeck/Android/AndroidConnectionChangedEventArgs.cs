using System;

namespace PocketDeck.Android;

public sealed class AndroidConnectionChangedEventArgs(AndroidConnectionSnapshot snapshot) : EventArgs
{
	public AndroidConnectionSnapshot Snapshot { get; } = snapshot;
}
