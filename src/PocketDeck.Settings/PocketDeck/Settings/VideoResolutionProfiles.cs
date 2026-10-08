using System;
using System.Collections.Generic;
using System.Linq;

namespace PocketDeck.Settings;

public static class VideoResolutionProfiles
{
	private const int _minimumShortEdge = 720;

	private static readonly int[] _standardPercentages = new int[4] { 100, 90, 80, 70 };

	public static IReadOnlyList<VideoResolutionProfile> Create(int nativeWidth, int nativeHeight)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(nativeWidth, 1, "nativeWidth");
		ArgumentOutOfRangeException.ThrowIfLessThan(nativeHeight, 1, "nativeHeight");
		int num = Math.Min(nativeWidth, nativeHeight);
		if (num < 720)
		{
			return new _003C_003Ez__ReadOnlySingleElementList<VideoResolutionProfile>(CreateProfile(nativeWidth, nativeHeight, 100, approximate: false));
		}
		List<VideoResolutionProfile> list = new List<VideoResolutionProfile>();
		int[] standardPercentages = _standardPercentages;
		foreach (int num2 in standardPercentages)
		{
			if (num2 == 100 || Scale(num, num2) >= 720)
			{
				list.Add(CreateProfile(nativeWidth, nativeHeight, num2, approximate: false));
			}
		}
		int minimumPercent = Math.Clamp(checked((int)Math.Ceiling(72000.0 / (double)num)), 1, 100);
		if (list.All((VideoResolutionProfile profile) => profile.Percent != minimumPercent))
		{
			list.Add(CreateProfile(nativeWidth, nativeHeight, minimumPercent, approximate: true));
		}
		return list.OrderByDescending((VideoResolutionProfile profile) => profile.Percent).ToArray();
	}

	public static VideoResolutionProfile Resolve(int nativeWidth, int nativeHeight, int requestedPercent)
	{
		IReadOnlyList<VideoResolutionProfile> source = Create(nativeWidth, nativeHeight);
		return (from profile in source
			orderby Math.Abs(checked(profile.Percent - requestedPercent)), profile.Percent descending
			select profile).First();
	}

	private static VideoResolutionProfile CreateProfile(int nativeWidth, int nativeHeight, int percent, bool approximate)
	{
		double num = (approximate ? (720.0 / (double)Math.Min(nativeWidth, nativeHeight)) : ((double)percent / 100.0));
		checked
		{
			int num2 = Math.Max(1, (int)Math.Round((double)nativeWidth * num, MidpointRounding.AwayFromZero));
			int num3 = Math.Max(1, (int)Math.Round((double)nativeHeight * num, MidpointRounding.AwayFromZero));
			return new VideoResolutionProfile(percent, num2, num3, (percent != 100) ? Math.Max(num2, num3) : 0, approximate);
		}
	}

	private static int Scale(int value, int percent)
	{
		return Math.Max(1, checked((int)Math.Round((double)(value * percent) / 100.0, MidpointRounding.AwayFromZero)));
	}
}
