namespace PocketDeck.Android;

public sealed record AndroidSessionFactories(IAndroidVideoSessionFactory Video, IAndroidAudioSessionFactory Audio, IAndroidControlSessionFactory Control);
