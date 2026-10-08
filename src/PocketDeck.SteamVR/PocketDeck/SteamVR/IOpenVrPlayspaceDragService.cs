using System;

namespace PocketDeck.SteamVR;

public interface IOpenVrPlayspaceDragService : IDisposable
{
	OpenVrPlayspaceDragSnapshot Snapshot { get; }

	event EventHandler<OpenVrPlayspaceDragChangedEventArgs>? StateChanged;

	event EventHandler<OpenVrPlayspaceDragDiagnosticEventArgs>? DiagnosticRecorded;

	void Start();

	void StopService();

	void SetEnabled(bool enabled);

	void SetMultiplier(float multiplier);

	void SetPhoneControllerHand(OpenVrControllerHand hand);
}
