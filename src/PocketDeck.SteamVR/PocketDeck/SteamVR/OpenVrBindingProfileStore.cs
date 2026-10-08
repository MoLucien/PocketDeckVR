using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PocketDeck.SteamVR;

internal sealed class OpenVrBindingProfileStore(string packagedActionManifestPath, string savedBindingDirectory, string runtimeDirectory, OpenVrControllerHand controllerHand = OpenVrControllerHand.Right)
{
	private const string _mainActionSetPath = "/actions/main";

	private const string _legacyPhoneButtonActionSetPath = "/actions/phonebuttons";

	private const string _pointerActionSetPath = "/actions/pointer";

	private const string _phoneButtonStateActionSetPath = "/actions/phonebuttonstate";

	private const string _phonePointerActionPath = "/actions/pointer/in/phonepointerpose";

	private const string _phoneButtonStateActionPath = "/actions/phonebuttonstate/in/anyphoneinputpressed";

	private static readonly string[] _requiredActionOutputs = new string[3] { "/actions/main/in/phoneoverlaygrab", "/actions/main/in/phoneoverlaytouch", "/actions/main/in/phonepointerpose" };

	private readonly string _packagedActionManifestPath = packagedActionManifestPath;

	private readonly string _savedBindingDirectory = savedBindingDirectory;

	private readonly string _runtimeDirectory = runtimeDirectory;

	private readonly OpenVrControllerHand _controllerHand = controllerHand;

	public static OpenVrBindingProfileStore CreateDefault()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return new OpenVrBindingProfileStore(Path.Combine(AppContext.BaseDirectory, "action_manifest.json"), Path.Combine(folderPath, "steamvr", "input"), Path.Combine(folderPath2, "PocketDeck", "steamvr"), OpenVrControllerPreferences.Load());
	}

	public OpenVrPreparedInputManifest Prepare()
	{
		JsonNode jsonNode = LoadObject(_packagedActionManifestPath);
		JsonArray jsonArray = jsonNode["default_bindings"]?.AsArray() ?? throw new InvalidDataException("SteamVR 动作清单缺少默认绑定列表");
		string baseDirectory = Path.GetDirectoryName(_packagedActionManifestPath) ?? throw new InvalidDataException("SteamVR 动作清单路径无效");
		string text = Path.Combine(_runtimeDirectory, "bindings");
		string text2 = Path.Combine(_runtimeDirectory, "user-bindings");
		Directory.CreateDirectory(text);
		Directory.CreateDirectory(text2);
		List<OpenVrPreparedBindingProfile> list = new List<OpenVrPreparedBindingProfile>();
		foreach (JsonNode item in jsonArray)
		{
			JsonObject jsonObject = item?.AsObject() ?? throw new InvalidDataException("SteamVR 默认绑定项无效");
			string text3 = jsonObject["controller_type"]?.GetValue<string>() ?? throw new InvalidDataException("SteamVR 默认绑定缺少手柄类型");
			EnsureSafeControllerType(text3);
			string bindingUrl = jsonObject["binding_url"]?.GetValue<string>() ?? throw new InvalidDataException("SteamVR 默认绑定缺少文件路径");
			string text4 = ResolvePackagedBinding(baseDirectory, bindingUrl);
			string savedBinding = Path.Combine(_savedBindingDirectory, "local.pocketdeck.desktop.v1_" + text3 + ".json");
			string cachedBinding = Path.Combine(text2, text3 + ".json");
			string text5 = Path.Combine(text, text3 + ".json");
			var (source, source2) = SelectBinding(text3, savedBinding, cachedBinding, text4);
			WriteRuntimeBinding(source, text5, _controllerHand, text4);
			jsonObject["binding_url"] = "bindings/" + text3 + ".json";
			list.Add(new OpenVrPreparedBindingProfile(text3, source2, text5));
		}
		AddSavedControllerProfiles(jsonArray, list, text2, text);
		string text6 = Path.Combine(_runtimeDirectory, "action_manifest.json");
		AtomicWrite(text6, jsonNode.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		return new OpenVrPreparedInputManifest(text6, list);
	}

	public string GetSavedBindingRevision()
	{
		JsonNode jsonNode = LoadObject(_packagedActionManifestPath);
		JsonArray source = jsonNode["default_bindings"]?.AsArray() ?? throw new InvalidDataException("SteamVR 动作清单缺少默认绑定列表");
		using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		foreach (string item in source.Select((JsonNode item) => item?["controller_type"]?.GetValue<string>() ?? throw new InvalidDataException("SteamVR 默认绑定缺少手柄类型")).Order(StringComparer.Ordinal))
		{
			EnsureSafeControllerType(item);
			string path = Path.Combine(_savedBindingDirectory, "local.pocketdeck.desktop.v1_" + item + ".json");
			incrementalHash.AppendData(Encoding.UTF8.GetBytes(item));
			if (File.Exists(path))
			{
				incrementalHash.AppendData(File.ReadAllBytes(path));
			}
		}
		if (Directory.Exists(_savedBindingDirectory))
		{
			foreach (string item2 in Directory.EnumerateFiles(_savedBindingDirectory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
			{
				incrementalHash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(item2)));
				incrementalHash.AppendData(File.ReadAllBytes(item2));
			}
		}
		return Convert.ToHexString(incrementalHash.GetHashAndReset());
	}

	private static (string Path, OpenVrBindingProfileSource Source) SelectBinding(string controllerType, string savedBinding, string cachedBinding, string packagedBinding)
	{
		if (IsValidBinding(savedBinding, controllerType))
		{
			AtomicCopy(savedBinding, cachedBinding);
			return (Path: savedBinding, Source: OpenVrBindingProfileSource.SavedUser);
		}
		if (IsValidBinding(cachedBinding, controllerType))
		{
			return (Path: cachedBinding, Source: OpenVrBindingProfileSource.CachedUser);
		}
		if (!IsValidBinding(packagedBinding, controllerType))
		{
			throw new InvalidDataException("开发默认绑定无效：" + controllerType);
		}
		return (Path: packagedBinding, Source: OpenVrBindingProfileSource.PackagedDefault);
	}

	private static bool IsValidBinding(string path, string controllerType)
	{
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object || !TryGetString(rootElement, "app_key", out string value) || !string.Equals(value, "local.pocketdeck.desktop.v1", StringComparison.Ordinal) || !TryGetString(rootElement, "controller_type", out string value2) || !string.Equals(value2, controllerType, StringComparison.Ordinal))
			{
				return false;
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			CollectOutputs(rootElement, hashSet);
			return _requiredActionOutputs.All(hashSet.Contains) && !ContainsMixedRoleSource(rootElement);
		}
		catch (JsonException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	private static void CollectOutputs(JsonElement element, ISet<string> outputs)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty item in element.EnumerateObject())
			{
				if (string.Equals(item.Name, "output", StringComparison.OrdinalIgnoreCase) && item.Value.ValueKind == JsonValueKind.String)
				{
					string text = item.Value.GetString();
					if (!string.IsNullOrWhiteSpace(text))
					{
						outputs.Add(text);
					}
				}
				CollectOutputs(item.Value, outputs);
			}
			return;
		}
		if (element.ValueKind != JsonValueKind.Array)
		{
			return;
		}
		foreach (JsonElement item2 in element.EnumerateArray())
		{
			CollectOutputs(item2, outputs);
		}
	}

	private static bool ContainsMixedRoleSource(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("path", out var value) && value.ValueKind == JsonValueKind.String)
			{
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				CollectOutputs(element, hashSet);
				if (hashSet.Any(IsPhoneAction) && hashSet.Any(IsPlayspaceAction))
				{
					return true;
				}
			}
			foreach (JsonProperty item in element.EnumerateObject())
			{
				if (ContainsMixedRoleSource(item.Value))
				{
					return true;
				}
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item2 in element.EnumerateArray())
			{
				if (ContainsMixedRoleSource(item2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryGetString(JsonElement element, string propertyName, out string? value)
	{
		value = null;
		if (element.TryGetProperty(propertyName, out var value2) && value2.ValueKind == JsonValueKind.String)
		{
			return (value = value2.GetString()) != null;
		}
		return false;
	}

	private static JsonObject LoadObject(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("SteamVR 动作清单未随程序发布", path);
		}
		return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("SteamVR 动作清单不是有效的 JSON 对象");
	}

	private void AddSavedControllerProfiles(JsonArray defaultBindings, List<OpenVrPreparedBindingProfile> prepared, string cachedBindingsDirectory, string runtimeBindingsDirectory)
	{
		if (!Directory.Exists(_savedBindingDirectory))
		{
			return;
		}
		HashSet<string> hashSet = prepared.Select((OpenVrPreparedBindingProfile profile) => profile.ControllerType).ToHashSet(StringComparer.Ordinal);
		foreach (string item in Directory.EnumerateFiles(_savedBindingDirectory, "*.json"))
		{
			if (TryReadControllerType(item, out string controllerType) && !hashSet.Contains(controllerType) && IsValidBinding(item, controllerType))
			{
				EnsureSafeControllerType(controllerType);
				string destination = Path.Combine(cachedBindingsDirectory, controllerType + ".json");
				string text = Path.Combine(runtimeBindingsDirectory, controllerType + ".json");
				AtomicCopy(item, destination);
				WriteRuntimeBinding(item, text, _controllerHand);
				defaultBindings.Add(new JsonObject
				{
					["controller_type"] = controllerType,
					["binding_url"] = "bindings/" + controllerType + ".json"
				});
				prepared.Add(new OpenVrPreparedBindingProfile(controllerType, OpenVrBindingProfileSource.SavedUser, text));
				hashSet.Add(controllerType);
			}
		}
	}

	private static bool TryReadControllerType(string path, out string controllerType)
	{
		controllerType = string.Empty;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement rootElement = jsonDocument.RootElement;
			string value = null;
			bool flag = TryGetString(rootElement, "app_key", out string value2) && string.Equals(value2, "local.pocketdeck.desktop.v1", StringComparison.Ordinal) && TryGetString(rootElement, "controller_type", out value) && !string.IsNullOrWhiteSpace(value);
			if (flag)
			{
				controllerType = value;
			}
			return flag;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? true : false)
		{
			return false;
		}
	}

	private static void WriteRuntimeBinding(string source, string destination, OpenVrControllerHand controllerHand, string? fallbackPath = null)
	{
		JsonNode jsonNode = JsonNode.Parse(File.ReadAllText(source)) ?? throw new InvalidDataException("SteamVR 手柄绑定不是有效 JSON");
		MigratePhoneButtonActions(jsonNode);
		RewriteHandPaths(jsonNode, controllerHand);
		JsonNode jsonNode2 = ((fallbackPath == null) ? null : JsonNode.Parse(File.ReadAllText(fallbackPath)));
		if (jsonNode2 != null)
		{
			RewriteHandPaths(jsonNode2, controllerHand);
		}
		OpenVrInputCaptureBindings.Add(jsonNode, controllerHand, jsonNode2);
		AtomicWrite(destination, jsonNode.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	private static void MigratePhoneButtonActions(JsonNode binding)
	{
		RewritePhoneButtonOutputPaths(binding);
		if (!(binding["bindings"] is JsonObject jsonObject))
		{
			return;
		}
		jsonObject.Remove("/actions/pointer");
		jsonObject.Remove("/actions/phonebuttonstate");
		if (jsonObject["/actions/phonebuttons"] is JsonObject jsonObject2 && jsonObject2["sources"] is JsonArray jsonArray && jsonObject["/actions/main"] is JsonObject jsonObject3 && jsonObject3["sources"] is JsonArray jsonArray2)
		{
			foreach (JsonNode item in jsonArray)
			{
				if (item != null)
				{
					jsonArray2.Add(item.DeepClone());
				}
			}
		}
		jsonObject.Remove("/actions/phonebuttons");
		WritePointerBinding(jsonObject);
		WritePhoneInputStateBindings(jsonObject);
	}

	private static void WritePointerBinding(JsonObject actionSets)
	{
		JsonArray jsonArray = new JsonArray();
		if (actionSets["/actions/main"] is JsonObject jsonObject && jsonObject["poses"] is JsonArray jsonArray2)
		{
			foreach (JsonNode item in jsonArray2)
			{
				if (item is JsonObject jsonObject2 && jsonObject2["output"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && string.Equals(value, "/actions/main/in/phonepointerpose", StringComparison.OrdinalIgnoreCase))
				{
					JsonObject jsonObject3 = (JsonObject)jsonObject2.DeepClone();
					jsonObject3["output"] = "/actions/pointer/in/phonepointerpose";
					jsonArray.Add(jsonObject3);
				}
			}
		}
		JsonArray jsonArray3 = new JsonArray();
		JsonArray jsonArray4 = new JsonArray();
		if (actionSets["/actions/main"]?["sources"] is JsonArray jsonArray5)
		{
			foreach (JsonNode item2 in jsonArray5)
			{
				if (!(item2?["inputs"] is JsonObject jsonObject4))
				{
					continue;
				}
				JsonObject jsonObject5 = new JsonObject();
				JsonObject jsonObject6 = new JsonObject();
				foreach (var (propertyName, jsonNode2) in jsonObject4)
				{
					if (string.Equals(jsonNode2?["output"]?.GetValue<string>(), "/actions/main/in/phoneoverlaytouch", StringComparison.OrdinalIgnoreCase))
					{
						jsonObject5[propertyName] = new JsonObject { ["output"] = "/actions/pointer/in/menudismiss" };
					}
					else if (string.Equals(jsonNode2?["output"]?.GetValue<string>(), "/actions/main/in/phonescreenshot", StringComparison.OrdinalIgnoreCase))
					{
						jsonObject6[propertyName] = new JsonObject { ["output"] = "/actions/phonerecall/in/recallmenu" };
					}
				}
				if (jsonObject6.Count > 0)
				{
					JsonNode jsonNode3 = item2.DeepClone();
					jsonNode3["inputs"] = jsonObject6;
					jsonArray4.Add(jsonNode3);
				}
				if (jsonObject5.Count != 0)
				{
					JsonNode jsonNode4 = item2.DeepClone();
					jsonNode4["inputs"] = jsonObject5;
					jsonArray3.Add(jsonNode4);
				}
			}
		}
		actionSets["/actions/pointer"] = new JsonObject
		{
			["poses"] = jsonArray,
			["sources"] = jsonArray3
		};
		actionSets["/actions/phonerecall"] = new JsonObject { ["sources"] = jsonArray4 };
	}

	private static void WritePhoneInputStateBindings(JsonObject actionSets)
	{
		JsonArray jsonArray = new JsonArray();
		if (actionSets["/actions/main"] is JsonObject jsonObject && jsonObject["sources"] is JsonArray jsonArray2)
		{
			foreach (JsonNode item in jsonArray2)
			{
				if (!(item is JsonObject jsonObject2) || jsonObject2["path"] == null)
				{
					continue;
				}
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				CollectOutputPaths(jsonObject2["inputs"], hashSet);
				if (hashSet.Any(IsGatedPhoneInputAction))
				{
					JsonObject jsonObject3 = (JsonObject)jsonObject2.DeepClone();
					jsonObject3["inputs"] = new JsonObject { ["click"] = new JsonObject { ["output"] = "/actions/phonebuttonstate/in/anyphoneinputpressed" } };
					if (jsonObject3["parameters"] is JsonObject jsonObject4)
					{
						jsonObject4.Remove("double_press_delay");
						jsonObject4.Remove("long_press_delay");
						jsonObject4.Remove("long_press_expiry");
					}
					jsonArray.Add(jsonObject3);
				}
			}
		}
		actionSets["/actions/phonebuttonstate"] = new JsonObject { ["sources"] = jsonArray };
	}

	private static void RewritePhoneButtonOutputPaths(JsonNode? node)
	{
		if (node is JsonObject jsonObject)
		{
			KeyValuePair<string, JsonNode>[] array = jsonObject.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				var (text2, jsonNode2) = array[i];
				if (string.Equals(text2, "output", StringComparison.OrdinalIgnoreCase) && jsonNode2 is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && TryMapPhoneButtonAction(value, out string mapped))
				{
					jsonObject[text2] = mapped;
				}
				else
				{
					RewritePhoneButtonOutputPaths(jsonNode2);
				}
			}
		}
		else
		{
			if (!(node is JsonArray jsonArray))
			{
				return;
			}
			foreach (JsonNode item in jsonArray)
			{
				RewritePhoneButtonOutputPaths(item);
			}
		}
	}

	private static bool TryMapPhoneButtonAction(string? output, out string mapped)
	{
		if (output != null && output.StartsWith("/actions/phonebuttons/in/phone", StringComparison.OrdinalIgnoreCase))
		{
			string text = output.Substring("/actions/phonebuttons/in/phone".Length).ToLowerInvariant();
			bool flag;
			switch (text)
			{
			case "back":
			case "home":
			case "recents":
			case "controlpanel":
			case "screenshot":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				mapped = "/actions/main/in/phone" + text;
				return true;
			}
		}
		mapped = string.Empty;
		return false;
	}

	private static void RewriteHandPaths(JsonNode? node, OpenVrControllerHand controllerHand)
	{
		if (node is JsonObject jsonObject)
		{
			if (jsonObject["path"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && value != null)
			{
				HashSet<string> outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				CollectOutputPaths(jsonObject, outputs);
				OpenVrControllerHand? openVrControllerHand = ResolveTargetHand(outputs, controllerHand);
				if (openVrControllerHand.HasValue)
				{
					jsonObject["path"] = MapHandPath(value, openVrControllerHand.Value);
				}
			}
			KeyValuePair<string, JsonNode>[] array = jsonObject.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				var (_, node2) = array[i];
				RewriteHandPaths(node2, controllerHand);
			}
		}
		else
		{
			if (!(node is JsonArray jsonArray))
			{
				return;
			}
			foreach (JsonNode item in jsonArray)
			{
				RewriteHandPaths(item, controllerHand);
			}
		}
	}

	private static void CollectOutputPaths(JsonNode? node, ISet<string> outputs)
	{
		if (node is JsonObject jsonObject)
		{
			{
				foreach (var (a, jsonNode2) in jsonObject)
				{
					if (string.Equals(a, "output", StringComparison.OrdinalIgnoreCase) && jsonNode2 is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
					{
						outputs.Add(value);
					}
					CollectOutputPaths(jsonNode2, outputs);
				}
				return;
			}
		}
		if (!(node is JsonArray jsonArray))
		{
			return;
		}
		foreach (JsonNode item in jsonArray)
		{
			CollectOutputPaths(item, outputs);
		}
	}

	private static OpenVrControllerHand? ResolveTargetHand(HashSet<string> outputs, OpenVrControllerHand controllerHand)
	{
		bool flag = outputs.Any(IsPhoneAction);
		bool flag2 = outputs.Any(IsPlayspaceAction);
		if (flag == flag2)
		{
			return null;
		}
		return flag ? controllerHand : OpenVrControllerHandRouting.Opposite(controllerHand);
	}

	private static bool IsPhoneAction(string output)
	{
		if (!output.StartsWith("/actions/main/in/phone", StringComparison.OrdinalIgnoreCase) && !output.StartsWith("/actions/phonebuttons/in/phone", StringComparison.OrdinalIgnoreCase) && !output.StartsWith("/actions/phonebuttonstate/in/", StringComparison.OrdinalIgnoreCase) && !output.StartsWith("/actions/phonerecall/in/", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/pointer/in/menudismiss", StringComparison.OrdinalIgnoreCase))
		{
			return output.StartsWith("/actions/pointer/in/phone", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsGatedPhoneInputAction(string output)
	{
		if (!output.Equals("/actions/main/in/phoneback", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/main/in/phonehome", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/main/in/phonerecents", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/main/in/phonecontrolpanel", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/main/in/phonescreenshot", StringComparison.OrdinalIgnoreCase) && !output.Equals("/actions/main/in/phoneoverlaytouch", StringComparison.OrdinalIgnoreCase))
		{
			return output.Equals("/actions/main/in/phoneoverlaygrab", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsPlayspaceAction(string output)
	{
		if (!output.EndsWith("spacedrag", StringComparison.OrdinalIgnoreCase))
		{
			return output.Equals("/actions/main/in/resetoffsets", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string MapHandPath(string path, OpenVrControllerHand targetHand)
	{
		if (!path.StartsWith("/user/hand/", StringComparison.Ordinal))
		{
			return path;
		}
		int num = path.IndexOf('/', "/user/hand/".Length);
		if (num < 0)
		{
			return path;
		}
		int length = "/user/hand/".Length;
		string text = path.Substring(length, num - length);
		string text2 = ((targetHand == OpenVrControllerHand.Left) ? "left" : "right");
		if (string.Equals(text, text2, StringComparison.Ordinal))
		{
			return path;
		}
		if (!(text == "left") && !(text == "right"))
		{
			return path;
		}
		string text3 = path.Substring(num);
		string text4;
		if (!(text == "right"))
		{
			if (!(text == "left") || !(text2 == "right"))
			{
				goto IL_0115;
			}
			if (!(text3 == "/input/x"))
			{
				if (!(text3 == "/input/y"))
				{
					goto IL_0115;
				}
				text4 = "/input/b";
			}
			else
			{
				text4 = "/input/a";
			}
		}
		else
		{
			if (!(text2 == "left"))
			{
				goto IL_0115;
			}
			if (!(text3 == "/input/a"))
			{
				if (!(text3 == "/input/b"))
				{
					goto IL_0115;
				}
				text4 = "/input/y";
			}
			else
			{
				text4 = "/input/x";
			}
		}
		goto IL_0118;
		IL_0115:
		text4 = text3;
		goto IL_0118;
		IL_0118:
		text3 = text4;
		return "/user/hand/" + text2 + text3;
	}

	private static string ResolvePackagedBinding(string baseDirectory, string bindingUrl)
	{
		string path = bindingUrl.Replace('/', Path.DirectorySeparatorChar);
		string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, path));
		string value = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("SteamVR 默认绑定路径越出程序目录");
		}
		return fullPath;
	}

	private static void EnsureSafeControllerType(string controllerType)
	{
		if (controllerType.Length == 0 || controllerType.Any((char character) => !char.IsAsciiLetterOrDigit(character) && character != '_' && character != '-'))
		{
			throw new InvalidDataException("SteamVR 手柄类型包含无效字符");
		}
	}

	private static void AtomicCopy(string source, string destination)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		string text = destination + ".new";
		File.Copy(source, text, overwrite: true);
		File.Move(text, destination, overwrite: true);
	}

	private static void AtomicWrite(string destination, string content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		string text = destination + ".new";
		File.WriteAllText(text, content);
		File.Move(text, destination, overwrite: true);
	}
}
