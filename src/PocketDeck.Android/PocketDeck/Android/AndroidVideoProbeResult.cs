namespace PocketDeck.Android;

public sealed record AndroidVideoProbeResult(AndroidVideoCodec Codec, int Width, int Height, int MediaPacketCount, long PayloadBytes, bool ConfigurationReceived, bool KeyFrameReceived);
