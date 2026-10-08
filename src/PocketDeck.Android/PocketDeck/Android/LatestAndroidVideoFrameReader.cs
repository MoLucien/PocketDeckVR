using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

public sealed class LatestAndroidVideoFrameReader : IAsyncDisposable
{
	private const int _encodedFrameCapacity = 32;

	private readonly IAndroidVideoSession _session;

	private readonly CancellationTokenSource _stopSource;

	private readonly SemaphoreSlim _available = new SemaphoreSlim(0, 1);

	private readonly object _gate = new object();

	private readonly Task _producer;

	private readonly Queue<AndroidLatestVideoFrame> _frames = new Queue<AndroidLatestVideoFrame>(32);

	private ExceptionDispatchInfo? _failure;

	private bool _completed;

	private bool _disposed;

	private long _droppedFrames;

	private long _encodedPayloadBytes;

	public long DroppedFrames => Interlocked.Read(in _droppedFrames);

	public long EncodedPayloadBytes => Interlocked.Read(in _encodedPayloadBytes);

	public LatestAndroidVideoFrameReader(IAndroidVideoSession session, CancellationToken cancellationToken)
	{
		_session = session ?? throw new ArgumentNullException("session");
		_stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_producer = Task.Run(() => ProduceAsync(_stopSource.Token), CancellationToken.None);
	}

	public async ValueTask<AndroidLatestVideoFrame?> ReadLatestAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		bool completed;
		do
		{
			await _available.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			AndroidLatestVideoFrame androidLatestVideoFrame = null;
			ExceptionDispatchInfo failure;
			lock (_gate)
			{
				if (_frames.Count > 0)
				{
					androidLatestVideoFrame = _frames.Dequeue();
					if (_frames.Count > 0 || _completed || _failure != null)
					{
						SignalAvailable();
					}
				}
				failure = _failure;
				completed = _completed;
			}
			if (androidLatestVideoFrame != null)
			{
				return androidLatestVideoFrame;
			}
			failure?.Throw();
		}
		while (!completed);
		return null;
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _stopSource.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				await _producer.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
			{
			}
			lock (_gate)
			{
				ClearFrames();
			}
			_available.Dispose();
			_stopSource.Dispose();
		}
	}

	private async Task ProduceAsync(CancellationToken cancellationToken)
	{
		int width = 0;
		int height = 0;
		int generation = 0;
		byte[] configuration = null;
		bool waitingForKeyFrame = true;
		bool resynchronizing = false;
		checked
		{
			try
			{
				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();
					AndroidVideoStreamItem androidVideoStreamItem = await _session.ReadNextAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Session)
					{
						width = androidVideoStreamItem.Width;
						height = androidVideoStreamItem.Height;
						generation++;
						configuration = null;
						waitingForKeyFrame = true;
						resynchronizing = false;
						androidVideoStreamItem.Dispose();
						continue;
					}
					if (androidVideoStreamItem.Kind == AndroidVideoStreamItemKind.Configuration)
					{
						configuration = androidVideoStreamItem.Payload.ToArray();
						androidVideoStreamItem.Dispose();
						continue;
					}
					Interlocked.Add(ref _encodedPayloadBytes, androidVideoStreamItem.Payload.Length);
					if (width < 1 || height < 1 || configuration == null)
					{
						androidVideoStreamItem.Dispose();
						continue;
					}
					if (waitingForKeyFrame && !androidVideoStreamItem.IsKeyFrame)
					{
						androidVideoStreamItem.Dispose();
						Interlocked.Increment(ref _droppedFrames);
						continue;
					}
					if (waitingForKeyFrame)
					{
						if (resynchronizing)
						{
							generation++;
						}
						waitingForKeyFrame = false;
						resynchronizing = false;
					}
					AndroidLatestVideoFrame androidLatestVideoFrame = null;
					try
					{
						androidLatestVideoFrame = new AndroidLatestVideoFrame(generation, width, height, configuration, androidVideoStreamItem);
						androidVideoStreamItem = null;
						if (!TryEnqueue(androidLatestVideoFrame))
						{
							waitingForKeyFrame = true;
							resynchronizing = true;
						}
						androidLatestVideoFrame = null;
					}
					finally
					{
						androidLatestVideoFrame?.Dispose();
						androidVideoStreamItem?.Dispose();
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception source)
			{
				lock (_gate)
				{
					_failure = ExceptionDispatchInfo.Capture(source);
				}
			}
			finally
			{
				lock (_gate)
				{
					_completed = true;
					SignalAvailable();
				}
			}
		}
	}

	private bool TryEnqueue(AndroidLatestVideoFrame next)
	{
		lock (_gate)
		{
			if (_frames.Count >= 32)
			{
				int num = checked(_frames.Count + 1);
				ClearFrames();
				next.Dispose();
				Interlocked.Add(ref _droppedFrames, num);
				return false;
			}
			_frames.Enqueue(next);
			SignalAvailable();
			return true;
		}
	}

	private void ClearFrames()
	{
		AndroidLatestVideoFrame result;
		while (_frames.TryDequeue(out result))
		{
			result.Dispose();
		}
	}

	private void SignalAvailable()
	{
		if (_available.CurrentCount == 0)
		{
			_available.Release();
		}
	}
}
