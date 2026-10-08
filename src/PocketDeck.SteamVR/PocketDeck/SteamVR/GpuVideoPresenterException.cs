using System;

namespace PocketDeck.SteamVR;

public sealed class GpuVideoPresenterException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
