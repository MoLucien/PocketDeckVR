using System;

namespace PocketDeck.Media;

public sealed class MediaDecoderException(string reasonCode, string message, Exception? inner = null) : Exception(message, inner)
{
	public string ReasonCode { get; } = reasonCode;
}
