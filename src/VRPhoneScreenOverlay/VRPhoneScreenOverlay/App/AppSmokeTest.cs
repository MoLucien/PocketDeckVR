using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PocketDeck.App;

internal static class AppSmokeTest
{
	public static int Run()
	{
		try
		{
			string baseDirectory = AppContext.BaseDirectory;
			string[] array = new string[4] { "action_manifest.json", "manifest.vrmanifest", "openvr_api.dll", "PocketDeck.SteamVR.BindingTool.exe" };
			string[] array2 = array;
			foreach (string path in array2)
			{
				if (!File.Exists(Path.Combine(baseDirectory, path)))
				{
					return 10;
				}
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseDirectory, "manifest.vrmanifest")));
			string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, jsonDocument.RootElement.GetProperty("applications").EnumerateArray().Single((JsonElement item) => item.GetProperty("app_key").GetString() == "local.pocketdeck.desktop.v1")
				.GetProperty("binary_path_windows")
				.GetString()));
			if (!File.Exists(fullPath) || !string.Equals(fullPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
			{
				return 14;
			}
			using JsonDocument jsonDocument2 = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseDirectory, "action_manifest.json")));
			if (jsonDocument2.RootElement.GetProperty("actions").GetArrayLength() == 0 || jsonDocument2.RootElement.GetProperty("default_bindings").GetArrayLength() == 0)
			{
				return 11;
			}
			return 0;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException) ? true : false)
		{
			return 12;
		}
	}
}
