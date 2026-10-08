using System;

namespace PocketDeck.Session;

public sealed record PhoneAudioSnapshot(PhoneAudioState State, string ReasonCode, string Message, long DecodedPackets = 0L, long DroppedBuffers = 0L, TimeSpan BufferedDuration = default(TimeSpan), string OutputDeviceId = "");
