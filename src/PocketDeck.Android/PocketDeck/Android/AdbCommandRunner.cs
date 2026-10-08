using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class AdbCommandRunner(string executablePath)
{
	private readonly string _executablePath = Path.GetFullPath(executablePath);

	public string ExecutablePath => _executablePath;

	public async ValueTask<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(arguments, "arguments");
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, "timeout");
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = _executablePath,
			WorkingDirectory = Path.GetDirectoryName(_executablePath),
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
		using Process process = new Process
		{
			StartInfo = processStartInfo
		};
		long startedAt = Stopwatch.GetTimestamp();
		if (!process.Start())
		{
			throw new InvalidOperationException("ANDROID_TOOL_START_FAILED");
		}
		Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout);
		using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
		bool timedOut = false;
		try
		{
			await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			timedOut = true;
			TryKill(process);
		}
		catch
		{
			TryKill(process);
			throw;
		}
		if (timedOut)
		{
			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		string output = await CompleteReadAsync(outputTask).ConfigureAwait(continueOnCapturedContext: false);
		string standardError = await CompleteReadAsync(errorTask).ConfigureAwait(continueOnCapturedContext: false);
		TimeSpan elapsedTime = Stopwatch.GetElapsedTime(startedAt);
		return new AdbCommandResult(timedOut ? (-1) : process.ExitCode, output, standardError, elapsedTime, timedOut);
	}

	private static async Task<string> CompleteReadAsync(Task<string> readTask)
	{
		try
		{
			return await readTask.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
			return string.Empty;
		}
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch (InvalidOperationException)
		{
		}
	}
}
