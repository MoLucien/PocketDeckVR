namespace PocketDeck.SteamVR;

public sealed record GpuVideoFrame(nint NativeTexturePointer, int Width, int Height, long PresentationTimeMicroseconds, int TextureSlot);
