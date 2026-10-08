using PocketDeck.Android;
using PocketDeck.Media;

namespace PocketDeck.Session;

public sealed record PhoneVideoDecodeProbeResult(AndroidVideoCodec Codec, int Width, int Height, VideoPixelFormat PixelFormat, int Stride, int EncodedPacketCount, long EncodedPayloadBytes, int DecodedFrameBytes, string DecoderBackend, string GpuBackend, int TextureSlot);
