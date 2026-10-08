using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRResources
{
	private IVRResources FnTable;

	internal CVRResources(nint pInterface)
	{
		FnTable = (IVRResources)Marshal.PtrToStructure(pInterface, typeof(IVRResources));
	}

	public uint LoadSharedResource(string pchResourceName, string pchBuffer, uint unBufferLen)
	{
		nint num = Utils.ToUtf8(pchResourceName);
		uint result = FnTable.LoadSharedResource(num, pchBuffer, unBufferLen);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetResourceFullPath(string pchResourceName, string pchResourceTypeDirectory, StringBuilder pchPathBuffer, uint unBufferLen)
	{
		nint num = Utils.ToUtf8(pchResourceName);
		nint num2 = Utils.ToUtf8(pchResourceTypeDirectory);
		uint result = FnTable.GetResourceFullPath(num, num2, pchPathBuffer, unBufferLen);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}
}
