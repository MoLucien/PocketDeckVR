using System.CodeDom.Compiler;
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.37416")]
internal sealed class _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__PropertyPattern_0 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				while (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan) && runtextpos != inputSpan.Length)
				{
					runtextpos++;
					if (_003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 3)
				{
					int num2 = inputSpan.Slice(num).IndexOfAny(_003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_asciiLettersAndDigitsAndDashDotUnderscore);
					if (num2 >= 0)
					{
						runtextpos = num + num2;
						return true;
					}
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int num3 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				num2 = num;
				int num4 = span.IndexOfAnyExcept(_003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_asciiLettersAndDigitsAndDashDotUnderscore);
				if (num4 < 0)
				{
					num4 = span.Length;
				}
				if (num4 == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(num4);
				num += num4;
				Capture(1, num2, num);
				if (span.IsEmpty || span[0] != ':')
				{
					UncaptureUntil(0);
					return false;
				}
				num++;
				span = inputSpan.Slice(num);
				num3 = num;
				int i;
				for (i = 0; (uint)i < (uint)span.Length && !char.IsWhiteSpace(span[i]); i++)
				{
				}
				if (i == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(i);
				num += i;
				Capture(2, num3, num);
				runtextpos = num;
				Capture(0, start, num);
				return true;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int capturePosition)
				{
					while (Crawlpos() > capturePosition)
					{
						Uncapture();
					}
				}
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__PropertyPattern_0 Instance = new _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__PropertyPattern_0();

	private _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__PropertyPattern_0()
	{
		pattern = "(?<name>[A-Za-z0-9_.-]+):(?<value>[^\\s]+)";
		roptions = RegexOptions.CultureInvariant;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		CapNames = new Hashtable
		{
			{ "0", 0 },
			{ "name", 1 },
			{ "value", 2 }
		};
		capslist = new string[3] { "0", "name", "value" };
		capsize = 3;
	}
}
