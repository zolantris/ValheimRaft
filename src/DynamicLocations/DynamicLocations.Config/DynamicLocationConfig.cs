using System.Collections.Generic;
using BepInEx.Configuration;
using DynamicLocations.Controllers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace DynamicLocations.Config;

public static class DynamicLocationsConfig
{
  public static ConfigFile? Config { get; private set; }

  // TODO add a list to be searched for an upcon interacting with the item add a popup to allow binding a spawn to the item
  // public static ConfigEntry<bool>
  //   DynamicSpawnPointGameObjects { get; private set; } = null!;

  public static ConfigEntry<List<string>> DisabledLoginApiIntegrations
  {
    get;
    set;
  } =
    null!;

  public static ConfigEntry<int> RespawnHeightOffset { get; set; } = null!;

  public static ConfigEntry<bool>
    EnableDynamicSpawnPoint { get; private set; } = null!;

  public static ConfigEntry<bool>
    EnableDynamicLogoutPoint { get; private set; } = null!;

  public static ConfigEntry<bool> FreezePlayerPosition { get; private set; } =
    null!;

  private static ConfigEntry<bool> Debug { get; set; } = null!;
  public static bool IsDebug => Debug.Value;

  public static ConfigEntry<bool> DebugDistancePortal { get; private set; } =
    null!;

  public static ConfigEntry<float> DebugForceUpdatePositionDelay
  {
    get;
    private set;
  } =
    null!;

  public static ConfigEntry<bool> DebugForceUpdatePositionAfterTeleport
  {
    get;
    private set;
  } =
    null!;

  private const string MainSection = "Main";
  private const string DebugSection = "Debug";

  public static void BindConfig(ConfigFile config)
  {
    Config = config;

    DisabledLoginApiIntegrations = Config.Bind(MainSection,
      "DisabledLoginApiIntegrations",
      new List<string>(),
      new ConfigDescription(
        $"A list of disabled plugins by GUID or name. This list will force disable any plugins matching either the guid or name. e.g. if you don't want ValheimRAFT to be enabling dynamic locations login integrations add \"zolantris.ValheimRAFT\" or \"ValheimRAFT.2.3.0\".",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = false, IsAdvanced = false }));

    FreezePlayerPosition = Config.Bind(MainSection,
      "FreezePlayerPosition",
      false,
      new ConfigDescription(
        $"Freezes the player position until the teleport and vehicle is fully loaded, prevents falling through",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = false, IsAdvanced = false }));

    DebugDistancePortal = Config.Bind(DebugSection,
      "DebugDistancePortal",
      false,
      new ConfigDescription(
        $"distance portal enabled, disabling this could break portals",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));


    DebugForceUpdatePositionDelay = Config.Bind(DebugSection,
      "DebugForceUpdatePositionDelay",
      0f,
      new ConfigDescription(
        $"distance portal enabled, disabling this could break portals",
        new AcceptableValueRange<float>(0, 5f),
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));


    DebugForceUpdatePositionAfterTeleport = Config.Bind(DebugSection,
      "DebugForceUpdatePositionAfterTeleport",
      false,
      new ConfigDescription(
        $"distance portal enabled, disabling this could break portals",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));

    EnableDynamicSpawnPoint = Config.Bind(MainSection,
      "enableDynamicSpawnPoints",
      true,
      new ConfigDescription(
        $"Enable dynamic spawn points. This will allow the user to re-spawn in a new area of the map if a vehicle has moved.",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));

    EnableDynamicLogoutPoint = Config.Bind(MainSection,
      "enableDynamicLogoutPoints",
      true,
      new ConfigDescription(
        $"Enable dynamic logout points. This will allow the user to login to a new area of the map if a vehicle has moved",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));

    RespawnHeightOffset = Config.Bind(MainSection,
      "respawnHeightOffset",
      0,
      new ConfigDescription(
        $"Sets the respawn height for beds. Useful if the player is spawning within the bed instead of above it",
        new AcceptableValueRange<int>(-5, 10),
        new ConfigurationManagerAttributes()
          { IsAdminOnly = false, IsAdvanced = true }));

    Debug = Config.Bind(MainSection,
      "debug",
      false,
      new ConfigDescription(
        $"Enable additional logging and debug drawing around spawn and logout points. Useful for debugging this mod",
        null,
        new ConfigurationManagerAttributes()
          { IsAdminOnly = true, IsAdvanced = true }));

    // Debug.SettingChanged += 
    //   class.Callback;
    DisabledLoginApiIntegrations.SettingChanged += (sender, args) =>
      LoginAPIController.UpdateIntegrations();

    if (Debug.Value)
    {
    }
  }
}