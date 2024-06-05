using BepInEx.Configuration;

namespace ValheimRAFT;

public static class ConfigHelpers
{
  public static ConfigDescription CreateConfigDescription(string description, bool isAdmin = false,
    bool isAdvanced = false)
  {
    return new ConfigDescription(
      description,
      null,
      new ConfigurationManagerAttributes()
      {
        IsAdminOnly = isAdmin,
        IsAdvanced = isAdvanced,
      }
    );
  }
}