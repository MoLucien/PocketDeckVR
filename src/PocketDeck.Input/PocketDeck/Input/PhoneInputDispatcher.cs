using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Input;

public sealed class PhoneInputDispatcher : IAsyncDisposable
{
	private sealed record QueuedCommand(PhoneInputCommand Command, TaskCompletionSource? Completion);

	public const int ReliableQueueCapacity = 64;

	private readonly object _gate = new object();

	private readonly Queue<QueuedCommand> _reliable = new Queue<QueuedCommand>(64);

	private readonly SemaphoreSlim _available = new SemaphoreSlim(0, 1);

	private readonly CancellationTokenSource _stopSource = new CancellationTokenSource();

	private readonly PhoneInputCommandSender _sender;

	private readonly Task _worker;

	private QueuedCommand? _latestMove;

	private PhoneInputCommand? _activePointer;

	private long _sentCommands;

	private long _replacedMoves;

	private double _lastQueueDelayMilliseconds;

	private Exception? _fault;

	private bool _stopping;

	private bool _disposed;

	public PhoneInputDispatcherSnapshot Snapshot
	{
		get
		{
			lock (_gate)
			{
				return new PhoneInputDispatcherSnapshot(_sentCommands, _replacedMoves, _reliable.Count, _lastQueueDelayMilliseconds, _fault != null);
			}
		}
	}

	public PhoneInputDispatcher(PhoneInputCommandSender sender)
	{
		_sender = sender ?? throw new ArgumentNullException("sender");
		_worker = RunAsync(_stopSource.Token);
	}

	public ValueTask EnqueueAsync(PhoneInputCommand command, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();
		QueuedCommand queuedCommand = new QueuedCommand(command, (command.Kind == PhoneInputCommandKind.PointerMove) ? null : new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
		checked
		{
			bool flag;
			lock (_gate)
			{
				if (_fault != null)
				{
					return ValueTask.FromException(_fault);
				}
				if (_stopping)
				{
					return ValueTask.FromException(new InvalidOperationException("The phone input dispatcher is stopping."));
				}
				flag = _reliable.Count == 0 && (object)_latestMove == null;
				if (command.Kind == PhoneInputCommandKind.PointerMove)
				{
					if ((object)_latestMove != null)
					{
						_replacedMoves++;
					}
					_latestMove = queuedCommand;
				}
				else
				{
					PhoneInputCommandKind kind = command.Kind;
					bool flag2 = unchecked((uint)(kind - 2)) <= 1u;
					if (flag2 && (object)_latestMove != null)
					{
						if (_reliable.Count < 63)
						{
							_reliable.Enqueue(_latestMove);
						}
						else
						{
							_replacedMoves++;
						}
						_latestMove = null;
					}
					if (_reliable.Count == 64)
					{
						return ValueTask.FromException(new InvalidOperationException("The reliable phone input queue is full."));
					}
					_reliable.Enqueue(queuedCommand);
				}
			}
			if (flag)
			{
				TryWake();
			}
			if (queuedCommand.Completion != null)
			{
				return new ValueTask(queuedCommand.Completion.Task.WaitAsync(cancellationToken));
			}
			return ValueTask.CompletedTask;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		lock (_gate)
		{
			_stopping = true;
			_latestMove = null;
			while (_reliable.Count > 0)
			{
				QueuedCommand queuedCommand = _reliable.Dequeue();
				queuedCommand.Completion?.TrySetCanceled();
			}
			PhoneInputCommand? activePointer = _activePointer;
			if (activePointer.HasValue)
			{
				PhoneInputCommand valueOrDefault = activePointer.GetValueOrDefault();
				PhoneInputCommand command = valueOrDefault with
				{
					Sequence = checked(valueOrDefault.Sequence + 1),
					Kind = PhoneInputCommandKind.PointerCancel,
					CreatedAt = DateTimeOffset.UtcNow
				};
				_reliable.Enqueue(new QueuedCommand(command, null));
			}
		}
		TryWake();
		if (await Task.WhenAny(_worker, Task.Delay(TimeSpan.FromSeconds(1L))).ConfigureAwait(continueOnCapturedContext: false) != _worker)
		{
			await _stopSource.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		try
		{
			await _worker.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
		}
		finally
		{
			_stopSource.Dispose();
			_available.Dispose();
		}
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			while (true)
			{
				await _available.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				QueuedCommand queued;
				while (TryTake(out queued))
				{
					try
					{
						await _sender(queued.Command, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						MarkSent(queued.Command);
						queued.Completion?.TrySetResult();
					}
					catch (Exception exception)
					{
						queued.Completion?.TrySetException(exception);
						Fault(exception);
						throw;
					}
				}
				lock (_gate)
				{
					if (_stopping && _reliable.Count == 0 && (object)_latestMove == null)
					{
						break;
					}
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private bool TryTake(out QueuedCommand queued)
	{
		lock (_gate)
		{
			if (_reliable.Count > 0)
			{
				queued = _reliable.Dequeue();
				return true;
			}
			if ((object)_latestMove != null)
			{
				queued = _latestMove;
				_latestMove = null;
				return true;
			}
			queued = null;
			return false;
		}
	}

	private void MarkSent(PhoneInputCommand command)
	{
		checked
		{
			lock (_gate)
			{
				_sentCommands++;
				_lastQueueDelayMilliseconds = Math.Max(0.0, (DateTimeOffset.UtcNow - command.CreatedAt).TotalMilliseconds);
				PhoneInputCommand? activePointer;
				switch (command.Kind)
				{
				case PhoneInputCommandKind.PointerDown:
				case PhoneInputCommandKind.PointerMove:
					activePointer = command;
					break;
				case PhoneInputCommandKind.PointerUp:
				case PhoneInputCommandKind.PointerCancel:
					activePointer = null;
					break;
				default:
					activePointer = _activePointer;
					break;
				}
				_activePointer = activePointer;
			}
		}
	}

	private void Fault(Exception exception)
	{
		lock (_gate)
		{
			_fault = exception;
			_stopping = true;
			_latestMove = null;
			while (_reliable.Count > 0)
			{
				_reliable.Dequeue().Completion?.TrySetException(exception);
			}
		}
	}

	private void TryWake()
	{
		try
		{
			_available.Release();
		}
		catch (SemaphoreFullException)
		{
		}
	}
}
