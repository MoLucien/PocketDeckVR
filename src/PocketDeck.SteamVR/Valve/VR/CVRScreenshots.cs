using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class CVRScreenshots
{
	private IVRScreenshots FnTable;

	internal CVRScreenshots(nint pInterface)
	{
		FnTable = (IVRScreenshots)Marshal.PtrToStructure(pInterface, typeof(IVRScreenshots));
	}

	public EVRScreenshotError RequestScreenshot(ref uint pOutScreenshotHandle, EVRScreenshotType type, string pchPreviewFilename, string pchVRFilename)
	{
		pOutScreenshotHandle = 0u;
		nint num = Utils.ToUtf8(pchPreviewFilename);
		nint num2 = Utils.ToUtf8(pchVRFilename);
		EVRScreenshotError result = FnTable.RequestScreenshot(ref pOutScreenshotHandle, type, num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public EVRScreenshotError HookScreenshot(EVRScreenshotType[] pSupportedTypes)
	{
		return FnTable.HookScreenshot(pSupportedTypes, pSupportedTypes.Length);
	}

	public EVRScreenshotType GetScreenshotPropertyType(uint screenshotHandle, ref EVRScreenshotError pError)
	{
		return FnTable.GetScreenshotPropertyType(screenshotHandle, ref pError);
	}

	public uint GetScreenshotPropertyFilename(uint screenshotHandle, EVRScreenshotPropertyFilenames filenameType, StringBuilder pchFilename, uint cchFilename, ref EVRScreenshotError pError)
	{
		return FnTable.GetScreenshotPropertyFilename(screenshotHandle, filenameType, pchFilename, cchFilename, ref pError);
	}

	public EVRScreenshotError UpdateScreenshotProgress(uint screenshotHandle, float flProgress)
	{
		return FnTable.UpdateScreenshotProgress(screenshotHandle, flProgress);
	}

	public EVRScreenshotError TakeStereoScreenshot(ref uint pOutScreenshotHandle, string pchPreviewFilename, string pchVRFilename)
	{
		pOutScreenshotHandle = 0u;
		nint num = Utils.ToUtf8(pchPreviewFilename);
		nint num2 = Utils.ToUtf8(pchVRFilename);
		EVRScreenshotError result = FnTable.TakeStereoScreenshot(ref pOutScreenshotHandle, num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}

	public EVRScreenshotError SubmitScreenshot(uint screenshotHandle, EVRScreenshotType type, string pchSourcePreviewFilename, string pchSourceVRFilename)
	{
		nint num = Utils.ToUtf8(pchSourcePreviewFilename);
		nint num2 = Utils.ToUtf8(pchSourceVRFilename);
		EVRScreenshotError result = FnTable.SubmitScreenshot(screenshotHandle, type, num, num2);
		Marshal.FreeHGlobal(num);
		Marshal.FreeHGlobal(num2);
		return result;
	}
}
