using System;
using System.Diagnostics;
using System.Threading;

namespace PocketDeck.Contracts;

public readonly record struct OperationDeadline
{
	private readonly long _expiresAtTimestamp;

	public bool IsExpired => Remaining == TimeSpan.Zero;

	public TimeSpan Remaining
	{
		get
		{
			long num = checked(_expiresAtTimestamp - Stopwatch.GetTimestamp());
			if (num <= 0)
			{
				return TimeSpan.Zero;
			}
			return TimeSpan.FromSeconds((double)num / (double)Stopwatch.Frequency);
		}
	}

	private OperationDeadline(long expiresAtTimestamp)
	{
		_expiresAtTimestamp = expiresAtTimestamp;
	}

	public static OperationDeadline Start(TimeSpan timeout)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, "timeout");
		checked
		{
			long num = (long)Math.Ceiling(timeout.TotalSeconds * (double)Stopwatch.Frequency);
			return new OperationDeadline(Stopwatch.GetTimestamp() + num);
		}
	}

	public TimeSpan GetRemainingUpTo(TimeSpan maximum)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximum, TimeSpan.Zero, "maximum");
		TimeSpan remaining = Remaining;
		if (!(remaining < maximum))
		{
			return maximum;
		}
		return remaining;
	}

	public CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken)
	{
		CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		TimeSpan remaining = Remaining;
		if (remaining == TimeSpan.Zero)
		{
			cancellationTokenSource.Cancel();
		}
		else
		{
			cancellationTokenSource.CancelAfter(remaining);
		}
		return cancellationTokenSource;
	}
}
