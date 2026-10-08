using System;
using PocketDeck.Contracts;

namespace PocketDeck.Core;

public sealed class AppRuntimeStateChangedEventArgs : EventArgs
{
	public AppRuntimeSnapshot Previous { get; }

	public AppRuntimeSnapshot Current { get; }

	public AppRuntimeStateChangedEventArgs(AppRuntimeSnapshot previous, AppRuntimeSnapshot current)
	{
		Previous = previous;
		Current = current;
	}
}
