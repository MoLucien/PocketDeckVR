using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrRuntimeHost
{
	private static readonly object _gate = new object();

	private static readonly OpenVrActionUpdateCoordinator _actionUpdates = new OpenVrActionUpdateCoordinator();

	private static CVRSystem? _system;

	private static bool _actionManifestSubmitted;

	internal static object ApiGate => _gate;

	internal static OpenVrActionUpdateCoordinator ActionUpdates => _actionUpdates;

	public static CVRSystem GetOrStart(ref EVRInitError initializationError)
	{
		lock (_gate)
		{
			if (_system != null)
			{
				initializationError = EVRInitError.None;
				return _system;
			}
			CVRSystem cVRSystem = OpenVR.Init(ref initializationError, EVRApplicationType.VRApplication_Overlay);
			if (initializationError == EVRInitError.None)
			{
				_system = cVRSystem;
			}
			return cVRSystem;
		}
	}

	public static EVRInputError EnsureActionManifestSubmitted()
	{
		lock (_gate)
		{
			if (_actionManifestSubmitted)
			{
				return EVRInputError.None;
			}
			EVRInputError eVRInputError = OpenVrInputManifest.RegisterAndSubmit();
			if ((eVRInputError == EVRInputError.None || eVRInputError == EVRInputError.IPCError) ? true : false)
			{
				_actionManifestSubmitted = true;
			}
			return eVRInputError;
		}
	}

	public static void Shutdown()
	{
		lock (_gate)
		{
			if (_system != null)
			{
				OpenVR.Shutdown();
				_system = null;
				_actionManifestSubmitted = false;
			}
		}
	}
}
