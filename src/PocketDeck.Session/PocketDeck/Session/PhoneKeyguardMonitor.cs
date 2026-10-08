using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Android;

namespace PocketDeck.Session;

internal sealed class PhoneKeyguardMonitor(IAndroidKeyguardStateService keyguard, IAndroidConnectionLogSink log, TimeSpan? interval = null)
{
	public async Task RunAsync(string? deviceKey, Func<bool> enabled, Action<bool?> publish, CancellationToken cancellationToken)
	{
		string previousReason = null;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!enabled())
			{
				publish(null);
				previousReason = null;
			}
			else
			{
				AndroidKeyguardSnapshot androidKeyguardSnapshot;
				try
				{
					androidKeyguardSnapshot = await keyguard.ProbeAsync(deviceKey, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception)
				{
					androidKeyguardSnapshot = new AndroidKeyguardSnapshot(AndroidKeyguardState.Unknown, "ANDROID_KEYGUARD_PROBE_FAILED", "暂时无法读取手机锁屏状态");
				}
				bool? obj = ((!enabled()) ? ((bool?)null) : (androidKeyguardSnapshot.State switch
				{
					AndroidKeyguardState.Locked => (bool?)true, 
					AndroidKeyguardState.Unlocked => false, 
					_ => null, 
				}));
				publish(obj);
				if (androidKeyguardSnapshot.ReasonCode != previousReason)
				{
					previousReason = androidKeyguardSnapshot.ReasonCode;
					log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "phone_keyguard", androidKeyguardSnapshot.ReasonCode, androidKeyguardSnapshot.Message));
				}
			}
			await Task.Delay(interval ?? TimeSpan.FromMilliseconds(750L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
