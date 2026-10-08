using System;
using System.Buffers;
using System.Threading.Tasks;
using Concentus;

namespace PocketDeck.Media;

internal sealed class OpusAudioDecoder : IAudioDecoder, IAsyncDisposable
{
	private const int _maximumSamplesPerChannel = 5760;

	private readonly IOpusDecoder _decoder;

	private bool _disposed;

	public MediaCodec Codec => MediaCodec.Opus;

	public int SampleRate { get; }

	public int Channels { get; }

	public OpusAudioDecoder(int sampleRate = 48000, int channels = 2)
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual(sampleRate, 48000, "sampleRate");
		if (channels != 1 && channels != 2)
		{
			throw new ArgumentOutOfRangeException("channels");
		}
		SampleRate = sampleRate;
		Channels = channels;
		_decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
	}

	public DecodedAudioFrame Decode(ReadOnlySpan<byte> encodedPacket, long presentationTimeMicroseconds)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (encodedPacket.IsEmpty)
		{
			throw new ArgumentException("Opus packet must not be empty.", "encodedPacket");
		}
		IMemoryOwner<short> memoryOwner = MemoryPool<short>.Shared.Rent(checked(5760 * Channels));
		try
		{
			int samplesPerChannel = _decoder.Decode(encodedPacket, memoryOwner.Memory.Span, 5760);
			DecodedAudioFrame result = new DecodedAudioFrame(SampleRate, Channels, samplesPerChannel, presentationTimeMicroseconds, memoryOwner);
			memoryOwner = null;
			return result;
		}
		catch (OpusException innerException)
		{
			throw new AudioPipelineException("AUDIO_OPUS_DECODE_FAILED", "Opus 音频包解码失败", innerException);
		}
		finally
		{
			memoryOwner?.Dispose();
		}
	}

	public ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			_decoder.Dispose();
		}
		return ValueTask.CompletedTask;
	}
}
