using System;
using System.Buffers;
using System.Threading;

namespace PocketDeck.Media;

public sealed class DecodedAudioFrame : IDisposable
{
	private IMemoryOwner<short>? _owner;

	public int SampleRate { get; }

	public int Channels { get; }

	public int SamplesPerChannel { get; }

	public long PresentationTimeMicroseconds { get; }

	public ReadOnlyMemory<short> Samples => (_owner ?? throw new ObjectDisposedException("DecodedAudioFrame")).Memory.Slice(0, checked(SamplesPerChannel * Channels));

	public TimeSpan Duration => TimeSpan.FromSeconds((double)SamplesPerChannel / (double)SampleRate);

	internal DecodedAudioFrame(int sampleRate, int channels, int samplesPerChannel, long presentationTimeMicroseconds, IMemoryOwner<short> owner)
	{
		SampleRate = sampleRate;
		Channels = channels;
		SamplesPerChannel = samplesPerChannel;
		PresentationTimeMicroseconds = presentationTimeMicroseconds;
		_owner = owner;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _owner, null)?.Dispose();
	}
}
