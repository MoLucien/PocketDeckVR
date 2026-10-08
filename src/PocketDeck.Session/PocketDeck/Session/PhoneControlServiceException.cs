using System;

namespace PocketDeck.Session;

public sealed class PhoneControlServiceException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
