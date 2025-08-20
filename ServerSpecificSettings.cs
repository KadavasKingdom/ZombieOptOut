using UserSettings.ServerSpecific;


namespace ZombieOptOut
{
    internal class ServerSpecificSettings
    {
        public static SSTwoButtonsSetting optOutButton;
        public static SSTwoButtonsSetting autoFillButton;
        public static Dictionary<ReferenceHub, (bool /* optOut */, bool /* fill */)> savedSettings = [];

        public static void Initialize()
        {
            optOutButton = new SSTwoButtonsSetting(null, "Opt-out of being a Zombie", "False", "True");
            autoFillButton = new SSTwoButtonsSetting(null, "Fill for opted-out Zombies", "False", "True");
            var settings = new List<ServerSpecificSettingBase>
                    {
                        new SSGroupHeader("Zombie Opt-out", false),
                        optOutButton,
                        autoFillButton
                    };

            ServerSpecificSettingsSync.DefinedSettings = settings.ToArray();
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += ServerOnSettingValueReceived;
            ServerSpecificSettingsSync.SendToAll();
        }

        public static void DeInitialize()
        {
            ServerSpecificSettingsSync.DefinedSettings = new ServerSpecificSettingBase[0];
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