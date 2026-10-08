using System.Runtime.InteropServices;

namespace Valve.VR;

public class CVRProperties
{
	private IVRProperties FnTable;

	internal CVRProperties(nint pInterface)
	{
		FnTable = (IVRProperties)Marshal.PtrToStructure(pInterface, typeof(IVRProperties));
	}

	public ETrackedPropertyError ReadPropertyBatch(ulong ulContainerHandle, ref PropertyRead_t pBatch, uint unBatchEntryCount)
	{
		return FnTable.ReadPropertyBatch(ulContainerHandle, ref pBatch, unBatchEntryCount);
	}

	public ETrackedPropertyError WritePropertyBatch(ulong ulContainerHandle, ref PropertyWrite_t pBatch, uint unBatchEntryCount)
	{
		return FnTable.WritePropertyBatch(ulContainerHandle, ref pBatch, unBatchEntryCount);
	}

	public string GetPropErrorNameFromEnum(ETrackedPropertyError error)
	{
		nint ptr = FnTable.GetPropErrorNameFromEnum(error);
		return Marshal.PtrToStringAnsi(ptr);
	}

	public ulong TrackedDeviceToPropertyContainer(uint nDevice)
	{
		return FnTable.TrackedDeviceToPropertyContainer(nDevice);
	}
}
