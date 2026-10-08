using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrInputCaptureProfiles
{
	public static async Task EnrichAsync(CancellationToken cancellationToken)
	{
		CVRSystem system = OpenVR.System;
		OpenVrControllerHand hand = OpenVrControllerPreferences.Load();
		uint trackedDeviceIndexForControllerRole = system.GetTrackedDeviceIndexForControllerRole((hand == OpenVrControllerHand.Left) ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand);
		if (trackedDeviceIndexForControllerRole == uint.MaxValue)
		{
			return;
		}
		string text = ReadProperty(system, trackedDeviceIndexForControllerRole, ETrackedDeviceProperty.Prop_ControllerType_String);
		string text2 = ReadProperty(system, trackedDeviceIndexForControllerRole, ETrackedDeviceProperty.Prop_InputProfilePath_String);
		if (string.IsNullOrEmpty(text) || text.Length > 128 || text.Any((char c) => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-') || string.IsNullOrEmpty(text2))
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder(4096);
		EVRInitError peError = EVRInitError.None;
		nint genericInterface = OpenVR.GetGenericInterface("FnTable:IVRResources_001", ref peError);
		if (peError != EVRInitError.None || genericInterface == 0)
		{
			return;
		}
		CVRResources cVRResources = new CVRResources(genericInterface);
		uint resourceFullPath = cVRResources.GetResourceFullPath(text2, string.Empty, stringBuilder, checked((uint)stringBuilder.Capacity));
		if (resourceFullPath == 0 || resourceFullPath > stringBuilder.Capacity)
		{
			return;
		}
		InlineArray5<string> buffer = default;
		buffer[0] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		buffer[1] = "PocketDeck";
		buffer[2] = "steamvr";
		buffer[3] = "bindings";
		buffer[4] = text + ".json";
		string runtimePath = Path.Combine(buffer);
		string temporary = runtimePath + ".capture-new";
		try
		{
			string text3 = stringBuilder.ToString();
			if (File.Exists(runtimePath) && File.Exists(text3) && new FileInfo(text3).Length <= 1048576 && new FileInfo(runtimePath).Length <= 1048576)
			{
				JsonNode profile = JsonNode.Parse(await File.ReadAllTextAsync(text3, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
				JsonNode jsonNode = JsonNode.Parse(await File.ReadAllTextAsync(runtimePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
				if (jsonNode != null && profile != null)
				{
					OpenVrInputCaptureBindings.Add(jsonNode, hand, null, profile);
					await File.WriteAllTextAsync(temporary, jsonNode.ToJsonString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					cancellationToken.ThrowIfCancellationRequested();
					File.Move(temporary, runtimePath, overwrite: true);
				}
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException || ex is OperationCanceledException) ? true : false)
		{
		}
		finally
		{
			try
			{
				File.Delete(temporary);
			}
			catch (Exception ex2) when ((ex2 is IOException || ex2 is UnauthorizedAccessException) ? true : false)
			{
			}
		}
	}

	private static string ReadProperty(CVRSystem system, uint device, ETrackedDeviceProperty property)
	{
		ETrackedPropertyError pError = ETrackedPropertyError.TrackedProp_Success;
		StringBuilder stringBuilder = new StringBuilder(4096);
		system.GetStringTrackedDeviceProperty(device, property, stringBuilder, checked((uint)stringBuilder.Capacity), ref pError);
		if (pError != ETrackedPropertyError.TrackedProp_Success)
		{
			return string.Empty;
		}
		return stringBuilder.ToString();
	}
}
