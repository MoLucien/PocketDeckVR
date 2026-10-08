using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal static class ScrcpyServerResourceValidator
{
	public static async ValueTask<string> ValidateAsync(string serverPath, CancellationToken cancellationToken)
	{
		string fullPath = Path.GetFullPath(serverPath);
		if (!File.Exists(fullPath))
		{
			throw new AndroidConnectionException("ANDROID_SCRCPY_SERVER_MISSING", "缺少 scrcpy 手机服务组件");
		}
		string directoryName = Path.GetDirectoryName(fullPath);
		string text = Path.Combine(directoryName ?? throw new AndroidConnectionException("ANDROID_SCRCPY_MANIFEST_MISSING", "缺少 scrcpy 手机服务清单"), "manifest.json");
		if (!File.Exists(text))
		{
			throw new AndroidConnectionException("ANDROID_SCRCPY_MANIFEST_MISSING", "缺少 scrcpy 手机服务清单");
		}
		string expectedHash = await ReadExpectedHashAsync(text, Path.GetFileName(fullPath), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string result;
		await using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, useAsync: true))
		{
			if (!string.Equals(Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)), expectedHash, StringComparison.Ordinal))
			{
				throw new AndroidConnectionException("ANDROID_SCRCPY_SERVER_HASH_MISMATCH", "scrcpy 手机服务组件校验失败");
			}
			result = fullPath;
		}
		return result;
	}

	private static async ValueTask<string> ReadExpectedHashAsync(string manifestPath, string serverFileName, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			string result;
			await using (FileStream stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true))
			{
				using JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!jsonDocument.RootElement.TryGetProperty("files", out var value) || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(serverFileName, out var value2) || value2.ValueKind != JsonValueKind.String)
				{
					throw new JsonException("The scrcpy server hash is missing from the manifest.");
				}
				string text = value2.GetString();
				if (text == null || text.Length != 64 || !text.All(Uri.IsHexDigit))
				{
					throw new JsonException("The scrcpy server hash in the manifest is invalid.");
				}
				result = text.ToLowerInvariant();
			}
			return result;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? true : false)
		{
			throw new AndroidConnectionException("ANDROID_SCRCPY_MANIFEST_INVALID", "scrcpy 手机服务清单无效");
		}
	}
}
