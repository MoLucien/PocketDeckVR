using System.Globalization;

namespace PocketDeck.Settings;

public sealed record VideoResolutionProfile(int Percent, int ExpectedWidth, int ExpectedHeight, int MaximumSize, bool IsApproximate)
{
	public string DisplayName
	{
		get
		{
			if (Percent == 100)
			{
				return $"原生 100%（{ExpectedWidth}×{ExpectedHeight}）";
			}
			return $"{(IsApproximate ? "约" : string.Empty)}{Percent.ToString(CultureInfo.InvariantCulture)}%（{ExpectedWidth}×{ExpectedHeight}）";
		}
	}
}
