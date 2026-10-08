using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PocketDeck.SteamVR;

internal static class OpenVrInputCaptureBindings
{
	public const string ActionSet = "/actions/phonecapture";

	private const string _button = "/actions/phonecapture/in/button";

	private const string _scalar = "/actions/phonecapture/in/scalar";

	private const string _axis = "/actions/phonecapture/in/axis";

	public static void Add(JsonNode binding, OpenVrControllerHand hand, JsonNode? fallback = null, JsonNode? profile = null)
	{
		string text = ((hand == OpenVrControllerHand.Left) ? "left" : "right");
		string text2 = "/user/hand/" + text;
		JsonArray jsonArray = new JsonArray();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (profile?["input_source"] is JsonObject jsonObject)
		{
			foreach (KeyValuePair<string, JsonNode> item in jsonObject)
			{
				item.Deconstruct(out var key, out var value);
				string text3 = key;
				JsonNode jsonNode = value;
				string text4 = jsonNode?["side"]?.GetValue<string>();
				string text5 = jsonNode?["type"]?.GetValue<string>();
				bool flag = !text3.StartsWith("/input/", StringComparison.Ordinal) || text3.Contains("..", StringComparison.Ordinal) || (text4 != null && text4 != text);
				bool flag2 = flag;
				if (!flag2)
				{
					bool flag3;
					switch (text5)
					{
					case "button":
					case "trigger":
					case "joystick":
					case "trackpad":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag2 = !flag3;
				}
				if (!flag2)
				{
					bool flag3 = ((text5 == "joystick" || text5 == "trackpad") ? true : false);
					string text6;
					if (flag3)
					{
						text6 = "position";
					}
					else
					{
						text6 = ((text5 == "trigger") ? "pull" : "click");
					}
					JsonObject jsonObject2 = new JsonObject
					{
						["path"] = text2 + text3,
						["mode"] = text5
					};
					JsonObject jsonObject3 = new JsonObject();
					key = text6;
					jsonObject3[key] = new JsonObject { ["output"] = OutputForSlot(text6) };
					jsonObject2["inputs"] = jsonObject3;
					jsonArray.Add(jsonObject2);
					hashSet.Add(text2 + text3);
				}
			}
		}
		AddExistingSources(binding, text2, jsonArray, hashSet);
		if (fallback != null)
		{
			AddExistingSources(fallback, text2, jsonArray, hashSet);
		}
		binding["bindings"]["/actions/phonecapture"] = new JsonObject { ["sources"] = jsonArray };
	}

	private static void AddExistingSources(JsonNode binding, string prefix, JsonArray sources, HashSet<string> paths)
	{
		if (!(binding["bindings"] is JsonObject jsonObject))
		{
			return;
		}
		foreach (KeyValuePair<string, JsonNode> item in jsonObject)
		{
			item.Deconstruct(out var key, out var value);
			if (!(value?["sources"] is JsonArray jsonArray))
			{
				continue;
			}
			foreach (JsonNode item2 in jsonArray)
			{
				string text = item2?["path"]?.GetValue<string>();
				if (text == null || !text.StartsWith(prefix + "/input/", StringComparison.Ordinal) || !(item2?["inputs"] is JsonObject jsonObject2) || !paths.Add(text))
				{
					continue;
				}
				JsonObject jsonObject3 = (JsonObject)item2.DeepClone();
				JsonObject jsonObject4 = new JsonObject();
				foreach (KeyValuePair<string, JsonNode> item3 in jsonObject2)
				{
					item3.Deconstruct(out key, out value);
					string text2 = key;
					if (value?["output"] != null)
					{
						jsonObject4[text2] = new JsonObject { ["output"] = OutputForSlot(text2) };
					}
				}
				if (jsonObject4.Count > 0)
				{
					jsonObject3["inputs"] = jsonObject4;
					jsonObject3.Remove("parameters");
					sources.Add(jsonObject3);
				}
			}
		}
	}

	private static string OutputForSlot(string slot)
	{
		switch (slot)
		{
		case "position":
			return "/actions/phonecapture/in/axis";
		case "pull":
		case "value":
		case "force":
			return "/actions/phonecapture/in/scalar";
		default:
			return "/actions/phonecapture/in/button";
		}
	}
}
