using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace PocketDeck.Media;

internal sealed class WasapiAudioSink(BufferedWaveProvider buffer, WasapiPlayer player) : IAudioSink, IAsyncDisposable
{
	private readonly BufferedWaveProvider _buffer = buffer;

	private readonly WasapiPlayer _player = player;

	private bool _disposed;

	public string OutputDeviceId => _player.DeviceId ?? "windows-default";

	public TimeSpan BufferedDuration => _buffer.BufferedDuration;

	public static async ValueTask<WasapiAudioSink> CreateAsync()
	{
		BufferedWaveProvider buffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 2), TimeSpan.FromMilliseconds(200L))
		{
			DiscardOnBufferOverflow = true,
			ReadFully = true
		};
		WasapiPlayer wasapiPlayer = await new WasapiPlayerBuilder().WithDefaultDeviceStreamRouting().WithLatency(60).WithEventSync()
			.BuildAsync()
			.ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			wasapiPlayer.Init(buffer);
			wasapiPlayer.Play();
			return new WasapiAudioSink(buffer, wasapiPlayer);
		}
		catch
		{
			await wasapiPlayer.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	public ValueTask WriteAsync(ReadOnlyMemory<short> interleavedPcm, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();
		_buffer.AddSamples(MemoryMarshal.AsBytes(interleavedPcm.Span));
		return ValueTask.CompletedTask;
	}

	public void Clear()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_buffer.ClearBuffer();
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _player.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
