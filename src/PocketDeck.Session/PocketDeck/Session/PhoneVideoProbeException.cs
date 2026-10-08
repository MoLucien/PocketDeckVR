using System;

namespace PocketDeck.Session;

public sealed class PhoneVideoProbeException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
