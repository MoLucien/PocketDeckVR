using System;
using System.IO;
using System.Text.Json;

namespace PocketDeck.SteamVR;

public static class OpenVrControllerPreferences
{
	private sealed record ControllerPreferenceDocument(string ControllerHand);

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	public static OpenVrControllerHand Load()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("VRPSO_CONTROLLER_HAND");
		if (Enum.TryParse<OpenVrControllerHand>(environmentVariable, ignoreCase: true, out var result))
		{
			return result;
		}
		return LoadUnifiedSettings(GetUnifiedSettingsPath()) ?? LoadFromPath(GetLegacyPath());
	}

	internal static OpenVrControllerHand LoadFromPath(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return OpenVrControllerHand.Right;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			string value = jsonDocument.RootElement.GetProperty("controllerHand").GetString();
			OpenVrControllerHand result;
			return Enum.TryParse<OpenVrControllerHand>(value, ignoreCase: true, out result) ? result : OpenVrControllerHand.Right;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException) ? true : false)
		{
			return OpenVrControllerHand.Right;
		}
	}

	public static void Save(OpenVrControllerHand hand)
	{
		SaveToPath(hand, GetLegacyPath());
	}

	internal static void SaveToPath(OpenVrControllerHand hand, string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".new";
		File.WriteAllText(text, JsonSerializer.Serialize(new ControllerPreferenceDocument(hand.ToString()), _jsonOptions));
		File.Move(text, path, overwrite: true);
	}

	private static OpenVrControllerHand? LoadUnifiedSettings(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			string value = jsonDocument.RootElement.GetProperty("controllerHand").GetString();
			OpenVrControllerHand result;
			return Enum.TryParse<OpenVrControllerHand>(value, ignoreCase: true, out result) ? new OpenVrControllerHand?(result) : ((OpenVrControllerHand?)null);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException) ? true : false)
		{
			return null;
		}
	}

	private static string GetUnifiedSettingsPath()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(folderPath, "PocketDeck", "settings.json");
	}

	private static string GetLegacyPath()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(folderPath, "PocketDeck", "input.json");
	}
}
