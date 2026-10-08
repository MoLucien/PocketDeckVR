using System;

namespace PocketDeck.Media;

public sealed class AudioPipelineException(string reasonCode, string message, Exception? innerException = null) : Exception(message, innerException)
{
	public string ReasonCode { get; } = reasonCode;
}
