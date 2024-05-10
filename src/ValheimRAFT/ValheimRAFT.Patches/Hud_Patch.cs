using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using a;
using HarmonyLib;
using Jotunn.Extensions;
using UnityEngine;
using UnityEngine.UI;
using ValheimVehicles.Prefabs;
using ValheimVehicles.Vehicles;
using ValheimVehicles.Vehicles.Components;
using Logger = Jotunn.Logger;

namespace ValheimRAFT.Patches;

[HarmonyPatch]
public class Hud_Patch
{
  public static CircleLine ActiveWindCircle;
  public static CircleLine InactiveWindCircle;
  public static GameObject WindCircleComponent;
  public static Image WindIndicatorImageInstance;
  public static GameObject AnchorHud;

  /// <summary>
  /// The LineRender Approach is not working, so this patch is disabled in 2.0.0
  /// </summary>
  /// <param name="windIndicatorCircle"></param>
  [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
  [HarmonyPostfix]
  public static void Hud_Awake(Hud __instance)
  {
    VehicleShipHudPatch(__instance);
  }

  public static void DisableVanillaWindIndicator(GameObject windIndicatorCircle)
  {
    WindIndicatorImageInstance = windIndicatorCircle.GetComponent<Image>();
    if (WindIndicatorImageInstance)
    {
      WindIndicatorImageInstance.enabled = false;
    }
  }

  public static void CreateCustomWindIndicator(GameObject windIndicatorCircle)
  {
    windIndicatorCircle.AddComponent<CircleWindIndicator>();
  }

  public static void AddAnchorGameObject(Transform shipPowerHud, Transform rudderIndicator)
  {
    AnchorHud = Object.Instantiate(LoadValheimVehicleAssets.HudAnchor, shipPowerHud);
    AnchorHud.name = PrefabNames.VehicleHudAnchorIndicator;
    AnchorHud.SetActive(false);

    if (rudderIndicator)
    {
      AnchorHud.transform.localPosition = rudderIndicator.localPosition;
    }
  }

  private static void ToggleAnchorHud(Ship ship)
  {
    if (!ship) return;
    var mbShip = ship.GetComponent<MoveableBaseShipComponent>();
    if (!mbShip) return;
    AnchorHud.SetActive(mbShip.IsAnchored);
  }

  private static void ToggleAnchorHud(VehicleShip vehicleShip)
  {
    if (!vehicleShip) return;
    var isAnchored = vehicleShip.MovementController.IsAnchored;
    AnchorHud.SetActive(isAnchored);
  }

  private static void VehicleShipHudPatch(Hud hud)
  {
    // fire 3 finds b/c later on these objects will have additional items added to them
    var shipHud = hud.transform?.FindDeepChild("ShipHud");
    var shipPowerIcon = shipHud?.Find("PowerIcon");
    var windIndicator = shipHud?.Find("WindIndicator");
    var rudder = shipHud.Find("Rudder");

    if (shipPowerIcon)
    {
      AddAnchorGameObject(shipPowerIcon, rudder);
    }

    var windIndicatorCircle = windIndicator?.Find("Circle");

    if (windIndicatorCircle?.gameObject)
    {
      DisableVanillaWindIndicator(windIndicatorCircle.gameObject);
      CreateCustomWindIndicator(windIndicatorCircle.gameObject);
    }
  }

  /// <summary>
  /// Most of the BaseGame Logic as of 0.217.46
  /// </summary>
  /// <param name="__instance"></param>
  /// <param name="player"></param>
  /// <param name="dt"></param>
  /// <param name="vehicleInterface"></param>
  private static void Hud_UpdateShipBaseGameLogic(Hud __instance, Player player, float dt,
    VehicleShipCompat vehicleInterface)
  {
    var speedSetting = vehicleInterface.GetSpeedSetting();
    var rudder = vehicleInterface.GetRudder();
    var rudderValue = vehicleInterface.GetRudderValue();
    __instance.m_shipHudRoot.SetActive(value: true);
    __instance.m_rudderSlow.SetActive(speedSetting == Ship.Speed.Slow);
    __instance.m_rudderForward.SetActive(speedSetting == Ship.Speed.Half);
    __instance.m_rudderFastForward.SetActive(speedSetting == Ship.Speed.Full);
    __instance.m_rudderBackward.SetActive(speedSetting == Ship.Speed.Back);
    __instance.m_rudderLeft.SetActive(value: false);
    __instance.m_rudderRight.SetActive(value: false);
    __instance.m_fullSail.SetActive(speedSetting == Ship.Speed.Full);
    __instance.m_halfSail.SetActive(speedSetting == Ship.Speed.Half);


    var rudder2 = __instance.m_rudder;
    int active;
    switch (speedSetting)
    {
      case Ship.Speed.Stop:
        active = ((Mathf.Abs(rudderValue) > 0.2f) ? 1 : 0);
        break;
      default:
        active = 0;
        break;
      case Ship.Speed.Back:
      case Ship.Speed.Slow:
        active = 1;
        break;
    }

    rudder2.SetActive((byte)active != 0);
    if ((rudder > 0f && rudderValue < 1f) || (rudder < 0f && rudderValue > -1f))
    {
      __instance.m_shipRudderIcon.transform.Rotate(new Vector3(0f, 0f,
        200f * (0f - rudder) * dt));
    }

    if (Mathf.Abs(rudderValue) < 0.02f)
    {
      __instance.m_shipRudderIndicator.gameObject.SetActive(value: false);
    }
    else
    {
      __instance.m_shipRudderIndicator.gameObject.SetActive(value: true);
      if (rudderValue > 0f)
      {
        __instance.m_shipRudderIndicator.fillClockwise = true;
        __instance.m_shipRudderIndicator.fillAmount = rudderValue * 0.25f;
      }
      else
      {
        __instance.m_shipRudderIndicator.fillClockwise = false;
        __instance.m_shipRudderIndicator.fillAmount = (0f - rudderValue) * 0.25f;
      }
    }

    float shipYawAngle = vehicleInterface.GetShipYawAngle();
    __instance.m_shipWindIndicatorRoot.localRotation = Quaternion.Euler(0f, 0f, shipYawAngle);
    float windAngle = vehicleInterface.GetWindAngle();
    __instance.m_shipWindIconRoot.localRotation = Quaternion.Euler(0f, 0f, windAngle);
    float windAngleFactor = vehicleInterface.GetWindAngleFactor();
    __instance.m_shipWindIcon.color =
      Color.Lerp(new Color(0.2f, 0.2f, 0.2f, 1f), Color.white, windAngleFactor);
    Camera mainCamera = Utils.GetMainCamera();
    if (!(mainCamera == null))
    {
      __instance.m_shipControlsRoot.transform.position =
        mainCamera.WorldToScreenPointScaled(vehicleInterface.m_controlGuiPos.position);
    }
  }

  [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateShipHud))]
  [HarmonyPrefix]
  public static bool UpdateShipHud(Hud __instance, Player player, float dt)
  {
    var controlledShipObj = Player_Patch.HandleGetControlledShip(player);
    if (controlledShipObj == null)
    {
      __instance.m_shipHudRoot.gameObject.SetActive(value: false);
      return false;
    }

    var vehicleInterface = VehicleShipCompat.InitFromUnknown(controlledShipObj);

    if (vehicleInterface == null)
    {
      Logger.LogWarning(
        "ValhiemRaft skipping ship hud initialization and defaulting to base game as no VehicleShip or Ship detected");
      return true;
    }


    if (vehicleInterface.IsVehicleShip)
    {
      ToggleAnchorHud(vehicleInterface.VehicleShipInstance);
    }
    else if (vehicleInterface.IsMbRaft)
    {
      ToggleAnchorHud(vehicleInterface.ShipInstance);
    }
    else
    {
      if (AnchorHud)
      {
        AnchorHud.SetActive(false);
      }
    }

    Hud_UpdateShipBaseGameLogic(__instance, player, dt, vehicleInterface);

    return false;
  }
}