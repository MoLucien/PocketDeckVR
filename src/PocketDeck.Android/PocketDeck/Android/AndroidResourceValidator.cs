using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal static class AndroidResourceValidator
{
	private static readonly IReadOnlyDictionary<string, string> _expectedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["adb.exe"] = "957e46b8615f7af5b7292a2ddabe98d2e61940c3fb2b0545756507f080613e71",
		["AdbWinApi.dll"] = "120bef587119c6cb926b86b9be90fdfbce38937588eae28cd91a94ce63c7b965",
		["AdbWinUsbApi.dll"] = "6ca69a2ca0e31309c087d288f058977d421ad03500e4c3e1dbd981241a069c60"
	};

	public static async ValueTask<AndroidResourceValidationResult> ValidateAsync(string resourceDirectory, CancellationToken cancellationToken)
	{
		string fullDirectory = Path.GetFullPath(resourceDirectory);
		foreach (var (fileName, expectedHash) in _expectedHashes)
		{
			string path = Path.Combine(fullDirectory, fileName);
			if (!File.Exists(path))
			{
				return new AndroidResourceValidationResult(Succeeded: false, "ANDROID_RESOURCE_MISSING", "缺少手机连接组件：" + fileName, Path.Combine(fullDirectory, "adb.exe"));
			}
			await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, useAsync: true))
			{
				string a = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
				if (!string.Equals(a, expectedHash, StringComparison.Ordinal))
				{
					return new AndroidResourceValidationResult(Succeeded: false, "ANDROID_RESOURCE_HASH_MISMATCH", "手机连接组件校验失败：" + fileName, Path.Combine(fullDirectory, "adb.exe"));
				}
			}
			AndroidResourceValidationResult androidResourceValidationResult = null;
		}
		return new AndroidResourceValidationResult(Succeeded: true, "ANDROID_RESOURCES_READY", "内置手机连接组件校验通过", Path.Combine(fullDirectory, "adb.exe"));
	}
}
