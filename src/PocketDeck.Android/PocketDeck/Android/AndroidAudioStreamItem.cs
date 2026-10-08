using System;
using System.Threading;

namespace PocketDeck.Android;

public sealed class AndroidAudioStreamItem : IDisposable
{
	private IDisposable? _owner;

	public AndroidAudioStreamItemKind Kind { get; }

	public long? PresentationTimeMicroseconds { get; }

	public ReadOnlyMemory<byte> Payload { get; private set; }

	internal AndroidAudioStreamItem(AndroidAudioStreamItemKind kind, long? presentationTimeMicroseconds, ReadOnlyMemory<byte> payload, IDisposable owner)
	{
		Kind = kind;
		PresentationTimeMicroseconds = presentationTimeMicroseconds;
		Payload = payload;
		_owner = owner;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _owner, null)?.Dispose();
		Payload = ReadOnlyMemory<byte>.Empty;
	}
}
