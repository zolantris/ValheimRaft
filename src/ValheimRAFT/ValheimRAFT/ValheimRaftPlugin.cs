﻿using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using Components;
using Jotunn;
using Properties;
using UnityEngine;
using ValheimRAFT.Patches;
using ValheimRAFT.Util;
using ValheimVehicles.ConsoleCommands;
using ValheimVehicles.Prefabs;
using ValheimVehicles.Vehicles;
using Logger = Jotunn.Logger;

namespace ValheimRAFT;

// [SentryDSN()]
[BepInPlugin(BepInGuid, ModName, Version)]
[BepInDependency(Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
public class ValheimRaftPlugin : BaseUnityPlugin
{
  /*
   * @note keeping this as Sarcen for now since there are low divergences from the original codebase and patches already mapped to sarcen's mod
   */
  public const string Author = "Zolantris";
  public const string Version = "2.0.0";
  internal const string ModName = "ValheimRAFT";
  public const string BepInGuid = $"{Author}.{ModName}";
  private const string HarmonyGuid = $"{Author}.{ModName}";
  public const string ModDescription = "Valheim Mod for building on the sea";
  public const string CopyRight = "Copyright © 2023-2024, GNU-v3 licensed";
  public static readonly int CustomRaftLayer = 29;
  private bool m_customItemsAdded;
  public PrefabRegistryController prefabController;

  public static ValheimRaftPlugin Instance { get; private set; }

  public ConfigEntry<bool> MakeAllPiecesWaterProof { get; set; }

  public ConfigEntry<bool> AllowFlight { get; set; }

  public ConfigEntry<string> PluginFolderName { get; set; }
  public ConfigEntry<float> InitialRaftFloorHeight { get; set; }
  public ConfigEntry<bool> PatchPlanBuildPositionIssues { get; set; }
  public ConfigEntry<float> RaftHealth { get; set; }
  public ConfigEntry<float> ServerRaftUpdateZoneInterval { get; set; }
  public ConfigEntry<float> RaftSailForceMultiplier { get; set; }
  public ConfigEntry<bool> DisplacedRaftAutoFix { get; set; }
  public ConfigEntry<bool> AdminsCanOnlyBuildRaft { get; set; }


  // Propulsion Configs
  public ConfigEntry<bool> EnableCustomPropulsionConfig { get; set; }

  public ConfigEntry<float> MaxPropulsionSpeed { get; set; }
  public ConfigEntry<float> MaxSailSpeed { get; set; }
  public ConfigEntry<float> SpeedCapMultiplier { get; set; }
  public ConfigEntry<bool> FlightVerticalIsToggle { get; set; }
  public ConfigEntry<bool> FlightVerticalAccelerates { get; set; }
  public ConfigEntry<bool> FlightNoAngularVelocity { get; set; }
  public ConfigEntry<bool> FlightHasDrag { get; set; }


  // for those that want to cruise with rudder
  // public ConfigEntry<bool> AllowRudderSpeed { get; set; }
  // public ConfigEntry<float> RudderSpeed2 { get; set; }
  // public ConfigEntry<float> RudderSpeed3 { get; set; }
  public ConfigEntry<float> SailTier1Area { get; set; }
  public ConfigEntry<float> SailTier2Area { get; set; }
  public ConfigEntry<float> SailTier3Area { get; set; }
  public ConfigEntry<float> SailCustomAreaTier1Multiplier { get; set; }
  public ConfigEntry<float> BoatDragCoefficient { get; set; }
  public ConfigEntry<float> MastShearForceThreshold { get; set; }
  public ConfigEntry<bool> HasDebugSails { get; set; }
  public ConfigEntry<bool> HasDebugBase { get; set; }

  public ConfigEntry<bool> HasShipWeightCalculations { get; set; }
  public ConfigEntry<float> MassPercentageFactor { get; set; }
  public ConfigEntry<bool> ShowShipStats { get; set; }
  public ConfigEntry<bool> HasShipContainerWeightCalculations { get; set; }
  public ConfigEntry<float> RaftCreativeHeight { get; set; }
  public ConfigEntry<float> FloatingColliderVerticalSize { get; set; }
  public ConfigEntry<float> FloatingColliderVerticalCenterOffset { get; set; }
  public ConfigEntry<float> BlockingColliderVerticalSize { get; set; }
  public ConfigEntry<float> BlockingColliderVerticalCenterOffset { get; set; }
  public ConfigEntry<KeyboardShortcut> AnchorKeyboardShortcut { get; set; }
  public ConfigEntry<bool> EnableMetrics { get; set; }
  public ConfigEntry<bool> EnableExactVehicleBounds { get; set; }
  public ConfigEntry<bool> AutoUpgradeV1Raft { get; set; }
  public ConfigEntry<bool> ProtectVehiclePiecesOnErrorFromWearNTearDamage { get; set; }
  public ConfigEntry<bool> DebugRemoveStartMenuBackground { get; set; }


  /**
   * These folder names are matched for the CustomTexturesGroup
   */
  public string[] possibleModFolderNames =
  [
    $"{Author}-{ModName}", $"zolantris-{ModName}", $"Zolantris-{ModName}", ModName
  ];

  private ConfigDescription CreateConfigDescription(string description, bool isAdmin = false)
  {
    return new ConfigDescription(
      description,
      null,
      new ConfigurationManagerAttributes()
      {
        IsAdminOnly = true
      }
    );
  }

  /**
   * @todo will port to valheim vehicles plugin soon.
   */
  private void CreateVehicleConfig()
  {
    AutoUpgradeV1Raft = Config.Bind("ValheimVehicles", "Auto Upgrade Raft", false,
      CreateConfigDescription(
        "Automatically updates the RaftV1 to the new raft. This allows players to update smoothly into the new raft"));
    EnableExactVehicleBounds = Config.Bind("ValheimVehicles", "EnableExactVehicleBounds", false,
      CreateConfigDescription(
        "Ensures that a piece placed within the raft is included in the float collider correctly. This only applies if the piece does not have proper colliders. Likely useful only for compatibility for other mods. Piece bounds of X and Z are considered, no height is factored into floating collider bounds (which controls ship floatation). Height IS factored into the onboard trigger.",
        false));
  }

  private void CreateColliderConfig()
  {
    FloatingColliderVerticalCenterOffset = Config.Bind("Debug",
      "FloatingColliderVerticalCenterOffset",
      -0.2f,
      CreateConfigDescription(
        "Sets the raft vertical collision center location original value is -0.2f. Lower offsets can make the boat more jittery, positive offsets will cause the boat to go underwater in areas",
        false));
    FloatingColliderVerticalSize = Config.Bind("Debug", "FloatingColliderVerticalSize",
      3f,
      CreateConfigDescription(
        "Sets the raft floating collider size. Smaller sizes can make the boat more jittery",
        false));

    BlockingColliderVerticalCenterOffset = Config.Bind("Debug",
      "BlockingColliderVerticalCenterOffset",
      -1.5f,
      CreateConfigDescription(
        "Sets the raft BlockingColliderVerticalCenterOffset which blocks the player or objects passing through. This will trigger physics so if there is an interaction between the boat and player/it can cause the player to push the boat in the direction of interaction",
        false));
    BlockingColliderVerticalSize = Config.Bind("Debug", "BlockingColliderVerticalSize",
      3f,
      CreateConfigDescription(
        "Sets sets the raft blocking collider size.", false));
  }

  private void CreateCommandConfig()
  {
    RaftCreativeHeight = Config.Bind("Config", "RaftCreativeHeight",
      5f,
      CreateConfigDescription(
        "Sets the raftcreative command height, raftcreative is relative to the current height of the ship, negative numbers will sink your ship temporarily",
        false));
  }

  private void CreatePropulsionConfig()
  {
    ShowShipStats = Config.Bind("Debug", "ShowShipState", true);
    MaxPropulsionSpeed = Config.Bind("Propulsion", "MaxSailSpeed", 18f,
      CreateConfigDescription(
        "Sets the absolute max speed a ship can ever hit. Prevents or enables space launches",
        true));
    MaxSailSpeed = Config.Bind("Propulsion", "MaxSailSpeed", 10f,
      CreateConfigDescription(
        "Sets the absolute max speed a ship can ever hit with sails. Prevents or enables space launches, cannot exceed MaxPropulsionSpeed.",
        true));
    MassPercentageFactor = Config.Bind("Propulsion", "MassPercentage", 55f, CreateConfigDescription(
      "Sets the mass percentage of the ship that will slow down the sails",
      true));
    SpeedCapMultiplier = Config.Bind("Propulsion", "SpeedCapMultiplier", 1f,
      CreateConfigDescription(
        "Sets the speed at which it becomes significantly harder to gain speed per sail area",
        true));

    // RudderSpeed2 = Config.Bind("Propulsion", "RudderSpeed2", 5f,
    //   CreateConfigDescription(
    //     "Max speed at rudder speed 2.", true));
    // RudderSpeed3 = Config.Bind("Propulsion", "RudderSpeed3", 10f,
    //   CreateConfigDescription(
    //     "", true));
    // AllowRudderSpeed = Config.Bind("Propulsion", "AllowRudderSpeed", true,
    //   CreateConfigDescription(
    //     "", true));

    HasShipWeightCalculations = Config.Bind("Propulsion", "HasShipWeightCalculations", true,
      CreateConfigDescription(
        "enables ship weight calculations for sail-force (sailing speed) and future propulsion, makes larger ships require more sails and smaller ships require less"));

    HasShipContainerWeightCalculations = Config.Bind("Propulsion",
      "HasShipContainerWeightCalculations",
      true,
      CreateConfigDescription(
        "enables ship weight calculations for containers which affects sail-force (sailing speed) and future propulsion calculations. Makes ships with lots of containers require more sails"));

    HasDebugSails = Config.Bind("Debug", "HasDebugSails", false,
      CreateConfigDescription(
        "Outputs all custom sail information when saving and updating ZDOs for the sails. Debug only."));

    EnableCustomPropulsionConfig = Config.Bind("Propulsion",
      "EnableCustomPropulsionConfig", SailAreaForce.HasPropulsionConfigOverride,
      CreateConfigDescription("Enables all custom propulsion values", false));

    SailCustomAreaTier1Multiplier = Config.Bind("Propulsion",
      "SailCustomAreaTier1Multiplier", SailAreaForce.CustomTier1AreaForceMultiplier,
      CreateConfigDescription(
        "Manual sets the sail wind area multiplier the custom tier1 sail. Currently there is only 1 tier",
        true)
    );

    SailTier1Area = Config.Bind("Propulsion",
      "SailTier1Area", SailAreaForce.Tier1,
      CreateConfigDescription("Manual sets the sail wind area of the tier 1 sail.", true)
    );

    SailTier2Area = Config.Bind("Propulsion",
      "SailTier2Area", SailAreaForce.Tier2,
      CreateConfigDescription("Manual sets the sail wind area of the tier 2 sail.", true));

    SailTier3Area = Config.Bind("Propulsion",
      "SailTier3Area", SailAreaForce.Tier3,
      CreateConfigDescription("Manual sets the sail wind area of the tier 3 sail.", true));
  }

  private void CreateServerConfig()
  {
    ProtectVehiclePiecesOnErrorFromWearNTearDamage = Config.Bind("Server config",
      "Protect Vehicle pieces from breaking on Error", true,
      "Protects against crashes breaking raft/vehicle initialization causing raft/vehicles to slowly break pieces attached to it. This will make pieces attached to valid raft ZDOs unbreakable from damage, but still breakable with hammer");
    AdminsCanOnlyBuildRaft = Config.Bind("Server config", "AdminsCanOnlyBuildRaft", false,
      new ConfigDescription(
        "ValheimRAFT hammer menu pieces are registered as disabled unless the user is an Admin, allowing only admins to create rafts. This will update automatically make sure to un-equip the hammer to see it apply (if your remove yourself as admin). Server / client does not need to restart",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));

    ServerRaftUpdateZoneInterval = Config.Bind<float>("Server config",
      "ServerRaftUpdateZoneInterval",
      10f,
      new ConfigDescription(
        "Allows Server Admin control over the update tick for the RAFT location. Larger Rafts will take much longer and lag out players, but making this ticket longer will make the raft turn into a box from a long distance away.",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));

    MakeAllPiecesWaterProof = Config.Bind<bool>("Server config",
      "MakeAllPiecesWaterProof", true, new ConfigDescription(
        "Makes it so all building pieces (walls, floors, etc) on the ship don't take rain damage.",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
    AllowFlight = Config.Bind<bool>("Server config", "AllowFlight", false,
      new ConfigDescription("Allow the raft to fly (jump\\crouch to go up and down)",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = true
          }
        }));
  }

  private void CreateFlightPropulsionConfig()
  {
    FlightVerticalAccelerates = Config.Bind<bool>("Propulsion",
      "Flight Vertical will increase in speed ever second the toggle is held",
      true, "Slowly accelerates accent and descent.");
    FlightVerticalIsToggle = Config.Bind<bool>("Propulsion",
      "Flight Vertical Continues UntilToggled",
      true,
      "Saves the user's fingers by allowing the ship to continue to climb or descend without needing to hold the button");
    FlightNoAngularVelocity = Config.Bind<bool>("Propulsion",
      "Flight no angular velocity",
      true,
      "Makes the flying vehicle use only foward or backward velocity. This makes the ship less realistic but much more controllable.");
    FlightHasDrag = Config.Bind<bool>("Propulsion",
      "Enable Flight Drag",
      true,
      "Makes the forward or backward velocity drop fast when velocity decreases. Makes flying easier");
  }

  private void CreateDebugConfig()
  {
    DebugRemoveStartMenuBackground =
      Config.Bind("Debug", "DebugRemoveStartMenuBackground", false,
        "Removes the start scene background");
    DisplacedRaftAutoFix = Config.Bind("Debug",
      "DisplacedRaftAutoFix", false,
      "Automatically fix a displaced glitched out raft if the player is standing on the raft. This will make the player fall into the water briefly but avoid having to run 'raftoffset 0 0 0'");
  }

  private void CreatePrefabConfig()
  {
    RaftHealth = Config.Bind<float>("Config", "raftHealth", 500f,
      "Set the raft health when used with wearNTear, lowest value is 100f");
  }

  private void CreateBaseConfig()
  {
    EnableMetrics = Config.Bind("Debug", "enableMetrics", true,
      CreateConfigDescription(
        "Enable sentry debug logging which will make it easier to troubleshoot raft errors and detect performance bottlenecks. The bare minimum is collected, and only data related to ValheimRaft. See https://github.com/zolantris/ValheimMods/tree/main/src/ValheimRAFT#logging-metrics for more details about what is collected"));
    HasDebugBase = Config.Bind("Debug", "HasDebugBase", false,
      CreateConfigDescription(
        "Outputs more debug logs for the MoveableBaseRootComponent. Useful for troubleshooting errors, but may fill logs quicker"));
    PatchPlanBuildPositionIssues = Config.Bind<bool>("Patches",
      "fixPlanBuildPositionIssues", false, new ConfigDescription(
        "Fixes the PlanBuild mod position problems with ValheimRaft so it uses localPosition of items based on the parent raft. This MUST be enabled to support PlanBuild but can be disabled when the mod owner adds direct support for this part of ValheimRAFT.",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));

    InitialRaftFloorHeight = Config.Bind<float>("Config",
      "Initial Floor Height", 0.6f, new ConfigDescription(
        "Allows users to set the raft floor spawn height. 0.45 was the original height in 1.4.9 but it looked a bit too low. Now people can customize it",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));

    PluginFolderName = Config.Bind<string>("Config",
      "pluginFolderName", "", new ConfigDescription(
        "Users can leave this empty. If they do not, the mod will attempt to match the folder string. Allows users to set the folder search name if their" +
        $" manager renames the folder, r2modman has a fallback case added to search for {Author}-{ModName}" +
        "Default search values are an ordered list first one is always matching non-empty strings from this pluginFolderName." +
        $"Folder Matches are:  {Author}-{ModName}, zolantris-{ModName} Zolantris-{ModName}, and {ModName}",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));
    PluginFolderName = Config.Bind<string>("Config",
      "pluginFolderName", "", new ConfigDescription(
        "Users can leave this empty. If they do not, the mod will attempt to match the folder string. Allows users to set the folder search name if their" +
        $" manager renames the folder, r2modman has a fallback case added to search for {Author}-{ModName}" +
        "Default search values are an ordered list first one is always matching non-empty strings from this pluginFolderName." +
        $"Folder Matches are:  {Author}-{ModName}, zolantris-{ModName} Zolantris-{ModName}, and {ModName}",
        (AcceptableValueBase)null, new object[1]
        {
          (object)new ConfigurationManagerAttributes()
          {
            IsAdminOnly = false
          }
        }));
  }

  private void CreateKeyboardSetup()
  {
    AnchorKeyboardShortcut =
      Config.Bind("Config", "AnchorKeyboardShortcut", new KeyboardShortcut(KeyCode.LeftShift),
        new ConfigDescription("Anchor keyboard hotkey. Only applies to keyboard"));
  }

  /*
   * aggregates all config creators.
   *
   * Future plans:
   * - Abstract specific config directly into related files and call init here to set those values in the associated classes.
   * - Most likely those items will need to be "static" values.
   * - Add a watcher so those items can take the new config and process it as things update.
   */

  private void CreateConfig()
  {
    //
    // Config.SettingChanged += 
    CreateBaseConfig();
    CreatePrefabConfig();
    CreateDebugConfig();
    CreateServerConfig();
    CreateCommandConfig();
    CreateColliderConfig();
    CreateKeyboardSetup();

    // vehicles
    CreateVehicleConfig();
    CreatePropulsionConfig();
  }

  internal void ApplyMetricIfAvailable()
  {
    var @namespace = "SentryUnityWrapper";
    var @pluginClass = "SentryUnityWrapperPlugin";
    Logger.LogDebug(
      $"contains sentryunitywrapper: {Chainloader.PluginInfos.ContainsKey("zolantris.SentryUnityWrapper")}");

    Logger.LogDebug($"plugininfos {Chainloader.PluginInfos}");

    if (!EnableMetrics.Value ||
        !Chainloader.PluginInfos.ContainsKey("zolantris.SentryUnityWrapper")) return;
    Logger.LogDebug("Made it to sentry check");
    SentryMetrics.ApplyMetrics();
  }

  public void Awake()
  {
    Instance = this;


    CreateConfig();
    PatchController.Apply(HarmonyGuid);

    AddPhysicsSettings();

    RegisterConsoleCommands();
    RegisterVehicleConsoleCommands();

    /*
     * @todo add a way to skip LoadCustomTextures when on server. This check when used here crashes the Plugin.
     */
    PrefabManager.OnVanillaPrefabsAvailable += LoadCustomTextures;
    PrefabManager.OnVanillaPrefabsAvailable += AddCustomPieces;
  }

  public void RegisterConsoleCommands()
  {
    CommandManager.Instance.AddConsoleCommand(new CreativeModeConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new MoveRaftConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new HideRaftConsoleCommand());
    CommandManager.Instance.AddConsoleCommand(new RecoverRaftConsoleCommand());
  }


  // this will be removed when vehicles becomes independent of valheim raft.
  public void RegisterVehicleConsoleCommands()
  {
    CommandManager.Instance.AddConsoleCommand(new VehicleCommands());
  }

  private void Start()
  {
    // SentryLoads after
    ApplyMetricIfAvailable();
    AddGuiLayerComponents();
  }

  /**
   * Important for raft collisions to only include water and landmass colliders.
   *
   * Other collisions on the piece level are not handled on the CustomRaftLayer
   *
   * todo remove CustomRaftLayer and use the VehicleLayer instead.
   * - Requires adding explicit collision ignores for the rigidbody attached to VehicleInstance (m_body)
   */
  private void AddPhysicsSettings()
  {
    var layer = LayerMask.NameToLayer("vehicle");

    for (var index = 0; index < 32; ++index)
      Physics.IgnoreLayerCollision(CustomRaftLayer, index,
        Physics.GetIgnoreLayerCollision(layer, index));

    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("vehicle"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("piece"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("character"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("smoke"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_ghost"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("weapon"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("blocker"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("pathblocker"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("viewblock"),
      true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_net"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("character_noenv"), true);
    Physics.IgnoreLayerCollision(CustomRaftLayer,
      LayerMask.NameToLayer("Default_small"), false);
    Physics.IgnoreLayerCollision(CustomRaftLayer, LayerMask.NameToLayer("Default"),
      false);
  }

  private void LoadCustomTextures()
  {
    var sails = CustomTextureGroup.Load("Sails");
    foreach (var texture3 in sails.Textures)
    {
      texture3.Texture.wrapMode = TextureWrapMode.Clamp;
      if ((bool)texture3.Normal) texture3.Normal.wrapMode = TextureWrapMode.Clamp;
    }

    var patterns = CustomTextureGroup.Load("Patterns");
    foreach (var texture2 in patterns.Textures)
    {
      texture2.Texture.filterMode = FilterMode.Point;
      texture2.Texture.wrapMode = TextureWrapMode.Repeat;
      if ((bool)texture2.Normal) texture2.Normal.wrapMode = TextureWrapMode.Repeat;
    }

    var logos = CustomTextureGroup.Load("Logos");
    foreach (var texture in logos.Textures)
    {
      texture.Texture.wrapMode = TextureWrapMode.Clamp;
      if ((bool)texture.Normal) texture.Normal.wrapMode = TextureWrapMode.Clamp;
    }
  }

  /**
   * todo: move to Vehicles plugin when it is ready
   */
  private void AddGuiLayerComponents()
  {
    gameObject.AddComponent<VehicleDebugGui>();
  }

  private void AddCustomPieces()
  {
    if (m_customItemsAdded) return;
    // Registers all prefabs using ValheimVehicles PrefabRegistryController
    prefabController = gameObject.AddComponent<PrefabRegistryController>();
    PrefabRegistryController.Init();

    m_customItemsAdded = true;
  }
}