using System;
using System.Runtime.InteropServices;
using Valve.VR;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace PocketDeck.SteamVR;

internal sealed class D3D11ControlTexture : IDisposable
{
	private readonly ID3D11Device _device;

	private ID3D11DeviceContext? _context;

	private ID3D11Texture2D? _texture;

	private bool _disposed;

	public int Width { get; }

	public int Height { get; }

	public nint NativePointer => _texture?.NativePointer ?? 0;

	internal ID3D11Texture2D Texture => _texture ?? throw new ObjectDisposedException("D3D11ControlTexture");

	internal long SubmissionFlushes { get; private set; }

	public D3D11ControlTexture(ID3D11Device device, int width, int height)
	{
		_device = device;
		try
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(width, 1, "width");
			ArgumentOutOfRangeException.ThrowIfLessThan(height, 1, "height");
			ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 2048, "width");
			ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 2048, "height");
			Width = width;
			Height = height;
			_context = device.ImmediateContext;
			_texture = device.CreateTexture2D(checked(new Texture2DDescription(Format.R8G8B8A8_UNorm, (uint)width, (uint)height, 1u, 1u, BindFlags.ShaderResource | BindFlags.RenderTarget)));
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	public void FlushSubmission()
	{
		_context.Flush();
		checked
		{
			SubmissionFlushes++;
		}
	}

	public static D3D11ControlTexture CreateForSteamVr(int width, int height)
	{
		ulong pnDevice = 0uL;
		OpenVR.System.GetOutputDevice(ref pnDevice, ETextureType.DirectX, 0);
		using IDXGIFactory4 iDXGIFactory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
		using IDXGIAdapter1 adapter = iDXGIFactory.EnumAdapterByLuid<IDXGIAdapter1>(Luid.FromInt64((long)pnDevice));
		ID3D11Device device = null;
		try
		{
			D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, new FeatureLevel[2]
			{
				FeatureLevel.Level_11_1,
				FeatureLevel.Level_11_0
			}, out device).CheckError();
			D3D11ControlTexture result = new D3D11ControlTexture(device ?? throw new InvalidOperationException("Control graphics device unavailable."), width, height);
			device = null;
			return result;
		}
		finally
		{
			device?.Dispose();
		}
	}

	public void Update(byte[] pixels)
	{
		ObjectDisposedException.ThrowIf((object)_texture == null, this);
		ArgumentNullException.ThrowIfNull(pixels, "pixels");
		checked
		{
			if (pixels.Length != Width * Height * 4)
			{
				throw new ArgumentException("Incorrect RGBA pixel count.", "pixels");
			}
			GCHandle gCHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
			try
			{
				_context.UpdateSubresource(_texture, 0u, null, gCHandle.AddrOfPinnedObject(), (uint)(Width * 4), (uint)pixels.Length);
				_context.Flush();
			}
			finally
			{
				gCHandle.Free();
			}
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_texture?.Dispose();
			_texture = null;
			_context?.Dispose();
			_context = null;
			_device.Dispose();
		}
	}
}
