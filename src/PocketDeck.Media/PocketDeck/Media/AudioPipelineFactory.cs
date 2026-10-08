using System.Threading.Tasks;

namespace PocketDeck.Media;

public static class AudioPipelineFactory
{
	public static IAudioDecoder CreateOpusDecoder()
	{
		return new OpusAudioDecoder();
	}

	public static ValueTask<IAudioSink> CreateDefaultWasapiSinkAsync()
	{
		return CreateDefaultWasapiSinkCoreAsync();
	}

	private static async ValueTask<IAudioSink> CreateDefaultWasapiSinkCoreAsync()
	{
		return await WasapiAudioSink.CreateAsync().ConfigureAwait(continueOnCapturedContext: false);
	}
}
