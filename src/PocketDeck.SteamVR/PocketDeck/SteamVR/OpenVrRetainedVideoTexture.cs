using System;
using System.Runtime.InteropServices;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrRetainedVideoTexture : IDisposable
{
	public nint Pointer { get; private set; }

	public void Update(nint pointer)
	{
		ArgumentOutOfRangeException.ThrowIfZero<nint>(pointer, "pointer");
		if (Pointer != pointer)
		{
			Marshal.AddRef(pointer);
			nint pointer2 = Pointer;
			Pointer = pointer;
			if (pointer2 != 0)
			{
				Marshal.Release(pointer2);
			}
		}
	}

	public void Dispose()
	{
		nint pointer = Pointer;
		Pointer = 0;
		if (pointer != 0)
		{
			Marshal.Release(pointer);
		}
	}
}
