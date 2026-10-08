using System;

namespace PocketDeck.Session;

public sealed class PhoneOverlayMetrics
{
	private long _framesAtLastSample;

	private long _encodedBytesAtLastSample;

	private double _totalLatencyMilliseconds;

	private long _latencySampleCount;

	public long SubmittedFrames { get; private set; }

	public long TotalEncodedBytes { get; private set; }

	public double FramesPerSecond { get; private set; }

	public double AverageBitrateMbps { get; private set; }

	public double PeakBitrateMbps { get; private set; }

	public double AverageLatencyMilliseconds { get; private set; }

	public void RecordSubmittedFrame(double? latencyMilliseconds)
	{
		checked
		{
			SubmittedFrames++;
			double valueOrDefault = default;
			int num;
			if (latencyMilliseconds.HasValue)
			{
				valueOrDefault = latencyMilliseconds.GetValueOrDefault();
				num = ((1 == 0) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			bool flag = unchecked((byte)num) != 0;
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3 = ((valueOrDefault < 0.0 || valueOrDefault > 5000.0) ? true : false);
				flag2 = flag3;
			}
			if (!flag2)
			{
				_totalLatencyMilliseconds += valueOrDefault;
				_latencySampleCount++;
				AverageLatencyMilliseconds = _totalLatencyMilliseconds / (double)_latencySampleCount;
			}
		}
	}

	public bool TrySampleThroughput(TimeSpan sinceLastSample, TimeSpan sinceStart, long totalEncodedBytes)
	{
		double totalSeconds = sinceLastSample.TotalSeconds;
		if (totalSeconds < 1.0)
		{
			return false;
		}
		TotalEncodedBytes = totalEncodedBytes;
		checked
		{
			long num = Math.Max(0L, totalEncodedBytes - _encodedBytesAtLastSample);
			FramesPerSecond = (double)(SubmittedFrames - _framesAtLastSample) / totalSeconds;
			double val = (double)num * 8.0 / totalSeconds / 1000000.0;
			PeakBitrateMbps = Math.Max(PeakBitrateMbps, val);
			AverageBitrateMbps = (double)totalEncodedBytes * 8.0 / Math.Max(sinceStart.TotalSeconds, 0.001) / 1000000.0;
			_encodedBytesAtLastSample = totalEncodedBytes;
			_framesAtLastSample = SubmittedFrames;
			return true;
		}
	}
}
