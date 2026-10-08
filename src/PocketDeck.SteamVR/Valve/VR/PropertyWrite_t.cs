namespace Valve.VR;

public struct PropertyWrite_t
{
	public ETrackedDeviceProperty prop;

	public EPropertyWriteType writeType;

	public ETrackedPropertyError eSetError;

	public nint pvBuffer;

	public uint unBufferSize;

	public uint unTag;

	public ETrackedPropertyError eError;
}
