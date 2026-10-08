using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AdbManagedProcess : IAsyncDisposable
{
	private const int _maximumTailLines = 80;

	private readonly Process _process;

	private readonly Queue<string> _tail = new Queue<string>(80);

	private readonly object _tailGate = new object();

	private bool _disposed;

	public bool HasExited => _process.HasExited;

	private AdbManagedProcess(Process process)
	{
		_process = process;
		_process.OutputDataReceived += OnOutputDataReceived;
		_process.ErrorDataReceived += OnErrorDataReceived;
		_process.BeginOutputReadLine();
		_process.BeginErrorReadLine();
	}

	public static AdbManagedProcess Start(string executablePath, IReadOnlyList<string> arguments)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(executablePath, "executablePath");
		ArgumentNullException.ThrowIfNull(arguments, "arguments");
		string fullPath = Path.GetFullPath(executablePath);
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = fullPath,
			WorkingDirectory = Path.GetDirectoryName(fullPath),
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		foreach (string argument in arguments)
		{
			processStartInfo.ArgumentList.Add(argument);
		}
		Process process = new Process
		{
			StartInfo = processStartInfo
		};
		try
		{
			if (!process.Start())
			{
				throw new AndroidConnectionException("ANDROID_SERVER_START_FAILED", "无法启动手机服务");
			}
			return new AdbManagedProcess(process);
		}
		catch
		{
			process.Dispose();
			throw;
		}
	}

	public string GetOutputTail()
	{
		lock (_tailGate)
		{
			return string.Join(" | ", _tail);
		}
	}

	public async ValueTask StopAsync(TimeSpan gracefulTimeout)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(gracefulTimeout, TimeSpan.Zero, "gracefulTimeout");
		if (_process.HasExited)
		{
			return;
		}
		using CancellationTokenSource timeout = new CancellationTokenSource(gracefulTimeout);
		int num = 0;
		try
		{
			await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			num = 1;
		}
		if (num == 1)
		{
			TryKill();
			await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await StopAsync(TimeSpan.Zero).ConfigureAwait(continueOnCapturedContext: false);
			_process.OutputDataReceived -= OnOutputDataReceived;
			_process.ErrorDataReceived -= OnErrorDataReceived;
			_process.Dispose();
		}
	}

	private void OnOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
	{
		AddTail(eventArgs.Data);
	}

	private void OnErrorDataReceived(object sender, DataReceivedEventArgs eventArgs)
	{
		AddTail(eventArgs.Data);
	}

	private void AddTail(string? line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return;
		}
		lock (_tailGate)
		{
			if (_tail.Count == 80)
			{
				_tail.Dequeue();
			}
			_tail.Enqueue(line.Trim());
		}
	}

	private void TryKill()
	{
		try
		{
			if (!_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
			}
		}
		catch (InvalidOperationException)
		{
		}
	}
}
