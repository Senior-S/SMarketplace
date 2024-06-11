using HarmonyLib;
using SDG.Unturned;

namespace SeniorS.SMarketplace.Patchs;
[HarmonyPatch]
public class Inventory_Patch
{
    public delegate void PlayerCloseStorage(Player player, Items items);
    public static event PlayerCloseStorage? onPlayerCloseStorage;

    [HarmonyPatch(typeof(PlayerInventory), "closeStorage")]
    static void Prefix(PlayerInventory __instance)
    {
        onPlayerCloseStorage?.Invoke(__instance.player, __instance.items[PlayerInventory.STORAGE]);
    }
}