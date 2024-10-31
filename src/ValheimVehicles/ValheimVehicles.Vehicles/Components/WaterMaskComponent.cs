using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Serialization;
using ValheimVehicles.Config;
using ValheimVehicles.Prefabs;
using ValheimVehicles.LayerUtils;
using ValheimVehicles.Vehicles;
using Logger = Jotunn.Logger;

namespace ValheimVehicles.Vehicles.Components;

public class CreativeModeColliderComponent : MonoBehaviour
{
  public BoxCollider collider;
  private static List<CreativeModeColliderComponent> Instances = new();
  public static bool IsEditMode = false;

  internal void Awake()
  {
    if (ZNetView.m_forceDisableInit)
    {
      return;
    }

    Instances.Add(this);
    SetMode(IsEditMode);
    collider = GetComponent<BoxCollider>();
  }

  internal void OnDestroy()
  {
    Instances.Remove(this);
  }

  /// <summary>
  /// Enables the box collider which allows for editing the watermask, otherwise the user will not be able to interact with box/delete it.
  /// </summary>
  /// <param name="val"></param>
  public void SetMode(bool val)
  {
    if (collider == null) return;
    collider.excludeLayers = LayerHelpers.PhysicalLayers;
  }

  public virtual void OnToggleEditMode()
  {
  }

  public static void ToggleEditMode()
  {
    IsEditMode = !IsEditMode;
    foreach (var creativeModeColliderComponent in Instances)
    {
      creativeModeColliderComponent.SetMode(IsEditMode);
      creativeModeColliderComponent.OnToggleEditMode();
    }
  }
}

public class WaterMaskComponent : CreativeModeColliderComponent
{
  public static Material WaterMaskMaterial;
  public ZNetView? netView;
  private MeshRenderer _meshRenderer;
  public static List<WaterMaskComponent> Instances = new();
  public ScalableDoubleSidedCube DoubleSidedCube;

  public Vector3 DefaultSize = Vector3.one;

  void Awake()
  {
    _meshRenderer = GetComponent<MeshRenderer>();
  }

  private void Start()
  {
    if (ZNetView.m_forceDisableInit)
    {
      return;
    }

    Instances.Add(this);
    netView = GetComponent<ZNetView>();
    InitMaskFromNetview();
  }

  private new void OnDestroy()
  {
    base.OnDestroy();
    Instances.Remove(this);
  }

  public static void OnToggleEditMode(bool isDebug)
  {
    foreach (var waterMaskComponent in Instances)
    {
      if (isDebug)
      {
        waterMaskComponent.UseDebugMaterial();
      }
      else
      {
        waterMaskComponent.UseMaskMaterial();
      }
    }
  }

  /// <summary>
  /// Meant to show the mask in a "Debug mode" by swapping shaders
  /// Makes the component breakable
  /// </summary>
  public void UseDebugMaterial()
  {
    var material =
      new Material(LoadValheimVehicleAssets.WaterHeightMaterial)
      {
        color = new Color(0, 0.5f, 1f, 0.4f)
      };
    // VehiclePiecesController.FixMaterial(material);
    _meshRenderer.material = material;
    // _meshRenderer.rendererPriority = 3000;
    // can break, but can walk through.
    gameObject.layer = LayerHelpers.NonSolidLayer;
    DoubleSidedCube.CubeLayer = LayerHelpers.NonSolidLayer;
  }

  /// <summary>
  /// Makes the component untouchable.
  /// </summary>
  public void UseMaskMaterial()
  {
    gameObject.layer = LayerHelpers.IgnoreRaycastLayer;
    DoubleSidedCube.CubeLayer = LayerHelpers.IgnoreRaycastLayer;
    _meshRenderer.material = WaterMaskMaterial;
  }

  public void InitMaskFromNetview()
  {
    if (ZNetView.m_forceDisableInit || netView?.GetZDO() == null) return;
    DoubleSidedCube = gameObject.AddComponent<ScalableDoubleSidedCube>();

    if (IsEditMode)
    {
      UseDebugMaterial();
    }
    else
    {
      UseMaskMaterial();
    }

    var zdo = netView.GetZDO();
    if (zdo == null) return;
    var size = zdo.GetVec3(VehicleZdoVars.CustomMeshScale, DefaultSize);
    if (size == Vector3.zero)
    {
      return;
    }

    // likely not required
    DoubleSidedCube.UpdateScale(DefaultSize);
  }
}