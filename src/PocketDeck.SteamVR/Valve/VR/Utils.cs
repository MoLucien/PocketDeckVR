using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR;

public class Utils
{
	private static byte[] buffer = new byte[1024];

	public static nint ToUtf8(string managedString)
	{
		if (managedString == null)
		{
			return IntPtr.Zero;
		}
		checked
		{
			int num = Encoding.UTF8.GetByteCount(managedString) + 1;
			if (buffer.Length < num)
			{
				buffer = new byte[num];
			}
			int bytes = Encoding.UTF8.GetBytes(managedString, 0, managedString.Length, buffer, 0);
			buffer[bytes] = 0;
			nint num2 = Marshal.AllocHGlobal(bytes + 1);
			Marshal.Copy(buffer, 0, num2, bytes + 1);
			return num2;
		}
	}
}
