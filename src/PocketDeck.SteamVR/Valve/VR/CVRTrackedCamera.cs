using System.Runtime.InteropServices;

namespace Valve.VR;

public class CVRTrackedCamera
{
	private IVRTrackedCamera FnTable;

	internal CVRTrackedCamera(nint pInterface)
	{
		FnTable = (IVRTrackedCamera)Marshal.PtrToStructure(pInterface, typeof(IVRTrackedCamera));
	}

	public string GetCameraErrorNameFromEnum(EVRTrackedCameraError eCameraError)
	{
		nint ptr = FnTable.GetCameraErrorNameFromEnum(eCameraError);
		return Marshal.PtrToStringAnsi(ptr);
	}

	public EVRTrackedCameraError HasCamera(uint nDeviceIndex, ref bool pHasCamera)
	{
		pHasCamera = false;
		return FnTable.HasCamera(nDeviceIndex, ref pHasCamera);
	}

	public EVRTrackedCameraError GetCameraFrameSize(uint nDeviceIndex, EVRTrackedCameraFrameType eFrameType, ref uint pnWidth, ref uint pnHeight, ref uint pnFrameBufferSize)
	{
		pnWidth = 0u;
		pnHeight = 0u;
		pnFrameBufferSize = 0u;
		return FnTable.GetCameraFrameSize(nDeviceIndex, eFrameType, ref pnWidth, ref pnHeight, ref pnFrameBufferSize);
	}

	public EVRTrackedCameraError GetCameraIntrinsics(uint nDeviceIndex, uint nCameraIndex, EVRTrackedCameraFrameType eFrameType, ref HmdVector2_t pFocalLength, ref HmdVector2_t pCenter)
	{
		return FnTable.GetCameraIntrinsics(nDeviceIndex, nCameraIndex, eFrameType, ref pFocalLength, ref pCenter);
	}

	public EVRTrackedCameraError GetCameraProjection(uint nDeviceIndex, uint nCameraIndex, EVRTrackedCameraFrameType eFrameType, float flZNear, float flZFar, ref HmdMatrix44_t pProjection)
	{
		return FnTable.GetCameraProjection(nDeviceIndex, nCameraIndex, eFrameType, flZNear, flZFar, ref pProjection);
	}

	public EVRTrackedCameraError AcquireVideoStreamingService(uint nDeviceIndex, ref ulong pHandle)
	{
		pHandle = 0uL;
		return FnTable.AcquireVideoStreamingService(nDeviceIndex, ref pHandle);
	}

	public EVRTrackedCameraError ReleaseVideoStreamingService(ulong hTrackedCamera)
	{
		return FnTable.ReleaseVideoStreamingService(hTrackedCamera);
	}

	public EVRTrackedCameraError GetVideoStreamFrameBuffer(ulong hTrackedCamera, EVRTrackedCameraFrameType eFrameType, nint pFrameBuffer, uint nFrameBufferSize, ref CameraVideoStreamFrameHeader_t pFrameHeader, uint nFrameHeaderSize)
	{
		return FnTable.GetVideoStreamFrameBuffer(hTrackedCamera, eFrameType, pFrameBuffer, nFrameBufferSize, ref pFrameHeader, nFrameHeaderSize);
	}

	public EVRTrackedCameraError GetVideoStreamTextureSize(uint nDeviceIndex, EVRTrackedCameraFrameType eFrameType, ref VRTextureBounds_t pTextureBounds, ref uint pnWidth, ref uint pnHeight)
	{
		pnWidth = 0u;
		pnHeight = 0u;
		return FnTable.GetVideoStreamTextureSize(nDeviceIndex, eFrameType, ref pTextureBounds, ref pnWidth, ref pnHeight);
	}

	public EVRTrackedCameraError GetVideoStreamTextureD3D11(ulong hTrackedCamera, EVRTrackedCameraFrameType eFrameType, nint pD3D11DeviceOrResource, ref nint ppD3D11ShaderResourceView, ref CameraVideoStreamFrameHeader_t pFrameHeader, uint nFrameHeaderSize)
	{
		return FnTable.GetVideoStreamTextureD3D11(hTrackedCamera, eFrameType, pD3D11DeviceOrResource, ref ppD3D11ShaderResourceView, ref pFrameHeader, nFrameHeaderSize);
	}

	public EVRTrackedCameraError GetVideoStreamTextureGL(ulong hTrackedCamera, EVRTrackedCameraFrameType eFrameType, ref uint pglTextureId, ref CameraVideoStreamFrameHeader_t pFrameHeader, uint nFrameHeaderSize)
	{
		pglTextureId = 0u;
		return FnTable.GetVideoStreamTextureGL(hTrackedCamera, eFrameType, ref pglTextureId, ref pFrameHeader, nFrameHeaderSize);
	}

	public EVRTrackedCameraError ReleaseVideoStreamTextureGL(ulong hTrackedCamera, uint glTextureId)
	{
		return FnTable.ReleaseVideoStreamTextureGL(hTrackedCamera, glTextureId);
	}

	public void SetCameraTrackingSpace(ETrackingUniverseOrigin eUniverse)
	{
		FnTable.SetCameraTrackingSpace(eUniverse);
	}

	public ETrackingUniverseOrigin GetCameraTrackingSpace()
	{
		return FnTable.GetCameraTrackingSpace();
	}
}
