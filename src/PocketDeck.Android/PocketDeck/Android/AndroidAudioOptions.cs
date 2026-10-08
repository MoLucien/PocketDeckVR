namespace PocketDeck.Android;

public sealed record AndroidAudioOptions
{
	public AndroidAudioCodec Codec { get; init; }

	public bool PowerOnDevice { get; init; }
}
