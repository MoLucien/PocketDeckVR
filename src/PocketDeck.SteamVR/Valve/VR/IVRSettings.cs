using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public struct IVRSettings
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate nint _GetSettingsErrorNameFromEnum(EVRSettingsError eError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _SetBool(nint pchSection, nint pchSettingsKey, bool bValue, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _SetInt32(nint pchSection, nint pchSettingsKey, int nValue, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _SetFloat(nint pchSection, nint pchSettingsKey, float flValue, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _SetString(nint pchSection, nint pchSettingsKey, nint pchValue, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate bool _GetBool(nint pchSection, nint pchSettingsKey, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate int _GetInt32(nint pchSection, nint pchSettingsKey, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate float _GetFloat(nint pchSection, nint pchSettingsKey, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _GetString(nint pchSection, nint pchSettingsKey, StringBuilder pchValue, uint unValueLen, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _RemoveSection(nint pchSection, ref EVRSettingsError peError);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void _RemoveKeyInSection(nint pchSection, nint pchSettingsKey, ref EVRSettingsError peError);

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _GetSettingsErrorNameFromEnum GetSettingsErrorNameFromEnum;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _SetBool SetBool;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _SetInt32 SetInt32;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _SetFloat SetFloat;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _SetString SetString;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _GetBool GetBool;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _GetInt32 GetInt32;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _GetFloat GetFloat;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _GetString GetString;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _RemoveSection RemoveSection;

	[MarshalAs(UnmanagedType.FunctionPtr)]
	internal _RemoveKeyInSection RemoveKeyInSection;
}
