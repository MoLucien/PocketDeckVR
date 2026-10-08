namespace PocketDeck.Input;

public sealed record PhoneInputDispatcherSnapshot(long SentCommands, long ReplacedPointerMoves, int ReliableQueueDepth, double LastQueueDelayMilliseconds, bool IsFaulted);
