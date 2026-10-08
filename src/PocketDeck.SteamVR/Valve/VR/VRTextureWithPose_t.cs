namespace Valve.VR;

public struct VRTextureWithPose_t
{
	public nint handle;

	public ETextureType eType;

	public EColorSpace eColorSpace;

	public HmdMatrix34_t mDeviceToAbsoluteTracking;
}
