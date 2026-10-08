using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRInput
{
	private IVRInput FnTable;

	internal CVRInput(nint pInterface)
	{
		FnTable = (IVRInput)Marshal.PtrToStructure(pInterface, typeof(IVRInput));
	}

	public EVRInputError SetActionManifestPath(string pchActionManifestPath)
	{
		nint num = Utils.ToUtf8(pchActionManifestPath);
		EVRInputError result = FnTable.SetActionManifestPath(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRInputError GetActionSetHandle(string pchActionSetName, ref ulong pHandle)
	{
		nint num = Utils.ToUtf8(pchActionSetName);
		pHandle = 0uL;
		EVRInputError result = FnTable.GetActionSetHandle(num, ref pHandle);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRInputError GetActionHandle(string pchActionName, ref ulong pHandle)
	{
		nint num = Utils.ToUtf8(pchActionName);
		pHandle = 0uL;
		EVRInputError result = FnTable.GetActionHandle(num, ref pHandle);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRInputError GetInputSourceHandle(string pchInputSourcePath, ref ulong pHandle)
	{
		nint num = Utils.ToUtf8(pchInputSourcePath);
		pHandle = 0uL;
		EVRInputError result = FnTable.GetInputSourceHandle(num, ref pHandle);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRInputError UpdateActionState(VRActiveActionSet_t[] pSets, uint unSizeOfVRSelectedActionSet_t)
	{
		return FnTable.UpdateActionState(pSets, unSizeOfVRSelectedActionSet_t, checked((uint)pSets.Length));
	}

	public EVRInputError GetDigitalActionData(ulong action, ref InputDigitalActionData_t pActionData, uint unActionDataSize, ulong ulRestrictToDevice)
	{
		return FnTable.GetDigitalActionData(action, ref pActionData, unActionDataSize, ulRestrictToDevice);
	}

	public EVRInputError GetAnalogActionData(ulong action, ref InputAnalogActionData_t pActionData, uint unActionDataSize, ulong ulRestrictToDevice)
	{
		return FnTable.GetAnalogActionData(action, ref pActionData, unActionDataSize, ulRestrictToDevice);
	}

	public EVRInputError GetPoseActionDataRelativeToNow(ulong action, ETrackingUniverseOrigin eOrigin, float fPredictedSecondsFromNow, ref InputPoseActionData_t pActionData, uint unActionDataSize, ulong ulRestrictToDevice)
	{
		return FnTable.GetPoseActionDataRelativeToNow(action, eOrigin, fPredictedSecondsFromNow, ref pActionData, unActionDataSize, ulRestrictToDevice);
	}

	public EVRInputError GetPoseActionDataForNextFrame(ulong action, ETrackingUniverseOrigin eOrigin, ref InputPoseActionData_t pActionData, uint unActionDataSize, ulong ulRestrictToDevice)
	{
		return FnTable.GetPoseActionDataForNextFrame(action, eOrigin, ref pActionData, unActionDataSize, ulRestrictToDevice);
	}

	public EVRInputError GetSkeletalActionData(ulong action, ref InputSkeletalActionData_t pActionData, uint unActionDataSize)
	{
		return FnTable.GetSkeletalActionData(action, ref pActionData, unActionDataSize);
	}

	public EVRInputError GetDominantHand(ref ETrackedControllerRole peDominantHand)
	{
		return FnTable.GetDominantHand(ref peDominantHand);
	}

	public EVRInputError SetDominantHand(ETrackedControllerRole eDominantHand)
	{
		return FnTable.SetDominantHand(eDominantHand);
	}

	public EVRInputError GetBoneCount(ulong action, ref uint pBoneCount)
	{
		pBoneCount = 0u;
		return FnTable.GetBoneCount(action, ref pBoneCount);
	}

	public EVRInputError GetBoneHierarchy(ulong action, int[] pParentIndices)
	{
		return FnTable.GetBoneHierarchy(action, pParentIndices, checked((uint)pParentIndices.Length));
	}

	public EVRInputError GetBoneName(ulong action, int nBoneIndex, StringBuilder pchBoneName, uint unNameBufferSize)
	{
		return FnTable.GetBoneName(action, nBoneIndex, pchBoneName, unNameBufferSize);
	}

	public EVRInputError GetSkeletalReferenceTransforms(ulong action, EVRSkeletalTransformSpace eTransformSpace, EVRSkeletalReferencePose eReferencePose, VRBoneTransform_t[] pTransformArray)
	{
		return FnTable.GetSkeletalReferenceTransforms(action, eTransformSpace, eReferencePose, pTransformArray, checked((uint)pTransformArray.Length));
	}

	public EVRInputError GetSkeletalTrackingLevel(ulong action, ref EVRSkeletalTrackingLevel pSkeletalTrackingLevel)
	{
		return FnTable.GetSkeletalTrackingLevel(action, ref pSkeletalTrackingLevel);
	}

	public EVRInputError GetSkeletalBoneData(ulong action, EVRSkeletalTransformSpace eTransformSpace, EVRSkeletalMotionRange eMotionRange, VRBoneTransform_t[] pTransformArray)
	{
		return FnTable.GetSkeletalBoneData(action, eTransformSpace, eMotionRange, pTransformArray, checked((uint)pTransformArray.Length));
	}

	public EVRInputError GetSkeletalSummaryData(ulong action, EVRSummaryType eSummaryType, ref VRSkeletalSummaryData_t pSkeletalSummaryData)
	{
		return FnTable.GetSkeletalSummaryData(action, eSummaryType, ref pSkeletalSummaryData);
	}

	public EVRInputError GetSkeletalBoneDataCompressed(ulong action, EVRSkeletalMotionRange eMotionRange, nint pvCompressedData, uint unCompressedSize, ref uint punRequiredCompressedSize)
	{
		punRequiredCompressedSize = 0u;
		return FnTable.GetSkeletalBoneDataCompressed(action, eMotionRange, pvCompressedData, unCompressedSize, ref punRequiredCompressedSize);
	}

	public EVRInputError DecompressSkeletalBoneData(nint pvCompressedBuffer, uint unCompressedBufferSize, EVRSkeletalTransformSpace eTransformSpace, VRBoneTransform_t[] pTransformArray)
	{
		return FnTable.DecompressSkeletalBoneData(pvCompressedBuffer, unCompressedBufferSize, eTransformSpace, pTransformArray, checked((uint)pTransformArray.Length));
	}

	public EVRInputError TriggerHapticVibrationAction(ulong action, float fStartSecondsFromNow, float fDurationSeconds, float fFrequency, float fAmplitude, ulong ulRestrictToDevice)
	{
		return FnTable.TriggerHapticVibrationAction(action, fStartSecondsFromNow, fDurationSeconds, fFrequency, fAmplitude, ulRestrictToDevice);
	}

	public EVRInputError GetActionOrigins(ulong actionSetHandle, ulong digitalActionHandle, ulong[] originsOut)
	{
		return FnTable.GetActionOrigins(actionSetHandle, digitalActionHandle, originsOut, checked((uint)originsOut.Length));
	}

	public EVRInputError GetOriginLocalizedName(ulong origin, StringBuilder pchNameArray, uint unNameArraySize, int unStringSectionsToInclude)
	{
		return FnTable.GetOriginLocalizedName(origin, pchNameArray, unNameArraySize, unStringSectionsToInclude);
	}

	public EVRInputError GetOriginTrackedDeviceInfo(ulong origin, ref InputOriginInfo_t pOriginInfo, uint unOriginInfoSize)
	{
		return FnTable.GetOriginTrackedDeviceInfo(origin, ref pOriginInfo, unOriginInfoSize);
	}

	public EVRInputError GetActionBindingInfo(ulong action, ref InputBindingInfo_t pOriginInfo, uint unBindingInfoSize, uint unBindingInfoCount, ref uint punReturnedBindingInfoCount)
	{
		punReturnedBindingInfoCount = 0u;
		return FnTable.GetActionBindingInfo(action, ref pOriginInfo, unBindingInfoSize, unBindingInfoCount, ref punReturnedBindingInfoCount);
	}

	public EVRInputError ShowActionOrigins(ulong actionSetHandle, ulong ulActionHandle)
	{
		return FnTable.ShowActionOrigins(actionSetHandle, ulActionHandle);
	}

	public EVRInputError ShowBindingsForActionSet(VRActiveActionSet_t[] pSets, uint unSizeOfVRSelectedActionSet_t, ulong originToHighlight)
	{
		return FnTable.ShowBindingsForActionSet(pSets, unSizeOfVRSelectedActionSet_t, checked((uint)pSets.Length), originToHighlight);
	}

	public EVRInputError GetComponentStateForBinding(string pchRenderModelName, string pchComponentName, ref InputBindingInfo_t pOriginInfo, uint unBindingInfoSize, uint unBindingInfoCount, ref RenderModel_ComponentState_t pComponentState)
	{
		nint num = Utils.ToUtf8(pchRenderModelName);
		nint num2 = Utils.ToUtf8(pchComponentName);
		EVRInputError result = FnTable.GetComponentStateForBinding(num, num2, ref pOriginInfo, unBindingInfoSize, unBindingInfoCount, ref pComponentState);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public bool IsUsingLegacyInput()
	{
		return FnTable.IsUsingLegacyInput();
	}

	public EVRInputError OpenBindingUI(string pchAppKey, ulong ulActionSetHandle, ulong ulDeviceHandle, bool bShowOnDesktop)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRInputError result = FnTable.OpenBindingUI(num, ulActionSetHandle, ulDeviceHandle, bShowOnDesktop);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRInputError GetBindingVariant(ulong ulDevicePath, StringBuilder pchVariantArray, uint unVariantArraySize)
	{
		return FnTable.GetBindingVariant(ulDevicePath, pchVariantArray, unVariantArraySize);
	}
}
