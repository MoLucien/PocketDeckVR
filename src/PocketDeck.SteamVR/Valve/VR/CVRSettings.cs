using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRSettings
{
	private IVRSettings FnTable;

	internal CVRSettings(nint pInterface)
	{
		FnTable = (IVRSettings)Marshal.PtrToStructure(pInterface, typeof(IVRSettings));
	}

	public string GetSettingsErrorNameFromEnum(EVRSettingsError eError)
	{
		nint ptr = FnTable.GetSettingsErrorNameFromEnum(eError);
		return Marshal.PtrToStringAnsi(ptr);
	}

	public void SetBool(string pchSection, string pchSettingsKey, bool bValue, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		FnTable.SetBool(num, num2, bValue, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
	}

	public void SetInt32(string pchSection, string pchSettingsKey, int nValue, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		FnTable.SetInt32(num, num2, nValue, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
	}

	public void SetFloat(string pchSection, string pchSettingsKey, float flValue, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		FnTable.SetFloat(num, num2, flValue, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
	}

	public void SetString(string pchSection, string pchSettingsKey, string pchValue, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		nint num3 = Utils.ToUtf8(pchValue);
		FnTable.SetString(num, num2, num3, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		Marshal.FreeHGlobal(num3);
	}

	public bool GetBool(string pchSection, string pchSettingsKey, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		bool result = FnTable.GetBool(num, num2, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public int GetInt32(string pchSection, string pchSettingsKey, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		int result = FnTable.GetInt32(num, num2, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public float GetFloat(string pchSection, string pchSettingsKey, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		float result = FnTable.GetFloat(num, num2, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public void GetString(string pchSection, string pchSettingsKey, StringBuilder pchValue, uint unValueLen, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		FnTable.GetString(num, num2, pchValue, unValueLen, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
	}

	public void RemoveSection(string pchSection, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		FnTable.RemoveSection(num, ref peError);
		Marshal.FreeHGlobal(num);
	}

	public void RemoveKeyInSection(string pchSection, string pchSettingsKey, ref EVRSettingsError peError)
	{
		nint num = Utils.ToUtf8(pchSection);
		nint num2 = Utils.ToUtf8(pchSettingsKey);
		FnTable.RemoveKeyInSection(num, num2, ref peError);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
	}
}
