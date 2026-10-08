using System;

namespace PocketDeck.Android;

public sealed record AndroidConnectionLogEntry(DateTimeOffset Timestamp, string EventName, string ReasonCode, string Message, string? DeviceKey = null, AndroidConnectionState? State = null, int? DeviceCount = null, long? DurationMilliseconds = null, string? ToolVersion = null, string? DeviceModel = null, string? AndroidVersion = null, int? AndroidSdk = null, string? CpuAbi = null, int? VideoWidth = null, int? VideoHeight = null, long? PacketCount = null, long? PayloadBytes = null, long? CommandSequence = null, string? CommandKind = null, int? QueueDepth = null, long? ReplacedMoveCount = null, string? ExceptionType = null, string? ExceptionMessage = null);
