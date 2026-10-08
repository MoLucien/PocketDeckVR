using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRApplications
{
	private IVRApplications FnTable;

	internal CVRApplications(nint pInterface)
	{
		FnTable = (IVRApplications)Marshal.PtrToStructure(pInterface, typeof(IVRApplications));
	}

	public EVRApplicationError AddApplicationManifest(string pchApplicationManifestFullPath, bool bTemporary)
	{
		nint num = Utils.ToUtf8(pchApplicationManifestFullPath);
		EVRApplicationError result = FnTable.AddApplicationManifest(num, bTemporary);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRApplicationError RemoveApplicationManifest(string pchApplicationManifestFullPath)
	{
		nint num = Utils.ToUtf8(pchApplicationManifestFullPath);
		EVRApplicationError result = FnTable.RemoveApplicationManifest(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public bool IsApplicationInstalled(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		bool result = FnTable.IsApplicationInstalled(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetApplicationCount()
	{
		return FnTable.GetApplicationCount();
	}

	public EVRApplicationError GetApplicationKeyByIndex(uint unApplicationIndex, StringBuilder pchAppKeyBuffer, uint unAppKeyBufferLen)
	{
		return FnTable.GetApplicationKeyByIndex(unApplicationIndex, pchAppKeyBuffer, unAppKeyBufferLen);
	}

	public EVRApplicationError GetApplicationKeyByProcessId(uint unProcessId, StringBuilder pchAppKeyBuffer, uint unAppKeyBufferLen)
	{
		return FnTable.GetApplicationKeyByProcessId(unProcessId, pchAppKeyBuffer, unAppKeyBufferLen);
	}

	public EVRApplicationError LaunchApplication(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRApplicationError result = FnTable.LaunchApplication(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRApplicationError LaunchTemplateApplication(string pchTemplateAppKey, string pchNewAppKey, AppOverrideKeys_t[] pKeys)
	{
		nint num = Utils.ToUtf8(pchTemplateAppKey);
		nint num2 = Utils.ToUtf8(pchNewAppKey);
		EVRApplicationError result = FnTable.LaunchTemplateApplication(num, num2, pKeys, checked((uint)pKeys.Length));
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public EVRApplicationError LaunchApplicationFromMimeType(string pchMimeType, string pchArgs)
	{
		nint num = Utils.ToUtf8(pchMimeType);
		nint num2 = Utils.ToUtf8(pchArgs);
		EVRApplicationError result = FnTable.LaunchApplicationFromMimeType(num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public EVRApplicationError LaunchDashboardOverlay(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRApplicationError result = FnTable.LaunchDashboardOverlay(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public bool CancelApplicationLaunch(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		bool result = FnTable.CancelApplicationLaunch(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRApplicationError IdentifyApplication(uint unProcessId, string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRApplicationError result = FnTable.IdentifyApplication(unProcessId, num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetApplicationProcessId(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		uint result = FnTable.GetApplicationProcessId(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public string GetApplicationsErrorNameFromEnum(EVRApplicationError error)
	{
		nint ptr = FnTable.GetApplicationsErrorNameFromEnum(error);
		return Marshal.PtrToStringAnsi(ptr);
	}

	public uint GetApplicationPropertyString(string pchAppKey, EVRApplicationProperty eProperty, StringBuilder pchPropertyValueBuffer, uint unPropertyValueBufferLen, ref EVRApplicationError peError)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		uint result = FnTable.GetApplicationPropertyString(num, eProperty, pchPropertyValueBuffer, unPropertyValueBufferLen, ref peError);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public bool GetApplicationPropertyBool(string pchAppKey, EVRApplicationProperty eProperty, ref EVRApplicationError peError)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		bool result = FnTable.GetApplicationPropertyBool(num, eProperty, ref peError);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public ulong GetApplicationPropertyUint64(string pchAppKey, EVRApplicationProperty eProperty, ref EVRApplicationError peError)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		ulong result = FnTable.GetApplicationPropertyUint64(num, eProperty, ref peError);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRApplicationError SetApplicationAutoLaunch(string pchAppKey, bool bAutoLaunch)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRApplicationError result = FnTable.SetApplicationAutoLaunch(num, bAutoLaunch);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public bool GetApplicationAutoLaunch(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		bool result = FnTable.GetApplicationAutoLaunch(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public EVRApplicationError SetDefaultApplicationForMimeType(string pchAppKey, string pchMimeType)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		nint num2 = Utils.ToUtf8(pchMimeType);
		EVRApplicationError result = FnTable.SetDefaultApplicationForMimeType(num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public bool GetDefaultApplicationForMimeType(string pchMimeType, StringBuilder pchAppKeyBuffer, uint unAppKeyBufferLen)
	{
		nint num = Utils.ToUtf8(pchMimeType);
		bool result = FnTable.GetDefaultApplicationForMimeType(num, pchAppKeyBuffer, unAppKeyBufferLen);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public bool GetApplicationSupportedMimeTypes(string pchAppKey, StringBuilder pchMimeTypesBuffer, uint unMimeTypesBuffer)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		bool result = FnTable.GetApplicationSupportedMimeTypes(num, pchMimeTypesBuffer, unMimeTypesBuffer);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetApplicationsThatSupportMimeType(string pchMimeType, StringBuilder pchAppKeysThatSupportBuffer, uint unAppKeysThatSupportBuffer)
	{
		nint num = Utils.ToUtf8(pchMimeType);
		uint result = FnTable.GetApplicationsThatSupportMimeType(num, pchAppKeysThatSupportBuffer, unAppKeysThatSupportBuffer);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public uint GetApplicationLaunchArguments(uint unHandle, StringBuilder pchArgs, uint unArgs)
	{
		return FnTable.GetApplicationLaunchArguments(unHandle, pchArgs, unArgs);
	}

	public EVRApplicationError GetStartingApplication(StringBuilder pchAppKeyBuffer, uint unAppKeyBufferLen)
	{
		return FnTable.GetStartingApplication(pchAppKeyBuffer, unAppKeyBufferLen);
	}

	public EVRSceneApplicationState GetSceneApplicationState()
	{
		return FnTable.GetSceneApplicationState();
	}

	public EVRApplicationError PerformApplicationPrelaunchCheck(string pchAppKey)
	{
		nint num = Utils.ToUtf8(pchAppKey);
		EVRApplicationError result = FnTable.PerformApplicationPrelaunchCheck(num);
		Marshal.FreeHGlobal(num);
		return result;
	}

	public string GetSceneApplicationStateNameFromEnum(EVRSceneApplicationState state)
	{
		nint ptr = FnTable.GetSceneApplicationStateNameFromEnum(state);
		return Marshal.PtrToStringAnsi(ptr);
	}

	public EVRApplicationError LaunchInternalProcess(string pchBinaryPath, string pchArguments, string pchWorkingDirectory)
	{
		nint num = Utils.ToUtf8(pchBinaryPath);
		nint num2 = Utils.ToUtf8(pchArguments);
		nint num3 = Utils.ToUtf8(pchWorkingDirectory);
		EVRApplicationError result = FnTable.LaunchInternalProcess(num, num2, num3);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		Marshal.FreeHGlobal(num3);
		return result;
	}

	public uint GetCurrentSceneProcessId()
	{
		return FnTable.GetCurrentSceneProcessId();
	}
}
