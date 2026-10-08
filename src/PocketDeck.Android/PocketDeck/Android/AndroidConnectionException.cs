using System;

namespace PocketDeck.Android;

public sealed class AndroidConnectionException(string reasonCode, string message) : Exception(message)
{
	public string ReasonCode { get; } = reasonCode;
}
