using UserSettings.ServerSpecific;

namespace ZombieOptOut;

internal class ServerSpecificSettings
{
    public static SSDropdownSetting optOutDropdown;
    public static Dictionary<ReferenceHub, int> savedSettings = [];
    static ServerSpecificSettingBase[] Settings;
    public enum OptOutType
    {
        OptIn,
        OptInAndFill,
        OptOut,
        OptOutFully
    };

    public static void Initialize()
    {
        if (ZombieOptOut.Main.Instance.Config.EnableSimpleCustomRolesSupport)
            optOutDropdown = new SSDropdownSetting(null, "Zombie Opt-Out Mode", ["Default (Not Opted-Out)", "Default + Fill (Replace Opted-Out players)", "Opt-Out", "Out-Out+ (Includes custom roles)"]);
        else
            optOutDropdown = new SSDropdownSetting(null, "Zombie Opt-Out Mode", ["Default (Not Opted-Out)", "Fill for Opted-Out Players", "Opt-Out"]);

        Settings =
        [
            new SSGroupHeader("Zombie Opt-out", false),
            optOutDropdown,
        ];
        List<ServerSpecificSettingBase> settingBases = [];
        if (ServerSpecificSettingsSync.DefinedSettings != null)
        {
            settingBases = [.. ServerSpecificSettingsSync.DefinedSettings];
        }
        settingBases.AddRange(Settings);
        ServerSpecificSettingsSync.DefinedSettings = [.. settingBases];
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += ServerOnSettingValueReceived;
        ServerSpecificSettingsSync.SendToAll();
    }

    public static void DeInitialize()
    {
        ServerSpecificSettingsSync.DefinedSettings = [];
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= ServerOnSettingValueReceived;
        ServerSpecificSettingsSync.SendToAll();
    }

    private static void ServerOnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase @base)
    {
        if (!savedSettings.TryGetValue(hub, out var val))
            savedSettings.Add(hub, 0);
        
        var settings = savedSettings[hub];

        var player = Player.Get(hub);

        if (player == null)
            return;

        if (@base is SSDropdownSetting dropdown && dropdown.SettingId == optOutDropdown.SettingId)
            settings = optOutDropdown.SyncSelectionIndexValidated;

        savedSettings[hub] = settings;
    }
}