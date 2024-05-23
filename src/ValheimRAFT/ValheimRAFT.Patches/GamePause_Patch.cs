using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimRAFT.Util;
using ValheimVehicles.Vehicles;
using ValheimVehicles.Vehicles.Components;
using Logger = Jotunn.Logger;

namespace ValheimRAFT.Patches;

public class GamePause_Patch
{
  // does not need transpiler, should always set to 1f on a ship
  // This could be applied to other ships for QOL fixes...would be good for base valheim
  [HarmonyPatch(typeof(Game), "UpdatePause")]
  [HarmonyPostfix]
  private static void UpdatePausePostfix()
  {
    PreventTimeFreezeOnShip();
  }


  public static IEnumerator SetPlayerOnBoat(ZDO zdo, Player player)
  {
    var zdoSector = zdo.GetSector();
    var zdoPosition = zdo.GetPosition();
    var playerOffsetHash = BaseVehicleController.GetDynamicParentOffset(player.m_nview);

    ZoneSystem.instance.PokeLocalZone(zdoSector);
    yield return new WaitUntil(() => ZoneSystem.instance.IsZoneLoaded(zdoSector));

    var newPosition = zdoPosition + playerOffsetHash;
    player.transform.position = newPosition;
    player.m_nview.GetZDO().SetPosition(newPosition);

    yield return null;
  }

  [HarmonyPatch(typeof(Bed), "Interact")]
  [HarmonyPostfix]
  private static void OnPlayerBedInteract(Bed __instance)
  {
    var isCurrent = __instance.IsCurrent();
    var spawnController = Player.m_localPlayer.GetComponentInChildren<PlayerSpawnController>();
    if (!spawnController) return;

    if (isCurrent)
    {
      spawnController.SyncSpawnPoint(__instance.m_nview);
    }
    else
    {
      spawnController.RemoveSpawnPointFromVehicle();
    }
  }

  [HarmonyPatch(typeof(PlayerProfile), "SaveLogoutPoint")]
  [HarmonyPostfix]
  private static void OnSaveLogoutPoint()
  {
    var spawnController = Player.m_localPlayer.GetComponentInChildren<PlayerSpawnController>();
    if (spawnController)
    {
      spawnController.SyncLogoutPoint(Player.m_localPlayer.gameObject);
    }
  }

  [HarmonyPatch(typeof(Game), nameof(Game.SpawnPlayer))]
  [HarmonyPostfix]
  private static void OnSpawned(Player __result)
  {
    if (ZNetView.m_forceDisableInit) return;
    var netView = Player.m_localPlayer.GetComponent<ZNetView>();
    // var netView = __result.GetComponent<ZNetView>();
    if (!netView) return;
    PlayerSpawnController.CreateSpawnDelegate(__result);

    // var playerVehicleId = BaseVehicleController.GetParentVehicleId(netView);
    // foreach (var zdo in BaseVehicleController.vehicleZdos)
    // {
    //   if (ZDOPersistentID.ZDOIDToId(zdo.m_uid) == playerVehicleId)
    //   {
    //     netView.StartCoroutine(SetPlayerOnBoat(zdo, __result));
    //     // __result.transform.position = transform.position + playerOffsetHash;
    //     // Logger.LogDebug("Adding player to active boat");
    //     break;
    //   }
    // }
  }

  private static void PreventTimeFreezeOnShip()
  {
    if (!Player.m_localPlayer) return;
    var baseVehicleShip = Player.m_localPlayer?.GetComponentInParent<BaseVehicleController>();

    // not on ship, do nothing
    if (!baseVehicleShip) return;

    var hasPeerConnections = ZNet.instance?.GetPeerConnections() > 0 ||
                             ValheimRaftPlugin.Instance.ShipPausePatchSinglePlayer.Value;
    // Previously onPause the time was set to 0 regarless if using multiplayer, which borks ZDOs and Physics updates that are reliant on the controlling player.
    // Also causes issues with Players that are on a ship controlled by another player and the ship moves out of range. The player is forced through the wall or smashed up into the air.
    if (Game.IsPaused())
    {
      Time.timeScale = hasPeerConnections ? 1f : 0f;
    }
    else
    {
      Time.timeScale = hasPeerConnections ? 1f : Game.m_timeScale;
    }
  }
}