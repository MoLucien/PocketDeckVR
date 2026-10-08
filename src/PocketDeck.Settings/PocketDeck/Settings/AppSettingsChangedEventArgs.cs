using System;

namespace PocketDeck.Settings;

public sealed class AppSettingsChangedEventArgs(AppSettingsSnapshot snapshot) : EventArgs
{
	public AppSettingsSnapshot Snapshot { get; } = snapshot;
}
