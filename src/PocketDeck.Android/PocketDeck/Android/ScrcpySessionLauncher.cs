using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed class ScrcpySessionLauncher
{
	private static readonly TimeSpan _serverPushTimeout = TimeSpan.FromSeconds(15L);

	private static readonly TimeSpan _forwardTimeout = TimeSpan.FromSeconds(5L);

	private static readonly TimeSpan _connectAttemptTimeout = TimeSpan.FromMilliseconds(750L);

	private static readonly TimeSpan _connectRetryDelay = TimeSpan.FromMilliseconds(100L);

	private readonly AdbCommandRunner _commandRunner;

	private readonly IAndroidConnectionLogSink _log;

	private readonly Lazy<Task<string>> _validatedServerPath;

	private readonly ConcurrentDictionary<ScrcpyDeviceSessionKey, Lazy<Task<AdbCommandResult>>> _serverPushes = new ConcurrentDictionary<ScrcpyDeviceSessionKey, Lazy<Task<AdbCommandResult>>>();

	public ScrcpySessionLauncher(AdbCommandRunner commandRunner, string serverPath, IAndroidConnectionLogSink log)
	{
		_commandRunner = commandRunner ?? throw new ArgumentNullException("commandRunner");
		ArgumentException.ThrowIfNullOrWhiteSpace(serverPath, "serverPath");
		_log = log ?? throw new ArgumentNullException("log");
		string fullServerPath = Path.GetFullPath(serverPath);
		_validatedServerPath = new Lazy<Task<string>>(() => ScrcpyServerResourceValidator.ValidateAsync(fullServerPath, CancellationToken.None).AsTask(), LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public async ValueTask<ScrcpyTransportLease> LaunchAsync(ScrcpySessionLaunchRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		ArgumentNullException.ThrowIfNull(request.BuildServerArguments, "request.BuildServerArguments");
		cancellationToken.ThrowIfCancellationRequested();
		string prefix = ReasonCodePrefix(request.Kind);
		string channelName = ChannelName(request.Kind);
		string validatedServer = await AwaitWithinDeadlineAsync(_validatedServerPath.Value, request.Deadline, prefix + "_SERVER_VALIDATION_TIMEOUT", "校验手机" + channelName + "服务组件超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await EnsureServerPushedAsync(request.Device, validatedServer, request.Deadline, request.Kind, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		int scid = RandomNumberGenerator.GetInt32(1, int.MaxValue);
		string text = $"scrcpy_{scid:x8}";
		AdbCommandResult adbCommandResult = await RunCommandWithinDeadlineAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5]
		{
			"-s",
			request.Device.Serial,
			"forward",
			"tcp:0",
			"localabstract:" + text
		}), _forwardTimeout, request.Deadline, prefix + "_FORWARD_TIMEOUT", "建立手机" + channelName + "通道超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		EnsureCommand(adbCommandResult, prefix + "_FORWARD", "无法建立手机" + channelName + "通道");
		bool flag = !int.TryParse(adbCommandResult.StandardOutput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var localPort);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((localPort < 1 || localPort > 65535) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			throw new AndroidConnectionException(prefix + "_FORWARD_PORT_INVALID", "手机" + channelName + "通道返回了无效端口");
		}
		AdbManagedProcess serverProcess = null;
		TcpClient tcpClient = null;
		try
		{
			serverProcess = AdbManagedProcess.Start(_commandRunner.ExecutablePath, EnsurePersistentServerArguments(request.BuildServerArguments(scid)));
			tcpClient = await ConnectSocketAsync(localPort, serverProcess, request.Device, request.Kind, request.Deadline, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ScrcpyTransportLease result = new ScrcpyTransportLease(request.Device, request.Kind, localPort, tcpClient, serverProcess, _commandRunner, _log);
			tcpClient = null;
			serverProcess = null;
			return result;
		}
		catch
		{
			tcpClient?.Dispose();
			if (serverProcess != null)
			{
				await DisposeServerProcessAsync(serverProcess, request.Device, request.Kind).ConfigureAwait(continueOnCapturedContext: false);
			}
			await RemoveForwardAsync(request.Device, request.Kind, localPort).ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
	}

	public ValueTask<AdbCommandResult> RunDeviceCommandAsync(ResolvedAndroidDevice device, IReadOnlyList<string> deviceArguments, TimeSpan maximumTimeout, OperationDeadline deadline, string timeoutReasonCode, string timeoutMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(deviceArguments, "deviceArguments");
		List<string> list = new List<string>(checked(deviceArguments.Count + 2)) { "-s", device.Serial };
		list.AddRange(deviceArguments);
		return RunCommandWithinDeadlineAsync(list, maximumTimeout, deadline, timeoutReasonCode, timeoutMessage, cancellationToken);
	}

	private async ValueTask EnsureServerPushedAsync(ResolvedAndroidDevice device, string validatedServer, OperationDeadline deadline, ScrcpySessionKind kind, CancellationToken cancellationToken)
	{
		ScrcpyDeviceSessionKey key = new ScrcpyDeviceSessionKey(device.DeviceKey, device.SessionEpoch);
		Lazy<Task<AdbCommandResult>> pendingPush = _serverPushes.GetOrAdd(key, (ScrcpyDeviceSessionKey _) => new Lazy<Task<AdbCommandResult>>(() => _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "-s", device.Serial, "push", validatedServer, "/data/local/tmp/vrphonescreen-scrcpy-server-v4.1.jar" }), _serverPushTimeout, CancellationToken.None).AsTask(), LazyThreadSafetyMode.ExecutionAndPublication));
		string prefix = ReasonCodePrefix(kind);
		Task<AdbCommandResult> pushTask = pendingPush.Value;
		AdbCommandResult adbCommandResult;
		try
		{
			adbCommandResult = await AwaitWithinDeadlineAsync(pushTask, deadline, prefix + "_SERVER_PUSH_TIMEOUT", "把手机" + ChannelName(kind) + "服务发送到设备超时", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			if (pushTask.IsFaulted || pushTask.IsCanceled)
			{
				RemovePushIfCurrent(key, pendingPush);
			}
			throw;
		}
		if (!adbCommandResult.Succeeded)
		{
			RemovePushIfCurrent(key, pendingPush);
		}
		EnsureCommand(adbCommandResult, prefix + "_SERVER_PUSH", "无法把" + ChannelName(kind) + "服务发送到手机");
		RemoveStalePushEntries(device.DeviceKey, device.SessionEpoch);
		WriteLog("scrcpy_server_ready", "ANDROID_SCRCPY_SERVER_READY", "scrcpy 服务组件已在当前 USB 会话中就绪", device.DeviceKey);
	}

	private async ValueTask<AdbCommandResult> RunCommandWithinDeadlineAsync(IReadOnlyList<string> arguments, TimeSpan maximumTimeout, OperationDeadline deadline, string timeoutReasonCode, string timeoutMessage, CancellationToken cancellationToken)
	{
		TimeSpan remainingUpTo = deadline.GetRemainingUpTo(maximumTimeout);
		if (remainingUpTo == TimeSpan.Zero)
		{
			throw new AndroidConnectionException(timeoutReasonCode, timeoutMessage);
		}
		return await _commandRunner.RunAsync(arguments, remainingUpTo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async ValueTask<T> AwaitWithinDeadlineAsync<T>(Task<T> task, OperationDeadline deadline, string timeoutReasonCode, string timeoutMessage, CancellationToken cancellationToken)
	{
		TimeSpan remaining = deadline.Remaining;
		if (remaining == TimeSpan.Zero)
		{
			throw new AndroidConnectionException(timeoutReasonCode, timeoutMessage);
		}
		try
		{
			return await task.WaitAsync(remaining, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (TimeoutException)
		{
			throw new AndroidConnectionException(timeoutReasonCode, timeoutMessage);
		}
	}

	internal static IReadOnlyList<string> EnsurePersistentServerArguments(IReadOnlyList<string> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments, "arguments");
		if (arguments.Contains("cleanup=false", StringComparer.Ordinal))
		{
			return arguments;
		}
		List<string> list = new List<string>(checked(arguments.Count + 1));
		list.AddRange(arguments);
		list.Add("cleanup=false");
		return list;
	}

	private async ValueTask<TcpClient> ConnectSocketAsync(int localPort, AdbManagedProcess serverProcess, ResolvedAndroidDevice device, ScrcpySessionKind kind, OperationDeadline deadline, CancellationToken cancellationToken)
	{
		string prefix = ReasonCodePrefix(kind);
		string channelName = ChannelName(kind);
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (deadline.IsExpired)
			{
				throw new AndroidConnectionException(prefix + "_CONNECT_TIMEOUT", "等待手机" + channelName + "服务连接超时");
			}
			if (serverProcess.HasExited)
			{
				string text = SanitizeServerOutput(serverProcess.GetOutputTail(), device.Serial);
				if (!string.IsNullOrWhiteSpace(text))
				{
					WriteLog("scrcpy_server_exit_diagnostic", prefix + "_SERVER_EXIT_DIAGNOSTIC", "手机" + channelName + "服务启动输出：" + text, device.DeviceKey);
				}
				throw new AndroidConnectionException(prefix + "_SERVER_EXITED", "手机" + channelName + "服务在建立连接前退出");
			}
			TcpClient client = new TcpClient(AddressFamily.InterNetwork)
			{
				NoDelay = true
			};
			try
			{
				TimeSpan remainingUpTo = deadline.GetRemainingUpTo(_connectAttemptTimeout);
				if (remainingUpTo == TimeSpan.Zero)
				{
					throw new AndroidConnectionException(prefix + "_CONNECT_TIMEOUT", "等待手机" + channelName + "服务连接超时");
				}
				using CancellationTokenSource attemptSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				attemptSource.CancelAfter(remainingUpTo);
				await client.ConnectAsync(IPAddress.Loopback, localPort, attemptSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				byte[] dummy = new byte[1];
				await client.GetStream().ReadExactlyAsync(dummy, attemptSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (dummy[0] != 0)
				{
					throw new IOException("Invalid scrcpy forward tunnel byte.");
				}
				TcpClient result = client;
				client = null;
				return result;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex2) when ((ex2 is SocketException || ex2 is IOException || ex2 is OperationCanceledException) ? true : false)
			{
				if (deadline.IsExpired)
				{
					throw new AndroidConnectionException(prefix + "_CONNECT_TIMEOUT", "等待手机" + channelName + "服务连接超时");
				}
			}
			finally
			{
				client?.Dispose();
			}
			TimeSpan remainingUpTo2 = deadline.GetRemainingUpTo(_connectRetryDelay);
			if (remainingUpTo2 == TimeSpan.Zero)
			{
				break;
			}
			await Task.Delay(remainingUpTo2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		throw new AndroidConnectionException(prefix + "_CONNECT_TIMEOUT", "等待手机" + channelName + "服务连接超时");
	}

	internal static string SanitizeServerOutput(string output, string serial)
	{
		string text = output.Replace(serial, "<device>", StringComparison.Ordinal);
		if (text.Length > 800)
		{
			return text.Substring(text.Length - 800);
		}
		return text;
	}

	private async ValueTask RemoveForwardAsync(ResolvedAndroidDevice device, ScrcpySessionKind kind, int localPort)
	{
		try
		{
			if (!(await _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5]
			{
				"-s",
				device.Serial,
				"forward",
				"--remove",
				$"tcp:{localPort}"
			}), TimeSpan.FromSeconds(3L), CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false)).Succeeded)
			{
				WriteLog("scrcpy_forward_cleanup_failed", ReasonCodePrefix(kind) + "_FORWARD_CLEANUP_FAILED", "手机" + ChannelName(kind) + "端口转发清理失败", device.DeviceKey);
			}
		}
		catch (Exception exception) when (IsExpectedCleanupFailure(exception))
		{
			WriteLog("scrcpy_forward_cleanup_failed", ReasonCodePrefix(kind) + "_FORWARD_CLEANUP_FAILED", "手机" + ChannelName(kind) + "端口转发清理失败", device.DeviceKey);
		}
	}

	private async ValueTask DisposeServerProcessAsync(AdbManagedProcess serverProcess, ResolvedAndroidDevice device, ScrcpySessionKind kind)
	{
		try
		{
			await serverProcess.StopAsync(TimeSpan.FromSeconds(2L)).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception) when (IsExpectedCleanupFailure(exception))
		{
			WriteLog("scrcpy_server_cleanup_failed", ReasonCodePrefix(kind) + "_SERVER_CLEANUP_FAILED", "手机" + ChannelName(kind) + "服务进程停止失败", device.DeviceKey);
		}
		try
		{
			await serverProcess.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception2) when (IsExpectedCleanupFailure(exception2))
		{
			WriteLog("scrcpy_server_cleanup_failed", ReasonCodePrefix(kind) + "_SERVER_CLEANUP_FAILED", "手机" + ChannelName(kind) + "服务进程释放失败", device.DeviceKey);
		}
	}

	private void RemovePushIfCurrent(ScrcpyDeviceSessionKey key, Lazy<Task<AdbCommandResult>> expected)
	{
		if (_serverPushes.TryGetValue(key, out Lazy<Task<AdbCommandResult>> value) && value == expected)
		{
			_serverPushes.TryRemove(key, out Lazy<Task<AdbCommandResult>> _);
		}
	}

	private static bool IsExpectedCleanupFailure(Exception exception)
	{
		if (exception is IOException || exception is InvalidOperationException || exception is UnauthorizedAccessException || exception is Win32Exception)
		{
			return true;
		}
		return false;
	}

	private void RemoveStalePushEntries(string deviceKey, long currentEpoch)
	{
		foreach (ScrcpyDeviceSessionKey key in _serverPushes.Keys)
		{
			if (string.Equals(key.DeviceKey, deviceKey, StringComparison.Ordinal) && key.SessionEpoch != currentEpoch)
			{
				_serverPushes.TryRemove(key, out Lazy<Task<AdbCommandResult>> _);
			}
		}
	}

	private static void EnsureCommand(AdbCommandResult result, string operation, string message)
	{
		if (result.TimedOut)
		{
			throw new AndroidConnectionException(operation + "_TIMEOUT", message + "：操作超时");
		}
		if (!result.Succeeded)
		{
			throw new AndroidConnectionException(operation + "_FAILED", message);
		}
	}

	internal static string ReasonCodePrefix(ScrcpySessionKind kind)
	{
		return kind switch
		{
			ScrcpySessionKind.Video => "ANDROID_VIDEO", 
			ScrcpySessionKind.Audio => "ANDROID_AUDIO", 
			ScrcpySessionKind.Control => "ANDROID_CONTROL", 
			_ => throw new ArgumentOutOfRangeException("kind"), 
		};
	}

	internal static string ChannelName(ScrcpySessionKind kind)
	{
		return kind switch
		{
			ScrcpySessionKind.Video => "视频", 
			ScrcpySessionKind.Audio => "音频", 
			ScrcpySessionKind.Control => "控制", 
			_ => throw new ArgumentOutOfRangeException("kind"), 
		};
	}

	private void WriteLog(string eventName, string reasonCode, string message, string deviceKey)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, reasonCode, message, deviceKey));
	}
}
