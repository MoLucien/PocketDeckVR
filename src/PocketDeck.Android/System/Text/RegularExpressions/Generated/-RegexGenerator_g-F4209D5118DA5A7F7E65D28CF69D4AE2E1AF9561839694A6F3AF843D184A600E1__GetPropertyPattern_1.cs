using System.CodeDom.Compiler;
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.37416")]
internal sealed class _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__GetPropertyPattern_1 : Regex
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
				if (num <= inputSpan.Length - 7)
				{
					if (num <= 0 || inputSpan[num - 1] == '\n')
					{
						goto IL_0058;
					}
					int num2 = inputSpan.Slice(num).IndexOf('\n');
					if ((uint)num2 <= inputSpan.Length - num - 1)
					{
						num += num2 + 1;
						if (num <= inputSpan.Length - 7)
						{
							goto IL_0058;
						}
					}
				}
				goto IL_0077;
				IL_0058:
				int num3 = inputSpan.Slice(num).IndexOf('[');
				if (num3 >= 0)
				{
					runtextpos = num + num3;
					return true;
				}
				goto IL_0077;
				IL_0077:
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if (num > 0 && inputSpan[num - 1] != '\n')
				{
					UncaptureUntil(0);
					return false;
				}
				if (span.IsEmpty || span[0] != '[')
				{
					UncaptureUntil(0);
					return false;
				}
				num++;
				span = inputSpan.Slice(num);
				num2 = num;
				int num7 = span.IndexOf(']');
				if (num7 < 0)
				{
					num7 = span.Length;
				}
				if (num7 == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(num7);
				num += num7;
				Capture(1, num2, num);
				if (!span.StartsWith("]: ["))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 4;
				span = inputSpan.Slice(num);
				num3 = num;
				num5 = num;
				int num8 = span.IndexOf('\n');
				if (num8 < 0)
				{
					num8 = span.Length;
				}
				span = span.Slice(num8);
				num += num8;
				num6 = num;
				while (true)
				{
					num4 = Crawlpos();
					Capture(2, num3, num);
					if (!span.IsEmpty && span[0] == ']' && (1 >= span.Length || span[1] == '\n'))
					{
						break;
					}
					UncaptureUntil(num4);
					if (_003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num5 >= num6 || (num6 = inputSpan.Slice(num5, num6 - num5).LastIndexOf(']')) < 0)
					{
						UncaptureUntil(0);
						return false;
					}
					num6 += num5;
					num = num6;
					span = inputSpan.Slice(num);
				}
				Capture(0, start, runtextpos = num + 1);
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

	internal static readonly _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__GetPropertyPattern_1 Instance = new _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__GetPropertyPattern_1();

	private _003CRegexGenerator_g_003EF4209D5118DA5A7F7E65D28CF69D4AE2E1AF9561839694A6F3AF843D184A600E1__GetPropertyPattern_1()
	{
		pattern = "^\\[(?<name>[^\\]]+)\\]: \\[(?<value>.*)\\]$";
		roptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;
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
