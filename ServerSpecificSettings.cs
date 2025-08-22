using UserSettings.ServerSpecific;

namespace ZombieOptOut
{
    internal class ServerSpecificSettings
    {
        public static SSTwoButtonsSetting optOutButton;
        public static SSTwoButtonsSetting autoFillButton;
        public static Dictionary<ReferenceHub, (bool /* optOut */, bool /* fill */)> savedSettings = [];
        static ServerSpecificSettingBase[] Settings;

        public static void Initialize()
        {
            optOutButton = new SSTwoButtonsSetting(null, "Opt-out of being a Zombie", "True", "False", defaultIsB: true);
            autoFillButton = new SSTwoButtonsSetting(null, "Auto-Fill for opted-out Zombies", "True", "False", defaultIsB: true);
            Settings =
            [
                new SSGroupHeader("Zombie Opt-out", false),
                optOutButton,
                autoFillButton
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
                savedSettings.Add(hub, (false, false));

            var settings = savedSettings[hub];

            var player = Player.Get(hub);

            if (player == null)
                return;

            if (@base is SSTwoButtonsSetting twoButton && twoButton.SettingId == optOutButton.SettingId)
                settings.Item1 = twoButton.SyncIsA;

            if (@base is SSTwoButtonsSetting twoButton2 && twoButton2.SettingId == autoFillButton.SettingId)
                settings.Item2 = twoButton2.SyncIsA;

            savedSettings[hub] = settings;
        }
    }
}