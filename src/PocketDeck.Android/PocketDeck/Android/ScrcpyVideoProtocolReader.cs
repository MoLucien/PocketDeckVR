using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class ScrcpyVideoProtocolReader(Stream stream)
{
	private const int _deviceNameFieldLength = 64;

	private const int _packetHeaderLength = 12;

	private const int _maximumPacketLength = 16777216;

	private const ulong _configurationFlag = 4611686018427387904uL;

	private const ulong _keyFrameFlag = 2305843009213693952uL;

	private const ulong _presentationTimeMask = 2305843009213693951uL;

	private readonly Stream _stream = stream ?? throw new ArgumentNullException("stream");

	public async ValueTask<ScrcpyVideoHandshake> ReadHandshakeAsync(CancellationToken cancellationToken)
	{
		byte[] deviceNameBuffer = new byte[64];
		await _stream.ReadExactlyAsync(deviceNameBuffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		int num = Array.IndexOf(deviceNameBuffer, (byte)0);
		int count = ((num < 0) ? 64 : num);
		string deviceName = Encoding.UTF8.GetString(deviceNameBuffer, 0, count);
		byte[] codecBuffer = new byte[4];
		await _stream.ReadExactlyAsync(codecBuffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		uint num2 = BinaryPrimitives.ReadUInt32BigEndian(codecBuffer);
		return new ScrcpyVideoHandshake(deviceName, num2 switch
		{
			1748121140u => AndroidVideoCodec.H264, 
			1748121141u => AndroidVideoCodec.H265, 
			6387249u => AndroidVideoCodec.Av1, 
			7761976u => AndroidVideoCodec.Vp8, 
			7761977u => AndroidVideoCodec.Vp9, 
			0u => throw new AndroidConnectionException("ANDROID_VIDEO_DISABLED", "手机关闭了视频流"), 
			1u => throw new AndroidConnectionException("ANDROID_VIDEO_CONFIGURATION_FAILED", "手机视频编码器配置失败"), 
			_ => throw new AndroidConnectionException("ANDROID_VIDEO_CODEC_UNKNOWN", $"手机返回了不支持的视频编码标识 0x{num2:x8}"), 
		});
	}

	public async ValueTask<AndroidVideoStreamItem> ReadNextAsync(CancellationToken cancellationToken)
	{
		byte[] header = new byte[12];
		await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		checked
		{
			if ((header[0] & 0x80) != 0)
			{
				int num = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
				int num2 = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4));
				bool flag = ((num < 1 || num > 16384) ? true : false);
				bool flag2 = flag;
				bool flag3 = flag2;
				if (!flag3)
				{
					bool flag4 = ((num2 < 1 || num2 > 16384) ? true : false);
					flag3 = flag4;
				}
				if (flag3)
				{
					throw new AndroidConnectionException("ANDROID_VIDEO_SIZE_INVALID", $"手机返回了无效视频尺寸 {num}x{num2}");
				}
				return new AndroidVideoStreamItem(AndroidVideoStreamItemKind.Session, num, num2, (header[3] & 1) != 0, null, keyFrame: false, ReadOnlyMemory<byte>.Empty, null);
			}
			ulong presentationTimeAndFlags = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(0, 8));
			uint num3 = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4));
			if ((num3 > 16777216 || num3 == 0) ? true : false)
			{
				throw new AndroidConnectionException("ANDROID_VIDEO_PACKET_SIZE_INVALID", $"手机返回了无效视频包长度 {num3}");
			}
			int num4 = (int)num3;
			IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(num4);
			try
			{
				Memory<byte> payload = owner.Memory.Slice(0, num4);
				await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				bool flag5 = (presentationTimeAndFlags & 0x4000000000000000L) != 0;
				bool keyFrame = (presentationTimeAndFlags & 0x2000000000000000L) != 0;
				long? presentationTimeMicroseconds = (flag5 ? ((long?)null) : new long?((long)(presentationTimeAndFlags & 0x1FFFFFFFFFFFFFFFL)));
				return new AndroidVideoStreamItem(flag5 ? AndroidVideoStreamItemKind.Configuration : AndroidVideoStreamItemKind.Media, 0, 0, clientResized: false, presentationTimeMicroseconds, keyFrame, payload, owner);
			}
			catch
			{
				owner.Dispose();
				throw;
			}
		}
	}
}
