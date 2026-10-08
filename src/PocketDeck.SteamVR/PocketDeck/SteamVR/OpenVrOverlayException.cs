using System;

namespace PocketDeck.SteamVR;

public sealed class OpenVrOverlayException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
