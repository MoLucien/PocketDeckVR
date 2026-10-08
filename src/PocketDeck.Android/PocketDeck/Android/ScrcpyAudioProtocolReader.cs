using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class ScrcpyAudioProtocolReader(Stream stream)
{
	private const int _deviceNameFieldLength = 64;

	private const int _packetHeaderLength = 12;

	private const int _maximumPacketLength = 1048576;

	private const ulong _configurationFlag = 4611686018427387904uL;

	private const ulong _presentationTimeMask = 2305843009213693951uL;

	private readonly Stream _stream = stream ?? throw new ArgumentNullException("stream");

	public async ValueTask<ScrcpyAudioHandshake> ReadHandshakeAsync(CancellationToken cancellationToken)
	{
		byte[] nameBuffer = new byte[64];
		await _stream.ReadExactlyAsync(nameBuffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		int num = Array.IndexOf(nameBuffer, (byte)0);
		string deviceName = Encoding.UTF8.GetString(nameBuffer, 0, (num < 0) ? nameBuffer.Length : num);
		byte[] codecBuffer = new byte[4];
		await _stream.ReadExactlyAsync(codecBuffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		uint num2 = BinaryPrimitives.ReadUInt32BigEndian(codecBuffer);
		return new ScrcpyAudioHandshake(deviceName, num2 switch
		{
			1869641075u => AndroidAudioCodec.Opus, 
			6381923u => AndroidAudioCodec.Aac, 
			1718378851u => AndroidAudioCodec.Flac, 
			7496055u => AndroidAudioCodec.Raw, 
			0u => throw new AndroidConnectionException("ANDROID_AUDIO_DISABLED", "此 Android 版本或当前应用不允许捕获内部音频"), 
			1u => throw new AndroidConnectionException("ANDROID_AUDIO_CONFIGURATION_FAILED", "手机内部音频编码器配置失败"), 
			_ => throw new AndroidConnectionException("ANDROID_AUDIO_CODEC_UNKNOWN", $"手机返回了不支持的音频编码标识 0x{num2:x8}"), 
		});
	}

	public async ValueTask<AndroidAudioStreamItem> ReadNextAsync(CancellationToken cancellationToken)
	{
		byte[] header = new byte[12];
		await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ulong presentationTimeAndFlags = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(0, 8));
		uint num = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4));
		if ((num > 1048576 || num == 0) ? true : false)
		{
			throw new AndroidConnectionException("ANDROID_AUDIO_PACKET_SIZE_INVALID", $"手机返回了无效音频包长度 {num}");
		}
		checked
		{
			int num2 = (int)num;
			IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(num2);
			try
			{
				Memory<byte> payload = owner.Memory.Slice(0, num2);
				await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				bool flag = (presentationTimeAndFlags & 0x4000000000000000L) != 0;
				long? presentationTimeMicroseconds = (flag ? ((long?)null) : new long?((long)(presentationTimeAndFlags & 0x1FFFFFFFFFFFFFFFL)));
				AndroidAudioStreamItem result = new AndroidAudioStreamItem((!flag) ? AndroidAudioStreamItemKind.Media : AndroidAudioStreamItemKind.Configuration, presentationTimeMicroseconds, payload, owner);
				owner = null;
				return result;
			}
			finally
			{
				owner?.Dispose();
			}
		}
	}
}
