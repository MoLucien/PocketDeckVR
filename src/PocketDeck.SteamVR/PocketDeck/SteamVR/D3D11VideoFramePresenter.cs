using System;
using System.Buffers;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using PocketDeck.Media;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace PocketDeck.SteamVR;

internal sealed class D3D11VideoFramePresenter : IGpuVideoFramePresenter, IDisposable
{
	private const int _textureSlotCount = 3;

	private readonly object _gate = new object();

	private ID3D11Device? _device;

	private ID3D11DeviceContext? _context;

	private ID3D11VideoDevice? _videoDevice;

	private ID3D11VideoContext? _videoContext;

	private ID3D11VideoProcessorEnumerator? _enumerator;

	private ID3D11VideoProcessor? _processor;

	private ID3D11Texture2D? _inputTexture;

	private ID3D11VideoProcessorInputView? _inputView;

	private readonly ID3D11Texture2D?[] _outputTextures = new ID3D11Texture2D[3];

	private readonly ID3D11VideoProcessorOutputView?[] _outputViews = new ID3D11VideoProcessorOutputView[3];

	private int _inputWidth;

	private int _inputHeight;

	private int _outputWidth;

	private int _outputHeight;

	private int _nextTextureSlot;

	private bool _disposed;

	public string AdapterBackend { get; }

	public D3D11VideoFramePresenter(long? adapterLuid)
	{
		try
		{
			(ID3D11Device, string) tuple = CreateDevice(adapterLuid);
			_device = tuple.Item1;
			string item = tuple.Item2;
			_context = _device.ImmediateContext;
			_videoDevice = _device.QueryInterface<ID3D11VideoDevice>();
			_videoContext = _context.QueryInterface<ID3D11VideoContext>();
			AdapterBackend = $"{item}; D3D11 feature level {_device.FeatureLevel}";
		}
		catch (Exception ex) when ((ex is SharpGenException || ex is COMException) ? true : false)
		{
			Dispose();
			throw new GpuVideoPresenterException("D3D11_VIDEO_INITIALIZATION_FAILED", "D3D11 视频设备初始化失败", ex);
		}
	}

	public GpuVideoFrame Present(DecodedVideoFrame frame)
	{
		ArgumentNullException.ThrowIfNull(frame, "frame");
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (frame.PixelFormat != VideoPixelFormat.Nv12)
		{
			throw new GpuVideoPresenterException("D3D11_VIDEO_PIXEL_FORMAT_UNSUPPORTED", $"D3D11 视频转换器不支持 {frame.PixelFormat} 输入");
		}
		if ((frame.Width & 1) != 0 || (frame.Height & 1) != 0)
		{
			throw new GpuVideoPresenterException("D3D11_VIDEO_NV12_ODD_DIMENSIONS", "NV12 视频宽高必须是偶数");
		}
		int num = checked(frame.Stride * frame.Height * 3) / 2;
		if (frame.Pixels.Length < num)
		{
			throw new GpuVideoPresenterException("D3D11_VIDEO_FRAME_TRUNCATED", "NV12 视频帧长度不足");
		}
		lock (_gate)
		{
			try
			{
				EnsureResources(frame.Width, frame.Height, frame.VisibleWidth, frame.VisibleHeight);
				UploadNv12(frame.Pixels, frame.Stride, num);
				int nextTextureSlot = _nextTextureSlot;
				_nextTextureSlot = checked(_nextTextureSlot + 1) % 3;
				VideoProcessorStream[] streams = new VideoProcessorStream[1]
				{
					new VideoProcessorStream
					{
						Enable = true,
						InputFrameOrField = 0u,
						InputSurface = _inputView,
						OutputIndex = 0u
					}
				};
				_videoContext.VideoProcessorBlt(_processor, _outputViews[nextTextureSlot], 0u, streams).CheckError();
				_context.Flush();
				return new GpuVideoFrame(_outputTextures[nextTextureSlot].NativePointer, frame.VisibleWidth, frame.VisibleHeight, frame.PresentationTimeMicroseconds, nextTextureSlot);
			}
			catch (GpuVideoPresenterException)
			{
				throw;
			}
			catch (Exception ex2) when ((ex2 is SharpGenException || ex2 is COMException) ? true : false)
			{
				throw new GpuVideoPresenterException("D3D11_VIDEO_PRESENT_FAILED", "D3D11 视频帧转换失败", ex2);
			}
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (!_disposed)
			{
				_disposed = true;
				ReleaseFrameResources();
				_videoContext?.Dispose();
				_videoContext = null;
				_videoDevice?.Dispose();
				_videoDevice = null;
				_context?.Dispose();
				_context = null;
				_device?.Dispose();
				_device = null;
			}
		}
	}

	private void EnsureResources(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
	{
		checked
		{
			if ((object)_inputTexture == null || _inputWidth != inputWidth || _inputHeight != inputHeight || _outputWidth != outputWidth || _outputHeight != outputHeight)
			{
				ReleaseFrameResources();
				_inputWidth = inputWidth;
				_inputHeight = inputHeight;
				_outputWidth = outputWidth;
				_outputHeight = outputHeight;
				_nextTextureSlot = 0;
				VideoProcessorContentDescription description = new VideoProcessorContentDescription
				{
					InputFrameFormat = VideoFrameFormat.Progressive,
					InputFrameRate = new Rational(60u, 1u),
					InputWidth = (uint)inputWidth,
					InputHeight = (uint)inputHeight,
					OutputFrameRate = new Rational(60u, 1u),
					OutputWidth = (uint)outputWidth,
					OutputHeight = (uint)outputHeight,
					Usage = VideoUsage.OptimalSpeed
				};
				_enumerator = _videoDevice.CreateVideoProcessorEnumerator(description);
				_processor = _videoDevice.CreateVideoProcessor(_enumerator, 0u);
				Texture2DDescription description2 = new Texture2DDescription(Format.NV12, (uint)inputWidth, (uint)inputHeight, 1u, 1u, BindFlags.None);
				_inputTexture = _device.CreateTexture2D(in description2);
				VideoProcessorInputViewDescription description3 = new VideoProcessorInputViewDescription
				{
					ViewDimension = VideoProcessorInputViewDimension.Texture2D,
					Texture2D = new Texture2DVideoProcessorInputView
					{
						MipSlice = 0u,
						ArraySlice = 0u
					}
				};
				_inputView = _videoDevice.CreateVideoProcessorInputView(_inputTexture, _enumerator, description3);
				Texture2DDescription description4 = new Texture2DDescription(Format.R8G8B8A8_UNorm, (uint)outputWidth, (uint)outputHeight, 1u, 1u, BindFlags.ShaderResource | BindFlags.RenderTarget);
				VideoProcessorOutputViewDescription description5 = new VideoProcessorOutputViewDescription
				{
					ViewDimension = VideoProcessorOutputViewDimension.Texture2D,
					Texture2D = new Texture2DVideoProcessorOutputView
					{
						MipSlice = 0u
					}
				};
				for (int i = 0; i < 3; i++)
				{
					_outputTextures[i] = _device.CreateTexture2D(in description4);
					_outputViews[i] = _videoDevice.CreateVideoProcessorOutputView(_outputTextures[i], _enumerator, description5);
				}
				_videoContext.VideoProcessorSetStreamFrameFormat(_processor, 0u, VideoFrameFormat.Progressive);
				_videoContext.VideoProcessorSetStreamAutoProcessingMode(_processor, 0u, false);
				_videoContext.VideoProcessorSetStreamSourceRect(_processor, 0u, true, new RawRect(0, 0, outputWidth, outputHeight));
				_videoContext.VideoProcessorSetStreamDestRect(_processor, 0u, true, new RawRect(0, 0, outputWidth, outputHeight));
				_videoContext.VideoProcessorSetOutputTargetRect(_processor, true, new RawRect(0, 0, outputWidth, outputHeight));
			}
		}
	}

	private static (ID3D11Device Device, string AdapterName) CreateDevice(long? adapterLuid)
	{
		DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;
		FeatureLevel[] featureLevels = new FeatureLevel[2]
		{
			FeatureLevel.Level_11_1,
			FeatureLevel.Level_11_0
		};
		if (!adapterLuid.HasValue)
		{
			return (Device: D3D11.D3D11CreateDevice(DriverType.Hardware, flags, featureLevels), AdapterName: "Default hardware adapter");
		}
		using IDXGIFactory4 iDXGIFactory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
		using IDXGIAdapter1 iDXGIAdapter = iDXGIFactory.EnumAdapterByLuid<IDXGIAdapter1>(Luid.FromInt64(adapterLuid.Value));
		string description = iDXGIAdapter.Description1.Description;
		D3D11.D3D11CreateDevice(iDXGIAdapter, DriverType.Unknown, flags, featureLevels, out ID3D11Device device).CheckError();
		return (Device: device ?? throw new GpuVideoPresenterException("D3D11_STEAMVR_ADAPTER_MISSING", "无法在 SteamVR 使用的显卡上创建 D3D11 设备"), AdapterName: description);
	}

	private unsafe void UploadNv12(ReadOnlyMemory<byte> pixels, int stride, int requiredLength)
	{
		checked
		{
			using MemoryHandle memoryHandle = pixels.Slice(0, requiredLength).Pin();
			_context.UpdateSubresource(_inputTexture, 0u, null, (nint)memoryHandle.Pointer, (uint)stride, (uint)requiredLength);
		}
	}

	private void ReleaseFrameResources()
	{
		for (int i = 0; i < 3; i = checked(i + 1))
		{
			_outputViews[i]?.Dispose();
			_outputViews[i] = null;
			_outputTextures[i]?.Dispose();
			_outputTextures[i] = null;
		}
		_inputView?.Dispose();
		_inputView = null;
		_inputTexture?.Dispose();
		_inputTexture = null;
		_processor?.Dispose();
		_processor = null;
		_enumerator?.Dispose();
		_enumerator = null;
		_inputWidth = 0;
		_inputHeight = 0;
		_outputWidth = 0;
		_outputHeight = 0;
	}
}
