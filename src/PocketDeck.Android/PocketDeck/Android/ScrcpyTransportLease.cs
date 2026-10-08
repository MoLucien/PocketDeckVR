using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class ScrcpyTransportLease : IAsyncDisposable
{
	private readonly string _serial;

	private readonly int _localPort;

	private readonly TcpClient _tcpClient;

	private readonly AdbManagedProcess _serverProcess;

	private readonly AdbCommandRunner _commandRunner;

	private readonly IAndroidConnectionLogSink _log;

	private readonly ScrcpySessionKind _kind;

	private readonly object _disposeGate = new object();

	private Task? _disposeTask;

	private int _disposeRequested;

	public string DeviceKey { get; }

	public NetworkStream Stream
	{
		get
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(in _disposeRequested) != 0, this);
			return _tcpClient.GetStream();
		}
	}

	public bool HasServerExited
	{
		get
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(in _disposeRequested) != 0, this);
			return _serverProcess.HasExited;
		}
	}

	public ScrcpyTransportLease(ResolvedAndroidDevice device, ScrcpySessionKind kind, int localPort, TcpClient tcpClient, AdbManagedProcess serverProcess, AdbCommandRunner commandRunner, IAndroidConnectionLogSink log)
	{
		DeviceKey = device.DeviceKey;
		_serial = device.Serial;
		_kind = kind;
		_localPort = localPort;
		_tcpClient = tcpClient ?? throw new ArgumentNullException("tcpClient");
		_serverProcess = serverProcess ?? throw new ArgumentNullException("serverProcess");
		_commandRunner = commandRunner ?? throw new ArgumentNullException("commandRunner");
		_log = log ?? throw new ArgumentNullException("log");
	}

	public async ValueTask<AdbCommandResult> RunDeviceCommandAsync(IReadOnlyList<string> deviceArguments, TimeSpan timeout, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(in _disposeRequested) != 0, this);
		ArgumentNullException.ThrowIfNull(deviceArguments, "deviceArguments");
		List<string> list = new List<string>(checked(deviceArguments.Count + 2)) { "-s", _serial };
		list.AddRange(deviceArguments);
		return await _commandRunner.RunAsync(list, timeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public ValueTask DisposeAsync()
	{
		Task disposeTask;
		lock (_disposeGate)
		{
			if (_disposeTask == null)
			{
				_disposeTask = DisposeCoreAsync();
			}
			disposeTask = _disposeTask;
		}
		return new ValueTask(disposeTask);
	}

	private async Task DisposeCoreAsync()
	{
		Interlocked.Exchange(ref _disposeRequested, 1);
		_tcpClient.Dispose();
		try
		{
			await _serverProcess.StopAsync(TimeSpan.FromSeconds(2L)).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception) when (IsExpectedCleanupFailure(exception))
		{
			WriteCleanupFailure("scrcpy_server_cleanup_failed", "SERVER_CLEANUP");
		}
		try
		{
			await _serverProcess.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception2) when (IsExpectedCleanupFailure(exception2))
		{
			WriteCleanupFailure("scrcpy_server_cleanup_failed", "SERVER_CLEANUP");
		}
		try
		{
			if (!(await _commandRunner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[5]
			{
				"-s",
				_serial,
				"forward",
				"--remove",
				$"tcp:{_localPort}"
			}), TimeSpan.FromSeconds(3L), CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false)).Succeeded)
			{
				WriteCleanupFailure("scrcpy_forward_cleanup_failed", "FORWARD_CLEANUP");
			}
		}
		catch (Exception exception3) when (IsExpectedCleanupFailure(exception3))
		{
			WriteCleanupFailure("scrcpy_forward_cleanup_failed", "FORWARD_CLEANUP");
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

	private void WriteCleanupFailure(string eventName, string reasonSuffix)
	{
		_log.TryWrite(new AndroidConnectionLogEntry(DateTimeOffset.UtcNow, eventName, ScrcpySessionLauncher.ReasonCodePrefix(_kind) + "_" + reasonSuffix + "_FAILED", "手机" + ScrcpySessionLauncher.ChannelName(_kind) + "会话清理未完全成功", DeviceKey));
	}
}
