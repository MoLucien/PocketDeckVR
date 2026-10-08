using System;

namespace PocketDeck.Android;

internal sealed record AdbCommandResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration, bool TimedOut)
{
	public bool Succeeded
	{
		get
		{
			if (!TimedOut)
			{
				return ExitCode == 0;
			}
			return false;
		}
	}
}
