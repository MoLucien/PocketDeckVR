using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class BundledAdbServerShutdown(string resourceDirectory, TimeSpan commandTimeout) : IAdbServerShutdown
{
	private readonly string _adbExecutable = Path.GetFullPath(Path.Combine(resourceDirectory, "adb.exe"));

	private readonly TimeSpan _commandTimeout = commandTimeout;

	public async ValueTask<AdbServerShutdownResult> StopAsync(CancellationToken cancellationToken)
	{
		bool flag = true;
		if (File.Exists(_adbExecutable))
		{
			try
			{
				flag = (await new AdbCommandRunner(_adbExecutable).RunAsync(new _003C_003Ez__ReadOnlySingleElementList<string>("kill-server"), _commandTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Succeeded;
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is Win32Exception) ? true : false)
			{
				flag = false;
			}
		}
		int value = StopRemainingBundledProcesses();
		if (FindBundledProcesses().Count != 0)
		{
			return new AdbServerShutdownResult(Succeeded: false, "ANDROID_ADB_SERVER_STOP_FAILED", "内置 ADB 后台服务未能完全退出");
		}
		string message = (flag ? "内置 ADB 后台服务已退出" : $"内置 ADB 后台服务已强制退出（处理 {value} 个残留进程）");
		return new AdbServerShutdownResult(Succeeded: true, "ANDROID_ADB_SERVER_STOPPED", message);
	}

	private int StopRemainingBundledProcesses()
	{
		int num = 0;
		foreach (Process item in FindBundledProcesses())
		{
			using (item)
			{
				try
				{
					if (!item.HasExited)
					{
						item.Kill(entireProcessTree: true);
						if (item.WaitForExit(5000))
						{
							num = checked(num + 1);
						}
					}
				}
				catch (Exception ex) when ((ex is InvalidOperationException || ex is Win32Exception || ex is NotSupportedException) ? true : false)
				{
				}
			}
		}
		return num;
	}

	private List<Process> FindBundledProcesses()
	{
		List<Process> list = new List<Process>();
		Process[] processesByName = Process.GetProcessesByName("adb");
		foreach (Process process in processesByName)
		{
			try
			{
				if (string.Equals(process.MainModule?.FileName, _adbExecutable, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(process);
					continue;
				}
			}
			catch (Exception ex) when ((ex is InvalidOperationException || ex is Win32Exception || ex is NotSupportedException) ? true : false)
			{
			}
			process.Dispose();
		}
		return list;
	}
}
