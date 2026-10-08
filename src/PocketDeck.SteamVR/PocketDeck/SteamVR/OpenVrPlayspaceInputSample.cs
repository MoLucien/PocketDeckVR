using System.Runtime.InteropServices;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrPlayspaceInputSample(bool DashboardVisible, bool DragPressed, bool ResetPressed, bool IsValid)
{
	public static OpenVrPlayspaceInputSample Read(CVRInput input, ulong leftDrag, ulong rightDrag, ulong resetOffsets, ulong inputSource, bool dashboard)
	{
		InputDigitalActionData_t pActionData = default;
		InputDigitalActionData_t pActionData2 = default;
		InputDigitalActionData_t pActionData3 = default;
		uint unActionDataSize = checked((uint)Marshal.SizeOf<InputDigitalActionData_t>());
		EVRInputError digitalActionData = input.GetDigitalActionData(leftDrag, ref pActionData, unActionDataSize, inputSource);
		EVRInputError digitalActionData2 = input.GetDigitalActionData(rightDrag, ref pActionData2, unActionDataSize, inputSource);
		EVRInputError digitalActionData3 = input.GetDigitalActionData(resetOffsets, ref pActionData3, unActionDataSize, inputSource);
		return new OpenVrPlayspaceInputSample(dashboard, (pActionData.bActive && pActionData.bState) || (pActionData2.bActive && pActionData2.bState), pActionData3.bActive && pActionData3.bState, digitalActionData == EVRInputError.None && digitalActionData2 == EVRInputError.None && digitalActionData3 == EVRInputError.None && (pActionData.bActive || pActionData2.bActive || pActionData3.bActive));
	}
}
