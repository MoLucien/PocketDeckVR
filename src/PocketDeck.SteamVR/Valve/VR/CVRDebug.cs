using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRDebug
{
	private IVRDebug FnTable;

	internal CVRDebug(nint pInterface)
	{
		FnTable = (IVRDebug)Marshal.PtrToStructure(pInterface, typeof(IVRDebug));
	}

	public EVRDebugError EmitVrProfilerEvent(string pchMessage)
	{
		nint num = Utils.ToUtf8(pchMessage);
		EVRDebugError result = FnTable.EmitVrProfilerEvent(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRDebugError BeginVrProfilerEvent(ref ulong pHandleOut)
	{
		pHandleOut = 0uL;
		return FnTable.BeginVrProfilerEvent(ref pHandleOut);
	}

	public EVRDebugError FinishVrProfilerEvent(ulong hHandle, string pchMessage)
	{
		nint num = Utils.ToUtf8(pchMessage);
		EVRDebugError result = FnTable.FinishVrProfilerEvent(hHandle, num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint DriverDebugRequest(uint unDeviceIndex, string pchRequest, StringBuilder pchResponseBuffer, uint unResponseBufferSize)
	{
		nint num = Utils.ToUtf8(pchRequest);
		uint result = FnTable.DriverDebugRequest(unDeviceIndex, num, pchResponseBuffer, unResponseBufferSize);
		Marshal.FreeHGlobal(num);
		return result;
	}
}
