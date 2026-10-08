namespace PocketDeck.SteamVR;

public sealed record OpenVrPhoneInteractionSnapshot(bool InputReady, bool WorldAnchored, bool Hovered, bool Grabbed, string ReasonCode, string Message, bool ControllerPoseValid = false, bool UnlockKeypadExpanded = false, string? DiagnosticExceptionType = null, string? DiagnosticExceptionMessage = null, string? HeadsetModel = null, string? ControllerType = null, bool PointerPoseBound = false, bool PointerPoseActive = false, bool TouchBound = false, bool TouchActive = false, bool GrabBound = false, bool GrabActive = false, bool ScaleBound = false, bool ScaleActive = false)
{
	public static OpenVrPhoneInteractionSnapshot Stopped { get; } = new OpenVrPhoneInteractionSnapshot(InputReady: false, WorldAnchored: false, Hovered: false, Grabbed: false, "OPENVR_INPUT_STOPPED", "SteamVR 手柄输入未运行");
}
