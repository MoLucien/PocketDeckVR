using System;
using System.Collections.Generic;

namespace PocketDeck.SteamVR;

public sealed record OpenVrBindingPreparationSnapshot(bool Prepared, string? Revision, OpenVrControllerHand ControllerHand, IReadOnlyList<OpenVrBindingProfileDiagnostic> Profiles)
{
	public static OpenVrBindingPreparationSnapshot NotPrepared { get; } = new OpenVrBindingPreparationSnapshot(Prepared: false, null, OpenVrControllerHand.Right, Array.Empty<OpenVrBindingProfileDiagnostic>());
}
