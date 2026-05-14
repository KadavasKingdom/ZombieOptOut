using UserSettings.ServerSpecific;

namespace ZombieOptOut;

internal static class ServerSpecificSettings
{
    static ServerSpecificSettings()
    {
        OptOutButton = new SSTwoButtonsSetting(null, "Opt-out of being a Zombie", "True", "False", defaultIsB: true);
        AutoFillButton = new SSTwoButtonsSetting(null, "Auto-Fill for opted-out Zombies", "True", "False", defaultIsB: true);
    }
    
    private static readonly SSTwoButtonsSetting OptOutButton;
    private static readonly SSTwoButtonsSetting AutoFillButton;
    private static Dictionary<ReferenceHub, (bool /* optOut */, bool /* fill */)> _savedSettings = [];
    public static bool GetOptOutEnabled(ReferenceHub hub) => _savedSettings.TryGetValue(hub, out var settings) && settings.Item1;
    public static bool GetAutoFillEnabled(ReferenceHub hub) => _savedSettings.TryGetValue(hub, out var settings) && settings.Item2;

    public static void Initialize()
    {
        ServerSpecificSettingBase[] settings =
        [
            new SSGroupHeader("Zombie Opt-out"),
            OptOutButton,
            AutoFillButton
        ];

        List<ServerSpecificSettingBase> settingBases = [];
        if (ServerSpecificSettingsSync.DefinedSettings != null)
        {
            settingBases = [.. ServerSpecificSettingsSync.DefinedSettings];
        }

        settingBases.AddRange(settings);
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
        if (!_savedSettings.TryGetValue(hub, out _))
            _savedSettings.Add(hub, (false, false));

        var settings = _savedSettings[hub];

        var player = Player.Get(hub);

        if (player == null)
            return;

        if (@base is SSTwoButtonsSetting twoButton && twoButton.SettingId == OptOutButton.SettingId)
            settings.Item1 = twoButton.SyncIsA;

        if (@base is SSTwoButtonsSetting twoButton2 && twoButton2.SettingId == AutoFillButton.SettingId)
            settings.Item2 = twoButton2.SyncIsA;

        _savedSettings[hub] = settings;
    }
}