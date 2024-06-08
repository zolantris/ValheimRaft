using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Extensions;
using Jotunn.Managers;
using UnityEngine;
using ValheimRAFT;
using ValheimVehicles.Vehicles;
using ValheimVehicles.Vehicles.Components;

namespace ValheimVehicles.Prefabs.Registry;

/**
 * example registry of a prefab
 */
public class WaterVehiclePrefab : IRegisterPrefab
{
  public static readonly WaterVehiclePrefab Instance = new();

  public static GameObject CreateClonedPrefab(string prefabName)
  {
    return PrefabManager.Instance.CreateClonedPrefab(prefabName,
      LoadValheimVehicleAssets.VehicleShipAsset);
  }

  /**
   * todo it's possible this all needs to be done in the Awake method to safely load valheim.
   * Should test this in development build of valheim
   */
  public static GameObject CreateWaterVehiclePrefab(
    GameObject prefab)
  {
    var netView = PrefabRegistryHelpers.AddNetViewWithPersistence(prefab);
    netView.m_type = ZDO.ObjectType.Prioritized;

    var vehicleMovementObj = prefab.transform.Find("vehicle_movement");

    // colliders already have a rigidbody on them from unity prefab
    var vehicleMovementColliders = vehicleMovementObj.transform.Find("colliders");
    var pieces = vehicleMovementObj.transform.Find("pieces");
    var movingPieces = vehicleMovementObj.transform.Find("moving_pieces");

    var shipPhysicsNetView = vehicleMovementColliders.gameObject.AddComponent<ZNetView>();
    shipPhysicsNetView.m_persistent = false;
    shipPhysicsNetView.m_distant = true;

    var shipPhysicsZSyncTransform =
      vehicleMovementColliders.gameObject.AddComponent<ZSyncTransform>();

    shipPhysicsZSyncTransform.m_syncPosition = true;
    shipPhysicsZSyncTransform.m_syncBodyVelocity = true;
    shipPhysicsZSyncTransform.m_syncRotation = true;

    var floatColliderObj =
      vehicleMovementColliders.Find(
        PrefabNames.WaterVehicleFloatCollider);
    var blockingColliderObj =
      vehicleMovementColliders.Find(PrefabNames
        .WaterVehicleBlockingCollider);
    var onboardColliderObj =
      vehicleMovementColliders.Find(PrefabNames
        .WaterVehicleOnboardCollider);

    onboardColliderObj.name = PrefabNames.WaterVehicleOnboardCollider;
    floatColliderObj.name = PrefabNames.WaterVehicleFloatCollider;
    blockingColliderObj.name = PrefabNames.WaterVehicleBlockingCollider;

    var floatBoxCollider = floatColliderObj.GetComponent<BoxCollider>();

    /*
     * ShipControls were a gameObject with a script attached to them. This approach directly attaches the script instead of having the rudder show.
     */
    var vehicleRigidbody = prefab.GetComponent<Rigidbody>();
    var piecesSyncNetView = pieces.gameObject.AddComponent<ZNetView>();
    piecesSyncNetView.m_persistent = false;
    piecesSyncNetView.m_distant = true;
    var zSyncTransform = pieces.gameObject.AddComponent<ZSyncTransform>();

    zSyncTransform.m_syncPosition = true;
    zSyncTransform.m_syncBodyVelocity = true;
    zSyncTransform.m_syncRotation = true;
    zSyncTransform.m_body = vehicleRigidbody;


    var shipInstance = vehicleMovementObj.gameObject.AddComponent<VehicleShip>();
    var shipControls = vehicleMovementObj.gameObject.AddComponent<VehicleMovementController>();
    shipInstance.ColliderParentObj = vehicleMovementColliders.gameObject;

    shipInstance.ShipDirection =
      floatColliderObj.FindDeepChild(PrefabNames.VehicleShipMovementOrientation);
    shipInstance.m_shipControlls = shipControls;
    shipInstance.MovementController = shipControls;
    shipInstance.gameObject.layer = ValheimRaftPlugin.CustomRaftLayer;
    shipInstance.m_body = vehicleRigidbody;
    shipInstance.m_zsyncTransform = zSyncTransform;

    // todo fix ship water effects so they do not cause ship materials to break

    var waterEffects =
      Object.Instantiate(LoadValheimAssets.shipWaterEffects, prefab.transform);
    waterEffects.name = PrefabNames.VehicleShipEffects;
    var shipEffects = waterEffects.GetComponent<ShipEffects>();
    var vehicleShipEffects = waterEffects.AddComponent<VehicleShipEffects>();
    VehicleShipEffects.CloneShipEffectsToInstance(vehicleShipEffects, shipEffects);
    Object.Destroy(shipEffects);

    vehicleShipEffects.transform.localPosition = new Vector3(0, -2, 0);
    shipInstance.ShipEffectsObj = vehicleShipEffects.gameObject;
    shipInstance.ShipEffects = vehicleShipEffects;

    shipInstance.m_floatcollider = floatBoxCollider;
    shipInstance.FloatCollider = floatBoxCollider;

    // wearntear may need to be removed or tweaked
    prefab.AddComponent<WearNTear>();
    var woodWNT = LoadValheimAssets.woodFloorPiece.GetComponent<WearNTear>();
    var wnt = PrefabRegistryHelpers.SetWearNTear(prefab, 1, true);
    PrefabRegistryHelpers.SetWearNTearSupport(wnt, WearNTear.MaterialType.HardWood);

    wnt.m_onDestroyed += woodWNT.m_onDestroyed;
    // triggerPrivateArea will damage enemies/pieces when within it
    wnt.m_triggerPrivateArea = true;

    wnt.m_supports = true;
    wnt.m_support = 2000f;
    wnt.m_noSupportWear = true;
    wnt.m_noRoofWear = true;
    wnt.enabled = false;

    // todo ImpactEffect likely never should have been added like this
    // todo remove if unnecessary
    var impactEffect = vehicleMovementObj.gameObject.AddComponent<ImpactEffect>();
    impactEffect.m_triggerMask = LayerMask.GetMask("Default", "character", "piece", "terrain",
      "static_solid", "Default_small", "character_net", "vehicle", LayerMask.LayerToName(29));
    impactEffect.m_toolTier = 1000;

    impactEffect.m_damages.m_blunt = 50;
    impactEffect.m_damages.m_chop = 0;
    impactEffect.m_damages.m_pickaxe = 0;

    impactEffect.m_interval = 0.5f;
    impactEffect.m_damagePlayers = true;
    impactEffect.m_damageToSelf = false;
    impactEffect.m_damageFish = true;
    impactEffect.m_hitType = HitData.HitType.Boat;
    impactEffect.m_minVelocity = 0.1f;
    impactEffect.m_maxVelocity = 7;

    return prefab;
  }

  private static void RegisterWaterVehicleShipPrefab()
  {
    var prefab = CreateClonedPrefab(PrefabNames.WaterVehicleShip);
    var waterVehiclePrefab = CreateWaterVehiclePrefab(prefab);

    var piece = PrefabRegistryHelpers.AddPieceForPrefab(PrefabNames.WaterVehicleShip, prefab);
    piece.m_waterPiece = true;


    // todo likely does nothing
    // piece.m_targetNonPlayerBuilt = true;
    // piece.m_primaryTarget = true;
    // piece.m_randomTarget = true;

    PieceManager.Instance.AddPiece(new CustomPiece(waterVehiclePrefab, true, new PieceConfig
    {
      PieceTable = "Hammer",
      Category = PrefabNames.ValheimRaftMenuName,
      Enabled = true,
      Requirements =
      [
        new RequirementConfig
        {
          Amount = 20,
          Item = "Wood",
          Recover = true
        }
      ]
    }));
  }

  private static void RegisterNautilusVehicleShipPrefab()
  {
    var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabNames.Nautilus,
      LoadValheimVehicleAssets.ShipNautilus);

    PrefabRegistryHelpers.AddNetViewWithPersistence(prefab);

    var piece = PrefabRegistryHelpers.AddPieceForPrefab(PrefabNames.Nautilus, prefab);
    piece.m_waterPiece = true;

    var wnt = PrefabRegistryHelpers.SetWearNTear(prefab, 3);

    PieceManager.Instance.AddPiece(new CustomPiece(prefab, true, new PieceConfig
    {
      PieceTable = "Hammer",
      Category = PrefabNames.ValheimRaftMenuName,
      Enabled = true,
      Requirements =
      [
        new RequirementConfig
        {
          Amount = 200,
          Item = "Bronze",
          Recover = true
        }
      ]
    }));
  }

  public void Register(PrefabManager prefabManager, PieceManager pieceManager)
  {
    RegisterWaterVehicleShipPrefab();
    RegisterNautilusVehicleShipPrefab();
  }
}