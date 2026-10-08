using System;
using PocketDeck.SteamVR;

namespace PocketDeck.Session;

public sealed class PhoneOverlayLogGate
{
	private string? _lastInteractionReasonCode;

	private bool _lastWorldAnchored;

	private bool _lastGrabbed;

	private bool _lastHovered;

	private bool _lastControllerPoseValid;

	private string? _lastBindingReasonCode;

	private int _lastSubmittedWidth;

	private int _lastSubmittedHeight;

	private TimeSpan _lastQualityLogAt = TimeSpan.Zero;

	public bool ShouldLogInteraction(OpenVrPhoneInteractionSnapshot interaction)
	{
		ArgumentNullException.ThrowIfNull(interaction, "interaction");
		if (string.Equals(interaction.ReasonCode, _lastInteractionReasonCode, StringComparison.Ordinal) && interaction.WorldAnchored == _lastWorldAnchored && interaction.Grabbed == _lastGrabbed && interaction.Hovered == _lastHovered && interaction.ControllerPoseValid == _lastControllerPoseValid)
		{
			return false;
		}
		_lastInteractionReasonCode = interaction.ReasonCode;
		_lastWorldAnchored = interaction.WorldAnchored;
		_lastGrabbed = interaction.Grabbed;
		_lastHovered = interaction.Hovered;
		_lastControllerPoseValid = interaction.ControllerPoseValid;
		return true;
	}

	public bool ShouldLogBindingHealth(string reasonCode)
	{
		if (string.Equals(reasonCode, _lastBindingReasonCode, StringComparison.Ordinal))
		{
			return false;
		}
		_lastBindingReasonCode = reasonCode;
		return true;
	}

	public bool ShouldLogSubmittedSize(int width, int height)
	{
		if (width == _lastSubmittedWidth && height == _lastSubmittedHeight)
		{
			return false;
		}
		_lastSubmittedWidth = width;
		_lastSubmittedHeight = height;
		return true;
	}

	public bool ShouldLogQualitySample(TimeSpan elapsed, TimeSpan interval)
	{
		if (elapsed - _lastQualityLogAt < interval)
		{
			return false;
		}
		_lastQualityLogAt = elapsed;
		return true;
	}
}
