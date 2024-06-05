using HarmonyLib;
using ValheimRAFT.Util;
using ValheimVehicles.Vehicles;
using ValheimVehicles.Vehicles.Components;

namespace ValheimRAFT.Patches;

[HarmonyPatch]
public class ZDO_Patch
{
  [HarmonyPatch(typeof(ZDO), "Deserialize")]
  [HarmonyPostfix]
  private static void ZDO_Deserialize(ZDO __instance, ZPackage pkg)
  {
    ZDOLoaded(__instance);
  }

  [HarmonyPatch(typeof(ZDO), "Load")]
  [HarmonyPostfix]
  private static void ZDO_Load(ZDO __instance, ZPackage pkg, int version)
  {
    ZDOLoaded(__instance);
  }

  private static void ZDOLoaded(ZDO zdo)
  {
    ZdoPersistManager.Instance.Register(zdo);
    BaseVehicleController.InitZdo(zdo);

    // deprecated will remove soon
    if (ValheimRaftPlugin.Instance.AllowOldV1RaftRecipe.Value)
    {
      MoveableBaseRootComponent.InitZDO(zdo);
    }
  }

  [HarmonyPatch(typeof(ZDO), "Reset")]
  [HarmonyPrefix]
  private static void ZDO_Reset(ZDO __instance)
  {
    ZDOUnload(__instance);
  }

  public static void ZDOUnload(ZDO zdo)
  {
    // deprecated will remove soon
    if (ValheimRaftPlugin.Instance.AllowOldV1RaftRecipe.Value)
    {
      MoveableBaseRootComponent.RemoveZDO(zdo);
    }

    BaseVehicleController.RemoveZDO(zdo);
    ZdoPersistManager.Instance.Unregister(zdo);
  }
}