using System.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimRAFT.Patches;
using ValheimVehicles.Config;
using ValheimVehicles.Vehicles.Controllers;

namespace ValheimVehicles.Patches;

/// <summary>
/// from vikings do swim might get things working 
/// </summary>
[HarmonyPatch]
internal class GameCamera_WaterPatches
{
  public static float CameraPositionY = 0f;

  public static float? prevFogDensity;
  public static bool? prevFog;
  public static Color? prevFogColor;
  public static Vector2i? prevFogZone = Vector2i.zero;

  // Meant to be updated by WaterVolumePatches
  public static bool CanUpdateFog;
  public static bool previousSurfaceState;

  public static void UpdateFogBasedOnEnvironment()
  {
    EnvSetup currentEnvironment = EnvMan.instance.GetCurrentEnvironment();
    var isNight = EnvMan.IsNight();
    Color color = ((!EnvMan.IsNight())
      ? currentEnvironment.m_fogColorDay
      : currentEnvironment.m_fogColorNight);
  }

  public static Vector2i GetCurrentZone()
  {
    var playerPos = GameCamera.instance.m_playerPos;
    var currentZone =
      ZoneSystem.GetZone(new Vector2(playerPos.x, playerPos.z));
    return currentZone;
  }

  public static void UpdateFogSettings()
  {
    if (!WaterConfig.UnderwaterFogEnabled.Value) return;
    var currentZone = GetCurrentZone();
    if (WaterVolume_WaterPatches.IsCameraAboveWater)
    {
      if (prevFogZone == currentZone)
      {
        if (prevFogDensity != null)
          RenderSettings.fogDensity = prevFogDensity.Value;
        if (prevFog != null)
          RenderSettings.fog = prevFog.Value;
        if (prevFogColor != null)
          RenderSettings.fogColor = prevFogColor.Value;
      }

      prevFogDensity = null;
      prevFog = null;
      prevFogColor = null;
      prevFogZone = null;
    }
    else
    {
      prevFogDensity = RenderSettings.fogDensity;
      prevFog = RenderSettings.fog;
      prevFogColor = RenderSettings.fogColor;
      prevFogZone = currentZone;

      RenderSettings.fogColor = WaterConfig.UnderWaterFogColor.Value;
      RenderSettings.fogDensity = WaterConfig.UnderWaterFogIntensity.Value;
      RenderSettings.fog = WaterConfig.UnderwaterFogEnabled.Value;
    }

    CanUpdateFog = false;
  }

  public static void RequestUpdate(
    bool isAboveWater)
  {
    if (isAboveWater != previousSurfaceState)
    {
      CanUpdateFog = true;
    }

    previousSurfaceState = isAboveWater;
  }

  // todo fix jitters with low headroom at water level
  // [HarmonyPostfix(typeof(GameCamera), nameof(GameCamera.UpdateNearClipping))]


  [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
  [HarmonyPostfix]
  public static void GameCameraUpdateCameraPatch(GameCamera __instance,
    Camera ___m_camera)
  {
    if (Player.m_localPlayer == null ||
        WaterConfig.UnderwaterAccessMode.Value ==
        WaterConfig.UnderwaterAccessModeType.Disabled)
    {
      return;
    }

    UpdateFogSettings();
    CameraPositionY = ___m_camera.gameObject.transform.position.y;

    // This is the most important flag, it prevents camera smashing into the watermesh.
    // negative value due to it allowing zoom further out
    // fallthrough logic
    if (WaterConfig.UnderwaterShipCameraZoom.Value != 0)
    {
      __instance.m_minWaterDistance =
        WaterConfig.UnderwaterShipCameraZoom.Value * -1;
    }
    else
    {
      // default
      __instance.m_minWaterDistance = 5f;
    }
  }
}