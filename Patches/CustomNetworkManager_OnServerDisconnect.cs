using CustomPlayerEffects;
using HarmonyLib;
using MEC;
using Mirror;

namespace ZombieOptOut.Patches;


// this simple patch just moves the logic for handling disconnected players to before they take damage
// this is as to cache their health before they died
[HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnServerDisconnect))]
internal class CustomNetworkManager_OnServerDisconnect
{
    public static void Prefix(CustomNetworkManager __instance, NetworkConnectionToClient conn)
    {
        if (__instance._disconnectDrop)
        {
            try
            {
                AFKReplacement.OnDisconnected(Player.Get(conn.identity));
            }
            catch (Exception e)
            {
                // Network thread can't call CL.Error, so
                Timing.CallDelayed(0.1f, () => CL.Error(e));
            }
        }
    }
}
