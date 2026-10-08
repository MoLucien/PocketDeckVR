using System;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrBindingHealthMonitor
{
	private const long _loadingNoticeDelayMilliseconds = 1000L;

	private const long _bindingProbeIntervalMilliseconds = 1000L;

	private const long _unboundGracePeriodMilliseconds = 10000L;

	private const int _failureThreshold = 3;

	private readonly object _gate = new object();

	private OpenVrBindingHealthSnapshot _snapshot = OpenVrBindingHealthSnapshot.Stopped;

	private long _startedAt;

	private long _nextProbeAt;

	private int _consecutiveFailures;

	private bool _explicitFailureObserved;

	public OpenVrBindingHealthSnapshot Snapshot
	{
		get
		{
			lock (_gate)
			{
				return _snapshot;
			}
		}
	}

	public void Begin(long now)
	{
		lock (_gate)
		{
			_startedAt = now;
			_nextProbeAt = now;
			_consecutiveFailures = 0;
			_explicitFailureObserved = false;
			_snapshot = new OpenVrBindingHealthSnapshot(OpenVrBindingHealthState.Loading, ShowNotice: false, "OPENVR_BINDING_LOADING", "正在加载 SteamVR 手柄绑定");
		}
	}

	public void Advance(long now)
	{
		lock (_gate)
		{
			if (_snapshot.State == OpenVrBindingHealthState.Loading && !_snapshot.ShowNotice && checked(now - _startedAt) >= 1000)
			{
				_snapshot = _snapshot with
				{
					ShowNotice = true
				};
			}
		}
	}

	public bool ShouldProbe(long now)
	{
		lock (_gate)
		{
			return _snapshot.State == OpenVrBindingHealthState.Loading && now >= _nextProbeAt;
		}
	}

	public void ObserveBindingLoadFailed(long now)
	{
		lock (_gate)
		{
			if (_snapshot.State == OpenVrBindingHealthState.Loading)
			{
				_explicitFailureObserved = true;
				_nextProbeAt = Math.Min(_nextProbeAt, now);
			}
		}
	}

	public void RecordInitializationFailure(long now)
	{
		lock (_gate)
		{
			if (_snapshot.State != OpenVrBindingHealthState.Ready)
			{
				_explicitFailureObserved = true;
				RecordFailedProbe(now);
			}
		}
	}

	public void RecordProbe(long now, bool controllerAvailable, bool requiredBindingsComplete)
	{
		checked
		{
			lock (_gate)
			{
				_nextProbeAt = now + 1000;
				if (requiredBindingsComplete)
				{
					_consecutiveFailures = 0;
					_explicitFailureObserved = false;
					_snapshot = new OpenVrBindingHealthSnapshot(OpenVrBindingHealthState.Ready, ShowNotice: false, "OPENVR_BINDING_READY", "SteamVR 手柄绑定已加载");
					return;
				}
				if (!controllerAvailable)
				{
					_consecutiveFailures = 0;
					return;
				}
				bool flag = now - _startedAt >= 10000;
				if (_explicitFailureObserved | flag)
				{
					RecordFailedProbe(now);
				}
			}
		}
	}

	private void RecordFailedProbe(long now)
	{
		checked
		{
			_nextProbeAt = now + 1000;
			_consecutiveFailures++;
			if (_consecutiveFailures >= 3)
			{
				_snapshot = new OpenVrBindingHealthSnapshot(OpenVrBindingHealthState.Failed, ShowNotice: true, "OPENVR_BINDING_LOAD_FAILED", "SteamVR 手柄绑定加载失败，手机射线和控制暂不可用");
			}
		}
	}
}
