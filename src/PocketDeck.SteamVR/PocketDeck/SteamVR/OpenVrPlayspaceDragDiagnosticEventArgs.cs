using System;

namespace PocketDeck.SteamVR;

public sealed class OpenVrPlayspaceDragDiagnosticEventArgs(OpenVrPlayspaceDragDiagnostic diagnostic) : EventArgs
{
	public OpenVrPlayspaceDragDiagnostic Diagnostic { get; } = diagnostic;
}
