using System;

namespace PocketDeck.SteamVR;

public interface IOpenVrSceneOverlay : IDisposable
{
	bool IsVisible { get; }

	long GraphicsAdapterLuid { get; }

	OpenVrPhoneInteractionSnapshot Interaction { get; }

	OpenVrBindingHealthSnapshot BindingHealth { get; }

	event OpenVrPhoneInputSink? PhoneInputReceived;

	void ConfigureLockScreenFeatures(bool keepAwakeWhileGrabbed, bool unlockKeypadEnabled);

	void Submit(GpuVideoFrame frame);

	void SetPhoneLocked(bool? locked);

	OpenVrBindingResult OpenBindingUi();

	void Hide();
}
