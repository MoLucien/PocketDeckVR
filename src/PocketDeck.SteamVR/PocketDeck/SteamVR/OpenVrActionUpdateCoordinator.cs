using System;
using System.Threading;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrActionUpdateCoordinator
{
	private sealed class PhonePollerLease(OpenVrActionUpdateCoordinator owner) : IDisposable
	{
		private OpenVrActionUpdateCoordinator? _owner = owner;

		public void Dispose()
		{
			Interlocked.Exchange(ref _owner, null)?.ReleasePhonePoller();
		}
	}

	private readonly object _gate = new object();

	private bool _phonePollerActive;

	private bool _playspaceEnabled;

	private OpenVrPlayspaceInputSample _inputSample;

	private long _inputRevision;

	private float _multiplier = 1f;

	private OpenVrPlayspaceControlRequest? _pendingControls;

	private long _coalescedControls;

	public long CoalescedControls
	{
		get
		{
			lock (_gate)
			{
				return _coalescedControls;
			}
		}
	}

	public void RequestControls(bool enabled, float multiplier)
	{
		bool flag = !float.IsFinite(multiplier);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((multiplier < 1f || multiplier > 40f) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			throw new ArgumentOutOfRangeException("multiplier");
		}
		checked
		{
			lock (_gate)
			{
				if ((object)_pendingControls != null)
				{
					_coalescedControls++;
				}
				_pendingControls = new OpenVrPlayspaceControlRequest(enabled, multiplier);
			}
		}
	}

	public OpenVrPlayspaceControlRequest? TakeControls()
	{
		lock (_gate)
		{
			OpenVrPlayspaceControlRequest pendingControls = _pendingControls;
			_pendingControls = null;
			return pendingControls;
		}
	}

	public void SetPlayspaceMultiplier(float multiplier)
	{
		lock (_gate)
		{
			_multiplier = multiplier;
			_pendingControls = null;
		}
	}

	public IDisposable AcquirePhonePoller()
	{
		lock (_gate)
		{
			if (_phonePollerActive)
			{
				throw new InvalidOperationException("Only one SteamVR phone action-state poller may be active.");
			}
			_phonePollerActive = true;
			_inputSample = default;
			return new PhonePollerLease(this);
		}
	}

	public void SetPlayspaceEnabled(bool enabled)
	{
		lock (_gate)
		{
			_playspaceEnabled = enabled;
			_pendingControls = null;
		}
	}

	public void PublishPlayspaceInput(OpenVrPlayspaceInputSample inputSample)
	{
		checked
		{
			lock (_gate)
			{
				if (_phonePollerActive)
				{
					_inputSample = inputSample;
					_inputRevision++;
				}
			}
		}
	}

	public OpenVrSharedPlayspaceInput Read()
	{
		lock (_gate)
		{
			return new OpenVrSharedPlayspaceInput(_phonePollerActive, _playspaceEnabled, _playspaceEnabled && _inputSample.IsValid && !_inputSample.DashboardVisible && _inputSample.DragPressed, _inputSample.IsValid && !_inputSample.DashboardVisible && _inputSample.ResetPressed, _multiplier, _inputSample, _inputRevision);
		}
	}

	private void ReleasePhonePoller()
	{
		lock (_gate)
		{
			_phonePollerActive = false;
			_inputSample = default;
			_pendingControls = null;
		}
	}
}
