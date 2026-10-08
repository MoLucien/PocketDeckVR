using System.Text.Json.Serialization;

namespace PocketDeck.SteamVR;

internal sealed record OpenVrSavedPlacement(float Scale, int SchemaVersion = 2)
{
	[JsonIgnore]
	public bool IsValid
	{
		get
		{
			if (SchemaVersion == 2 && float.IsFinite(Scale))
			{
				float scale = Scale;
				if (scale >= 0.2f)
				{
					return scale <= 2.5f;
				}
				return false;
			}
			return false;
		}
	}
}
