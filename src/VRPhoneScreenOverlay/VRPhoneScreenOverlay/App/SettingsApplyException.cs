using System;

namespace PocketDeck.App;

internal sealed class SettingsApplyException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
