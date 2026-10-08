using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRRenderModels
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate bool _GetComponentStatePacked(nint pchRenderModelName, nint pchComponentName, ref VRControllerState_t_Packed pControllerState, ref RenderModel_ControllerMode_State_t pState, ref RenderModel_ComponentState_t pComponentState);

	[StructLayout(LayoutKind.Explicit)]
	private struct GetComponentStateUnion
	{
		[FieldOffset(0)]
		public IVRRenderModels._GetComponentState pGetComponentState;

		[FieldOffset(0)]
		public _GetComponentStatePacked pGetComponentStatePacked;
	}

	private IVRRenderModels FnTable;

	internal CVRRenderModels(nint pInterface)
	{
		FnTable = (IVRRenderModels)Marshal.PtrToStructure(pInterface, typeof(IVRRenderModels));
	}

	public EVRRenderModelError LoadRenderModel_Async(string pchRenderModelName, ref nint ppRenderModel)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		EVRRenderModelError result = FnTable.LoadRenderModel_Async(num, ref ppRenderModel);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public void FreeRenderModel(nint pRenderModel)
	{
		FnTable.FreeRenderModel(pRenderModel);
	}

	public EVRRenderModelError LoadTexture_Async(int textureId, ref nint ppTexture)
	{
		return FnTable.LoadTexture_Async(textureId, ref ppTexture);
	}

	public void FreeTexture(nint pTexture)
	{
		FnTable.FreeTexture(pTexture);
	}

	public EVRRenderModelError LoadTextureD3D11_Async(int textureId, nint pD3D11Device, ref nint ppD3D11Texture2D)
	{
		return FnTable.LoadTextureD3D11_Async(textureId, pD3D11Device, ref ppD3D11Texture2D);
	}

	public EVRRenderModelError LoadIntoTextureD3D11_Async(int textureId, nint pDstTexture)
	{
		return FnTable.LoadIntoTextureD3D11_Async(textureId, pDstTexture);
	}

	public void FreeTextureD3D11(nint pD3D11Texture2D)
	{
		FnTable.FreeTextureD3D11(pD3D11Texture2D);
	}

	public uint GetRenderModelName(uint unRenderModelIndex, StringBuilder pchRenderModelName, uint unRenderModelNameLen)
	{
		return FnTable.GetRenderModelName(unRenderModelIndex, pchRenderModelName, unRenderModelNameLen);
	}

	public uint GetRenderModelCount()
	{
		return FnTable.GetRenderModelCount();
	}

	public uint GetComponentCount(string pchRenderModelName)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		uint result = FnTable.GetComponentCount(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetComponentName(string pchRenderModelName, uint unComponentIndex, StringBuilder pchComponentName, uint unComponentNameLen)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		uint result = FnTable.GetComponentName(num, unComponentIndex, pchComponentName, unComponentNameLen);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public ulong GetComponentButtonMask(string pchRenderModelName, string pchComponentName)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		ulong result = FnTable.GetComponentButtonMask(num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public uint GetComponentRenderModelName(string pchRenderModelName, string pchComponentName, StringBuilder pchComponentRenderModelName, uint unComponentRenderModelNameLen)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		uint result = FnTable.GetComponentRenderModelName(num, num2, pchComponentRenderModelName, unComponentRenderModelNameLen);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public bool GetComponentStateForDevicePath(string pchRenderModelName, string pchComponentName, ulong devicePath, ref RenderModel_ControllerMode_State_t pState, ref RenderModel_ComponentState_t pComponentState)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		bool result = FnTable.GetComponentStateForDevicePath(num, num2, devicePath, ref pState, ref pComponentState);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public bool GetComponentState(string pchRenderModelName, string pchComponentName, ref VRControllerState_t pControllerState, ref RenderModel_ControllerMode_State_t pState, ref RenderModel_ComponentState_t pComponentState)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix)
		{
			VRControllerState_t_Packed pControllerState2 = new VRControllerState_t_Packed(pControllerState);
			GetComponentStateUnion getComponentStateUnion = default;
			getComponentStateUnion.pGetComponentStatePacked = null;
			getComponentStateUnion.pGetComponentState = FnTable.GetComponentState;
			bool result = getComponentStateUnion.pGetComponentStatePacked(num, num2, ref pControllerState2, ref pState, ref pComponentState);
			pControllerState2.Unpack(ref pControllerState);
			return result;
		}
		bool result2 = FnTable.GetComponentState(num, num2, ref pControllerState, ref pState, ref pComponentState);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result2;
	}

	public bool RenderModelHasComponent(string pchRenderModelName, string pchComponentName)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		bool result = FnTable.RenderModelHasComponent(num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public uint GetRenderModelThumbnailURL(string pchRenderModelName, StringBuilder pchThumbnailURL, uint unThumbnailURLLen, ref EVRRenderModelError peError)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		uint result = FnTable.GetRenderModelThumbnailURL(num, pchThumbnailURL, unThumbnailURLLen, ref peError);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetRenderModelOriginalPath(string pchRenderModelName, StringBuilder pchOriginalPath, uint unOriginalPathLen, ref EVRRenderModelError peError)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		uint result = FnTable.GetRenderModelOriginalPath(num, pchOriginalPath, unOriginalPathLen, ref peError);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public string GetRenderModelErrorNameFromEnum(EVRRenderModelError error)
	{
		nint ptr = FnTable.GetRenderModelErrorNameFromEnum(error);
		return Marshal.PtrToStringAnsi(ptr);
	}
}
