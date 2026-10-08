using System;
using System.Diagnostics;
using System.Globalization;

namespace PocketDeck.Android;

internal sealed class AndroidVideoClockSynchronizer(long localTimestamp, long deviceMonotonicMicroseconds, TimeSpan roundTripDuration)
{
	private static readonly TimeSpan _maximumRoundTripDuration = TimeSpan.FromSeconds(1L);

	private readonly long _localTimestamp = localTimestamp;

	private readonly long _deviceMonotonicMicroseconds = deviceMonotonicMicroseconds;

	public TimeSpan RoundTripDuration { get; } = roundTripDuration;

	public long? EstimateLocalCaptureTimestamp(long presentationTimeMicroseconds)
	{
		if (presentationTimeMicroseconds <= 0 || RoundTripDuration < TimeSpan.Zero || RoundTripDuration > _maximumRoundTripDuration)
		{
			return null;
		}
		checked
		{
			double num = (double)(presentationTimeMicroseconds - _deviceMonotonicMicroseconds) * (double)Stopwatch.Frequency / 1000000.0;
			double num2 = (double)_localTimestamp + num;
			if (!double.IsFinite(num2) || num2 < -9.223372036854776E+18 || num2 > 9.223372036854776E+18)
			{
				return null;
			}
			return (long)Math.Round(num2, MidpointRounding.AwayFromZero);
		}
	}

	public static bool TryCreate(string deviceMonotonicSeconds, long commandStartedTimestamp, long commandCompletedTimestamp, out AndroidVideoClockSynchronizer? synchronizer)
	{
		synchronizer = null;
		if (commandCompletedTimestamp < commandStartedTimestamp)
		{
			return false;
		}
		if (!double.TryParse(deviceMonotonicSeconds, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result) || result <= 0.0 || result > 9223372036854.775)
		{
			return false;
		}
		checked
		{
			long localTimestamp = commandStartedTimestamp + unchecked(checked(commandCompletedTimestamp - commandStartedTimestamp) / 2);
			TimeSpan elapsedTime = Stopwatch.GetElapsedTime(commandStartedTimestamp, commandCompletedTimestamp);
			synchronizer = new AndroidVideoClockSynchronizer(localTimestamp, (long)Math.Round(result * 1000000.0, MidpointRounding.AwayFromZero), elapsedTime);
			return elapsedTime <= _maximumRoundTripDuration;
		}
	}
}
