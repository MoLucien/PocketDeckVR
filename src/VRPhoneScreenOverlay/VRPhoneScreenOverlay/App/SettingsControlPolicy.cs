namespace PocketDeck.App;

internal static class SettingsControlPolicy
{
	public static bool CanUseResolutionSelector(bool generalOperationInProgress, int nativeWidth, int nativeHeight)
	{
		if (!generalOperationInProgress && nativeWidth > 0)
		{
			return nativeHeight > 0;
		}
		return false;
	}

	public static bool CanUsePendingSettingsActions(bool generalOperationInProgress, bool immediateSettingsInProgress, bool settingsDirty)
	{
		return (!generalOperationInProgress && !immediateSettingsInProgress) & settingsDirty;
	}
}
