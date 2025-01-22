using BepInEx.Configuration;
using ValheimVehicles.Vehicles;
using ValheimVehicles.Vehicles.Components;

namespace ValheimVehicles.Config;

public static class CameraConfig
{
  private static ConfigFile Config = null!;
  public static ConfigEntry<float> CameraOcclusionInterval = null!;
  public static ConfigEntry<bool> CameraOcclusionEnabled = null!;

  private const string SectionKey = "Camera Optimizations";

  public static void BindConfig(ConfigFile config)
  {
    Config = config;
    CameraOcclusionInterval = Config.Bind(SectionKey,
      "CameraOcclusionInterval", 0.1f,
      ConfigHelpers.CreateConfigDescription(
        "Interval in seconds at which the camera will hide meshes in attempt to consolidate FPS / GPU memory.",
        false, false, new AcceptableValueRange<float>(0, 30f)));

    CameraOcclusionEnabled = Config.Bind(SectionKey,
      "CameraOcclusionEnabled", true, ConfigHelpers.CreateConfigDescription(
        $"Enables hiding active raft pieces at specific intervals. This will hide only the rendered texture.",
        false, false));

    CameraOcclusionInterval.SettingChanged += (sender, args) =>
      VehicleCameraCullingComponent.AddOrRemoveCameraCulling();
    CameraOcclusionEnabled.SettingChanged += (sender, args) =>
      VehicleCameraCullingComponent.AddOrRemoveCameraCulling();
  }
}