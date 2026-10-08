namespace PocketDeck.Session;

public sealed record PhoneControlSnapshot(PhoneControlState State, string ReasonCode, string Message, string? DeviceKey, long SentCommands, long ReplacedPointerMoves, double LastQueueDelayMilliseconds);
