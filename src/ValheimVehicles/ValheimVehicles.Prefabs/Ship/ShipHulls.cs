using UnityEngine;

namespace ValheimVehicles.Prefabs;

public abstract class ShipHulls
{
  public enum HullOrientation
  {
    Horizontal = 0,
    Vertical = 90,
    FortyFive = 45,
    TwentySix = 26,
  }

  public abstract class HullMaterial
  {
    public const string CoreWood = "core_wood";
    public const string ElderWood = "elder_wood";
    public const string Wood = "wood";
    public const string Iron = "iron";
    public const string YggdrasilWood = "yggdrasil_wood";
  }

  public static void SetMaterialHealthValues(string hullMaterial, WearNTear wnt, int pieces)
  {
    switch (hullMaterial)
    {
      // wood health support is 400 for a normal plank but these are extra thick so +200
      // iron reinforced wood is 2000 health but these are rustic plats but can be made larger and are also slightly weaker since they incorporate bronze out layer
      case HullMaterial.Iron:
        PrefabRegistryHelpers.SetWearNTearSupport(wnt, WearNTear.MaterialType.Iron);
        wnt.m_health = 800 * pieces;
        break;
      case HullMaterial.Wood:
        wnt.m_health = 200 * pieces;
        break;
    }
  }
}