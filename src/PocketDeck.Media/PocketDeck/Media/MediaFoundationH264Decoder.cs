using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.MediaFoundation;

namespace PocketDeck.Media;

internal sealed class MediaFoundationH264Decoder : IH264VideoDecoder, IDisposable
{
	private const int _inputStreamId = 0;

	private const int _outputStreamId = 0;

	private readonly IMFTransform _transform;

	private readonly byte[] _codecConfiguration;

	private readonly int _framesPerSecond;

	private readonly int _visibleWidth;

	private readonly int _visibleHeight;

	private int _width;

	private int _height;

	private int _stride;

	private bool _prependConfiguration = true;

	private bool _mediaFoundationStarted;

	private bool _disposed;

	public string BackendName { get; }

	public MediaFoundationH264Decoder(int width, int height, int framesPerSecond, ReadOnlySpan<byte> codecConfiguration)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(width, 1, "width");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 16384, "width");
		ArgumentOutOfRangeException.ThrowIfLessThan(height, 1, "height");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 16384, "height");
		ArgumentOutOfRangeException.ThrowIfLessThan(framesPerSecond, 1, "framesPerSecond");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(framesPerSecond, 120, "framesPerSecond");
		if (codecConfiguration.IsEmpty)
		{
			throw new ArgumentException("H.264 codec configuration must not be empty.", "codecConfiguration");
		}
		ArgumentOutOfRangeException.ThrowIfGreaterThan(codecConfiguration.Length, 1048576, "codecConfiguration");
		_width = width;
		_height = height;
		_visibleWidth = width;
		_visibleHeight = height;
		_stride = width;
		_framesPerSecond = framesPerSecond;
		_codecConfiguration = codecConfiguration.ToArray();
		try
		{
			MediaFactory.MFStartup().CheckError();
			_mediaFoundationStarted = true;
			(IMFTransform, string)? tuple = TryCreateHardwareDecoder();
			if (tuple.HasValue)
			{
				_transform = tuple.Value.Item1;
				BackendName = tuple.Value.Item2;
				try
				{
					ConfigureTransform();
					return;
				}
				catch (Exception ex) when ((ex is SharpGenException || ex is COMException || ex is MediaDecoderException) ? true : false)
				{
					_transform.Dispose();
				}
			}
			(_transform, BackendName) = CreateSoftwareDecoder();
			ConfigureTransform();
		}
		catch (Exception ex2) when ((ex2 is SharpGenException || ex2 is COMException) ? true : false)
		{
			Dispose();
			throw new MediaDecoderException("MEDIA_H264_INITIALIZATION_FAILED", "Windows H.264 解码器初始化失败", ex2);
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	public DecodedVideoFrame? DecodePacket(ReadOnlySpan<byte> annexBPayload, long presentationTimeMicroseconds, bool keyFrame)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (annexBPayload.IsEmpty)
		{
			throw new ArgumentException("H.264 media packet must not be empty.", "annexBPayload");
		}
		try
		{
			DecodedVideoFrame decodedVideoFrame = DrainOutput();
			try
			{
				using IMFSample sample = CreateInputSample(annexBPayload, presentationTimeMicroseconds, keyFrame);
				_transform.ProcessInput(0, sample, 0);
				DecodedVideoFrame decodedVideoFrame2 = DrainOutput();
				if (decodedVideoFrame2 != null)
				{
					decodedVideoFrame?.Dispose();
					decodedVideoFrame = decodedVideoFrame2;
				}
				DecodedVideoFrame result = decodedVideoFrame;
				decodedVideoFrame = null;
				return result;
			}
			finally
			{
				decodedVideoFrame?.Dispose();
			}
		}
		catch (Exception ex) when ((ex is SharpGenException || ex is COMException) ? true : false)
		{
			throw new MediaDecoderException("MEDIA_H264_DECODE_FAILED", "Windows H.264 解码器处理视频包失败", ex);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		if ((object)_transform != null)
		{
			try
			{
				_transform.ProcessMessage(TMessageType.MessageNotifyEndOfStream, 0u);
				_transform.ProcessMessage(TMessageType.MessageNotifyEndStreaming, 0u);
			}
			catch (SharpGenException)
			{
			}
			_transform.Dispose();
		}
		if (_mediaFoundationStarted)
		{
			MediaFactory.MFShutdown();
			_mediaFoundationStarted = false;
		}
	}

	public DecodedVideoFrame? Drain()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		try
		{
			_transform.ProcessMessage(TMessageType.MessageNotifyEndOfStream, 0u);
			_transform.ProcessMessage(TMessageType.MessageCommandDrain, 0u);
			return DrainOutput();
		}
		catch (Exception ex) when ((ex is SharpGenException || ex is COMException) ? true : false)
		{
			throw new MediaDecoderException("MEDIA_H264_DRAIN_FAILED", "Windows H.264 解码器结束视频流失败", ex);
		}
	}

	private static (IMFTransform Transform, string Name)? TryCreateHardwareDecoder()
	{
		RegisterTypeInfo inputType = new RegisterTypeInfo
		{
			GuidMajorType = MediaTypeGuids.Video,
			GuidSubtype = VideoFormatGuids.H264Es
		};
		return TryActivateDecoder(inputType, EnumFlag.EnumFlagAsyncmft | EnumFlag.EnumFlagHardware | EnumFlag.EnumFlagSortandfilter, "hardware");
	}

	private static (IMFTransform Transform, string Name) CreateSoftwareDecoder()
	{
		RegisterTypeInfo inputType = new RegisterTypeInfo
		{
			GuidMajorType = MediaTypeGuids.Video,
			GuidSubtype = VideoFormatGuids.H264Es
		};
		(IMFTransform, string)? tuple = TryActivateDecoder(inputType, EnumFlag.EnumFlagSyncmft | EnumFlag.EnumFlagSortandfilter, "software");
		if (tuple.HasValue)
		{
			return tuple.Value;
		}
		throw new MediaDecoderException("MEDIA_H264_DECODER_NOT_FOUND", "Windows 没有可用的 H.264 解码器");
	}

	private static (IMFTransform Transform, string Name)? TryActivateDecoder(RegisterTypeInfo inputType, EnumFlag flags, string kind)
	{
		using IMFActivateCollection iMFActivateCollection = MediaFactory.MFTEnumEx(TransformCategoryGuids.VideoDecoder, checked((uint)flags), inputType, null);
		using (IEnumerator<IMFActivate> enumerator = iMFActivateCollection.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				IMFActivate current = enumerator.Current;
				using (current)
				{
					string text = (string.IsNullOrWhiteSpace(current.FriendlyName) ? "Windows Media Foundation H.264" : current.FriendlyName);
					return (current.ActivateObject<IMFTransform>(), text + " (" + kind + ")");
				}
			}
		}
		return null;
	}

	private void ConfigureTransform()
	{
		using (IMFAttributes iMFAttributes = _transform.Attributes)
		{
			iMFAttributes.Set(SinkWriterAttributeKeys.LowLatency, 1u).CheckError();
		}
		checked
		{
			using IMFMediaType iMFMediaType = MediaFactory.MFCreateMediaType();
			iMFMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video).CheckError();
			iMFMediaType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264Es).CheckError();
			iMFMediaType.Set(MediaTypeAttributeKeys.InterlaceMode, 7u).CheckError();
			iMFMediaType.SetBlob(MediaTypeAttributeKeys.MpegSequenceHeader, _codecConfiguration).CheckError();
			MediaFactory.MFSetAttributeSize(iMFMediaType, MediaTypeAttributeKeys.FrameSize, (uint)_width, (uint)_height).CheckError();
			MediaFactory.MFSetAttributeRatio(iMFMediaType, MediaTypeAttributeKeys.FrameRate, (uint)_framesPerSecond, 1u).CheckError();
			MediaFactory.MFSetAttributeRatio(iMFMediaType, MediaTypeAttributeKeys.PixelAspectRatio, 1u, 1u).CheckError();
			_transform.SetInputType(0, iMFMediaType, 0);
			SelectNv12OutputType();
			_transform.ProcessMessage(TMessageType.MessageNotifyBeginStreaming, 0u);
			_transform.ProcessMessage(TMessageType.MessageNotifyStartOfStream, 0u);
		}
	}

	private void SelectNv12OutputType()
	{
		for (int i = 0; i < 64; i = checked(i + 1))
		{
			IMFMediaType iMFMediaType = null;
			try
			{
				iMFMediaType = _transform.GetOutputAvailableType(0, i);
				if (iMFMediaType.GetGUID(MediaTypeAttributeKeys.Subtype) != VideoFormatGuids.NV12)
				{
					continue;
				}
				_transform.SetOutputType(0, iMFMediaType, 0);
				UpdateOutputGeometry(iMFMediaType);
				return;
			}
			catch (SharpGenException ex) when (i > 0 && ex.ResultCode == ResultCode.NoMoreTypes)
			{
				break;
			}
			finally
			{
				iMFMediaType?.Dispose();
			}
		}
		throw new MediaDecoderException("MEDIA_H264_NV12_NOT_SUPPORTED", "Windows H.264 解码器不支持 NV12 输出");
	}

	private void UpdateOutputGeometry(IMFMediaType outputType)
	{
		checked
		{
			if (MediaFactory.MFGetAttributeSize(outputType, MediaTypeAttributeKeys.FrameSize, out var width, out var height).Success)
			{
				_width = (int)width;
				_height = (int)height;
			}
		}
		if (outputType.GetUInt32(MediaTypeAttributeKeys.DefaultStride, out var punValue).Success)
		{
			_stride = Math.Abs((int)punValue);
		}
		else
		{
			_stride = _width;
		}
	}

	private unsafe IMFSample CreateInputSample(ReadOnlySpan<byte> payload, long presentationTimeMicroseconds, bool keyFrame)
	{
		bool flag = _prependConfiguration & keyFrame;
		int num = checked(payload.Length + (flag ? _codecConfiguration.Length : 0));
		IMFMediaBuffer iMFMediaBuffer = null;
		IMFSample iMFSample = null;
		try
		{
			iMFMediaBuffer = MediaFactory.MFCreateMemoryBuffer(num);
			iMFMediaBuffer.Lock(out var ppbBuffer, out var _, out var _);
			try
			{
				Span<byte> destination = new Span<byte>((void*)checked((nuint)ppbBuffer), num);
				int start = 0;
				if (flag)
				{
					_codecConfiguration.CopyTo(destination);
					start = _codecConfiguration.Length;
					_prependConfiguration = false;
				}
				payload.CopyTo(destination.Slice(start));
			}
			finally
			{
				iMFMediaBuffer.Unlock();
			}
			iMFMediaBuffer.CurrentLength = num;
			iMFSample = MediaFactory.MFCreateSample();
			iMFSample.AddBuffer(iMFMediaBuffer);
			iMFSample.SampleTime = checked(presentationTimeMicroseconds * 10);
			iMFSample.SampleDuration = 10000000L / (long)_framesPerSecond;
			if (keyFrame)
			{
				iMFSample.Set(SampleAttributeKeys.CleanPoint, value: true).CheckError();
			}
			IMFSample result = iMFSample;
			iMFSample = null;
			return result;
		}
		finally
		{
			iMFSample?.Dispose();
			iMFMediaBuffer?.Dispose();
		}
	}

	private DecodedVideoFrame? DrainOutput()
	{
		DecodedVideoFrame decodedVideoFrame = null;
		try
		{
			for (int i = 0; i < 4; i = checked(i + 1))
			{
				DecodedVideoFrame decodedVideoFrame2 = TryReadOutput(out var needsMoreInput);
				if (decodedVideoFrame2 != null)
				{
					decodedVideoFrame?.Dispose();
					decodedVideoFrame = decodedVideoFrame2;
				}
				if (needsMoreInput)
				{
					break;
				}
			}
			DecodedVideoFrame result = decodedVideoFrame;
			decodedVideoFrame = null;
			return result;
		}
		finally
		{
			decodedVideoFrame?.Dispose();
		}
	}

	private DecodedVideoFrame? TryReadOutput(out bool needsMoreInput)
	{
		needsMoreInput = false;
		OutputStreamInfo outputStreamInfo = _transform.GetOutputStreamInfo(0);
		bool flag = (outputStreamInfo.Flags & 0x100) != 0;
		IMFSample iMFSample = null;
		IMFMediaBuffer iMFMediaBuffer = null;
		OutputDataBuffer outputSamples = new OutputDataBuffer
		{
			StreamID = 0
		};
		try
		{
			if (!flag)
			{
				int val = checked(_stride * _height * 3) / 2;
				int maxLength = Math.Max(outputStreamInfo.Size, val);
				iMFMediaBuffer = MediaFactory.MFCreateMemoryBuffer(maxLength);
				iMFSample = MediaFactory.MFCreateSample();
				iMFSample.AddBuffer(iMFMediaBuffer);
				outputSamples.Sample = iMFSample;
			}
			Result result = _transform.ProcessOutput(ProcessOutputFlags.None, 1, ref outputSamples, out var _);
			if (result == ResultCode.TransformNeedMoreInput)
			{
				needsMoreInput = true;
				return null;
			}
			if (result == ResultCode.TransformStreamChange)
			{
				SelectNv12OutputType();
				return null;
			}
			result.CheckError();
			IMFSample sample = outputSamples.Sample ?? throw new MediaDecoderException("MEDIA_H264_OUTPUT_MISSING", "Windows H.264 解码器没有返回视频帧");
			return CopyDecodedFrame(sample);
		}
		finally
		{
			outputSamples.Events?.Dispose();
			if ((object)outputSamples.Sample != null && ((object)iMFSample == null || outputSamples.Sample.NativePointer != iMFSample.NativePointer))
			{
				outputSamples.Sample.Dispose();
			}
			iMFSample?.Dispose();
			iMFMediaBuffer?.Dispose();
		}
	}

	private unsafe DecodedVideoFrame CopyDecodedFrame(IMFSample sample)
	{
		using IMFMediaBuffer iMFMediaBuffer = sample.ConvertToContiguousBuffer();
		iMFMediaBuffer.Lock(out var ppbBuffer, out var _, out var pcbCurrentLength);
		IMemoryOwner<byte> memoryOwner = null;
		try
		{
			if (pcbCurrentLength < 1)
			{
				throw new MediaDecoderException("MEDIA_H264_OUTPUT_EMPTY", "Windows H.264 解码器返回了空视频帧");
			}
			memoryOwner = MemoryPool<byte>.Shared.Rent(pcbCurrentLength);
			new ReadOnlySpan<byte>((void*)checked((nuint)ppbBuffer), pcbCurrentLength).CopyTo(memoryOwner.Memory.Span);
			long presentationTimeMicroseconds = sample.SampleTime / 10;
			DecodedVideoFrame result = new DecodedVideoFrame(_width, _height, _stride, _visibleWidth, _visibleHeight, VideoPixelFormat.Nv12, presentationTimeMicroseconds, memoryOwner, pcbCurrentLength);
			memoryOwner = null;
			return result;
		}
		finally
		{
			memoryOwner?.Dispose();
			iMFMediaBuffer.Unlock();
		}
	}
}
