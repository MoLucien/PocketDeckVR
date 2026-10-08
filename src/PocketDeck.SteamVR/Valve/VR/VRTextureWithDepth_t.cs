namespace Valve.VR;

public struct VRTextureWithDepth_t
{
	public nint handle;

	public ETextureType eType;

	public EColorSpace eColorSpace;

	public VRTextureDepthInfo_t depth;
}
