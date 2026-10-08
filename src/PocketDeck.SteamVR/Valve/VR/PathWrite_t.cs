namespace Valve.VR;

public struct PathWrite_t
{
	public ulong ulPath;

	public EPropertyWriteType writeType;

	public ETrackedPropertyError eSetError;

	public nint pvBuffer;

	public uint unBufferSize;

	public uint unTag;

	public ETrackedPropertyError eError;

	public nint pszPath;
}
