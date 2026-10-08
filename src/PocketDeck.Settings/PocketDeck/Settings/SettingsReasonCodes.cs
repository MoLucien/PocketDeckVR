namespace PocketDeck.Settings;

public static class SettingsReasonCodes
{
	public const string BatchApplyRolledBack = "SETTINGS_BATCH_APPLY_ROLLED_BACK";

	public const string BatchRollbackFailed = "SETTINGS_BATCH_ROLLBACK_FAILED";

	public const string CorruptDefaulted = "SETTINGS_CORRUPT_DEFAULTED";

	public const string DefaultCreated = "SETTINGS_DEFAULT_CREATED";

	public const string InputMigrated = "SETTINGS_INPUT_MIGRATED";

	public const string Loaded = "SETTINGS_LOADED";

	public const string NotInitialized = "SETTINGS_NOT_INITIALIZED";

	public const string SaveUnavailable = "SETTINGS_SAVE_UNAVAILABLE";

	public const string Saved = "SETTINGS_SAVED";

	public const string ValuesRepaired = "SETTINGS_VALUES_REPAIRED";
}
