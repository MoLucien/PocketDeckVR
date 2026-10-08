using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpyControlSession(string deviceKey, ScrcpyTransportLease transport, ScrcpyControlProtocolWriter protocol, AndroidDisplaySize displaySize, IAndroidConnectionLogSink log) : IAndroidControlSession, IAsyncDisposable
{
	private readonly ScrcpyTransportLease _transport = transport;

	private readonly ScrcpyControlProtocolWriter _protocol = protocol;

	private readonly AndroidDisplaySize _displaySize = displaySize;

	private readonly IAndroidConnectionLogSink _log = log;

	private long _commandCount;

	private long _moveCount;

	private bool _disposed;

	public string DeviceKey { get; } = deviceKey;

	public async ValueTask SendAsync(PhoneInputCommand command, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_transport.HasServerExited)
		{
			throw new AndroidConnectionException("ANDROID_CONTROL_SERVER_EXITED", "手机控制服务已经停止");
		}
		try
		{
			PhoneInputCommand command2 = _displaySize.Map(command);
			await _protocol.WriteAsync(command2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (command.Kind == PhoneInputCommandKind.WakeScreen)
			{
				await SendAndroidWakeKeyAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is SocketException) ? true : false)
		{
			throw new AndroidConnectionException("ANDROID_CONTROL_WRITE_FAILED", "手机控制指令发送失败");
		}
		checked
		{
			_commandCount++;
			if (command.Kind == PhoneInputCommandKind.PointerMove)
			{
				_moveCount++;
				return;
			}
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			IAndroidConnectionLogSink log = _log;
			string message = $"手机控制指令已发送：{command.Kind}";
			string deviceKey = DeviceKey;
			long? durationMilliseconds = Math.Max(0L, (long)(utcNow - command.CreatedAt).TotalMilliseconds);
			long? commandSequence = command.Sequence;
			string commandKind = command.Kind.ToString();
			log.TryWrite(new AndroidConnectionLogEntry(utcNow, "control_command", "ANDROID_CONTROL_COMMAND_SENT", message, deviceKey, null, null, durationMilliseconds, null, null, null, null, null, null, null, null, null, commandSequence, commandKind));
		}
	}

	private async ValueTask SendAndroidWakeKeyAsync(CancellationToken cancellationToken)
	{
		try
		{
			AdbCommandResult adbCommandResult = await _transport.RunDeviceCommandAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "shell", "input", "keyevent", "224" }), TimeSpan.FromSeconds(2L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!adbCommandResult.Succeeded)
			{
				IAndroidConnectionLogSink log = _log;
				DateTimeOffset utcNow = DateTimeOffset.UtcNow;
				string reasonCode = (adbCommandResult.TimedOut ? "ANDROID_WAKE_KEY_TIMEOUT" : "ANDROID_WAKE_KEY_FAILED");
				string deviceKey = DeviceKey;
				long? durationMilliseconds = checked((long)adbCommandResult.Duration.TotalMilliseconds);
				log.TryWrite(new AndroidConnectionLogEntry(utcNow, "control_wake_fallback", reasonCode, "scrcpy 唤醒指令已发送，但 Android 唤醒键发送失败", deviceKey, null, null, durationMilliseconds));
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is InvalidOperationException) ? true : false)
		{
			_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, "control_wake_fallback", "ANDROID_WAKE_KEY_FAILED", "scrcpy 唤醒指令已发送，但 Android 唤醒键发送失败", DeviceKey));
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _transport.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			IAndroidConnectionLogSink log = _log;
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			string message = $"手机控制通道已停止；指令 {_commandCount}；合并后 MOVE {_moveCount}";
			string deviceKey = DeviceKey;
			long? packetCount = _commandCount;
			log.TryWrite(new AndroidConnectionLogEntry(utcNow, "control_stopped", "ANDROID_CONTROL_STOPPED", message, deviceKey, null, null, null, null, null, null, null, null, null, null, packetCount));
		}
	}
}
