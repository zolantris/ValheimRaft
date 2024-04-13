using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using ValheimRAFT;
using ValheimVehicles.Prefabs;
using ValheimVehicles.Prefabs.Registry;
using ValheimVehicles.Vehicles.Components;
using Logger = Jotunn.Logger;

namespace ValheimVehicles.Vehicles;

/*
 * Mostly vanilla Valheim However this is safe from other mods overriding valheim ships directly
 *
 * 2215240816:53310 53409
 */
public class VehicleShip : ValheimBaseGameShip, IVehicleShip
{
  public GameObject RudderObject { get; set; }

  public IWaterVehicleController Controller => _controller;

  public GameObject? previewComponent;
  public GameObject? VehiclePiecesContainer;

  public GameObject? waterEffects;

  private WaterVehicleController _controller;
  private GameObject _waterVehicle;
  public ZSyncTransform m_zsyncTransform;

  private GameObject _shipRotationObj;
  public Transform ShipDirectionTransform => _shipRotationObj.transform;

  public VehicleShip Instance => this;

  public BoxCollider FloatCollider
  {
    get => m_floatcollider;
    set => m_floatcollider = value;
  }

  public Transform ControlGuiPosition
  {
    get => m_controlGuiPos;
    set => m_controlGuiPos = value;
  }

  public void OnDestroy()
  {
    if (VehiclePiecesContainer)
    {
      _controller.CleanUp();
      Destroy(VehiclePiecesContainer);
    }

    if (_shipRotationObj)
    {
      Destroy(_shipRotationObj);
    }

    if (m_sailCloth)
    {
      Destroy(m_sailCloth);
    }

    if (m_sailObject)
    {
      Destroy(m_sailObject);
    }

    if (m_mastObject)
    {
      Destroy(m_mastObject);
    }
  }

  public double currentRotationOffset = 0;
  // private static readonly List<VVShip> s_currentShips = new();

  private static bool GetAnchorKey()
  {
    if (ValheimRaftPlugin.Instance.AnchorKeyboardShortcut.Value.ToString() != "False" &&
        ValheimRaftPlugin.Instance.AnchorKeyboardShortcut.Value.ToString() != "Not set")
    {
      var isLeftShiftDown = ZInput.GetButtonDown("LeftShift");
      var mainKeyString = ValheimRaftPlugin.Instance.AnchorKeyboardShortcut.Value.MainKey
        .ToString();
      var buttonDownDynamic =
        ZInput.GetButtonDown(mainKeyString);

      // Logger.LogDebug($"AnchorKey: leftShift {isLeftShiftDown}, mainKey: {mainKeyString}");
      // Logger.LogDebug(
      //   $"AnchorKey isDown: {ValheimRaftPlugin.Instance.AnchorKeyboardShortcut.Value.IsDown()}");
      return buttonDownDynamic || isLeftShiftDown ||
             ValheimRaftPlugin.Instance.AnchorKeyboardShortcut.Value.IsDown();
    }

    var isPressingRun = ZInput.GetButtonDown("Run") || ZInput.GetButtonDown("JoyRun");
    var isPressingJoyRun = ZInput.GetButtonDown("JoyRun");

    // Logger.LogDebug(
    //   $"AnchorKey isPressingRun: {isPressingRun},isPressingJoyRun {isPressingJoyRun} ");

    return isPressingRun || isPressingJoyRun;
  }

  private void Update()
  {
    if (!GetAnchorKey()) return;
    Logger.LogDebug("Anchor Keydown is pressed");

    var flag = HaveControllingPlayer();
    if (flag && Player.m_localPlayer.IsAttached() && Player.m_localPlayer.m_attachPoint &&
        Player.m_localPlayer.m_doodadController != null)
    {
      Logger.LogDebug("toggling vehicleShip anchor");
      _controller.ToggleAnchor();
    }
    else
    {
      Logger.LogDebug("Player not controlling ship, skipping");
    }
  }

  private void SetupShipComponents()
  {
    m_mastObject = new GameObject()
    {
      name = PrefabNames.VehicleSailMast,
    };
    m_sailObject = new GameObject()
    {
      name = PrefabNames.VehicleSail,
    };

    _shipRotationObj = new GameObject
    {
      name = "VehicleShip_transform"
    };
    m_sailCloth = m_sailObject.AddComponent<Cloth>();
    m_sailCloth.name = PrefabNames.VehicleSailCloth;
  }

  GUIStyle myButtonStyle;


  private void OnGUI()
  {
    if (myButtonStyle == null)
    {
      myButtonStyle = new GUIStyle(GUI.skin.button);
      myButtonStyle.fontSize = 50;
    }

    GUILayout.BeginArea(new Rect(300, 10, 150, 150), myButtonStyle);
    if (GUILayout.Button("Update collider draw"))
    {
      DrawColliders();
    }

    if (GUILayout.Button("rotate90 ship"))
    {
      if (currentRotationOffset > 360)
      {
        currentRotationOffset = 0;
      }
      else
      {
        currentRotationOffset += 90;
      }

      _shipRotationObj.transform.Rotate(0, 90, 0);

      // Instance.transform.rotation = new Quaternion(Instance.transform.rotation.x,
      //   Instance.transform.rotation.y + 90, Instance.transform.rotation.z,
      //   Instance.transform.rotation.w);
    }

    if (GUILayout.Button("rotate based on rudder dir"))
    {
      if (_controller.m_rudderPieces.Count > 0)
      {
        var rudderPiece = _controller.m_rudderPieces.First();
        if (rudderPiece.transform.localRotation != _shipRotationObj.transform.rotation)
        {
          _shipRotationObj.transform.localRotation = rudderPiece.transform.localRotation;
        }
      }
      // Instance.transform.rotation = new Quaternion(Instance.transform.rotation.x,
      //   Instance.transform.rotation.y + 90, Instance.transform.rotation.z,
      //   Instance.transform.rotation.w);
      // _controller.transform.rotation = new Quaternion(_controller.transform.rotation.x,
      //   _controller.transform.rotation.y - 90, _controller.transform.rotation.z,
      //   _controller.transform.rotation.w);
    }

    if (GUILayout.Button("rotate based on steering"))
    {
      if (_controller.m_rudderWheelPieces.Count > 0)
      {
        var wheelPiece = _controller.m_rudderWheelPieces.First();
        _shipRotationObj.transform.localRotation = wheelPiece.transform.localRotation;
      }

      // FloatCollider.transform.Rotate(0, currentRotationOffset, 0);
      // _controller.transform.SetParent(null);
      // Instance.transform.rotation = new Quaternion(Instance.transform.rotation.x,
      //   Instance.transform.rotation.y + 90, Instance.transform.rotation.z,
      //   Instance.transform.rotation.w);
      // _controller.transform.SetParent(transform);
    }

    GUILayout.EndArea();
  }

  private List<LineRenderer> lines = new List<LineRenderer>();

  private void DrawLine(Vector3 start, Vector3 end, Color color, Material material,
    float width = 0.01f)
  {
    LineRenderer line = new GameObject("Line_" + start.ToString() + "_" + end.ToString())
      .AddComponent<LineRenderer>();
    line.material = material;
    line.startColor = color;
    line.endColor = color;
    line.startWidth = width;
    line.endWidth = width;
    line.positionCount = 2;
    line.useWorldSpace = true;
    line.SetPosition(0, start);
    line.SetPosition(1, end);
    line.transform.SetParent(transform);
    lines.Add(line);
  }

  public void SetLinesColor(Color color)
  {
    for (int i = 0; i < lines.Count; i++)
    {
      lines[i].material.color = color;
      lines[i].startColor = color;
      lines[i].endColor = color;
    }
  }

  private void DrawColliders()
  {
    BoxCollider boxCollider = GetComponent<BoxCollider>();
    if (boxCollider != null)
    {
      Material material = new Material(Shader.Find("Unlit/Color"));
      Color color = Color.green;
      material.color = color;
      float width = 0.01f;
      Vector3 rightDir = boxCollider.transform.right.normalized;
      Vector3 forwardDir = boxCollider.transform.forward.normalized;
      Vector3 upDir = boxCollider.transform.up.normalized;
      Vector3 center = boxCollider.transform.position + boxCollider.center;
      Vector3 size = boxCollider.size;
      size.x *= boxCollider.transform.lossyScale.x;
      size.y *= boxCollider.transform.lossyScale.y;
      size.z *= boxCollider.transform.lossyScale.z;
      DrawLine(center + upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center + upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center - upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center - upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center - upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center - upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f,
        center + upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center - upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f,
        center - upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f,
        center - upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f,
        center - upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center + upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center - upDir * size.y / 2f + rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center - upDir * size.y / 2f + rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center + upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center + upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
      DrawLine(center - upDir * size.y / 2f - rightDir * size.x / 2f + forwardDir * size.z / 2f,
        center - upDir * size.y / 2f - rightDir * size.x / 2f - forwardDir * size.z / 2f, color,
        material, width);
    }
  }

  private new void Awake()
  {
    SetupShipComponents();

    base.Awake();

    if (m_floatcollider)
    {
      _shipRotationObj.transform.position = m_floatcollider.transform.position;
      _shipRotationObj.transform.SetParent(transform);
    }

    Logger.LogDebug($"called Awake in VVShip, m_body {m_body}");
    if (!m_nview)
    {
      m_nview = GetComponent<ZNetView>();
    }

    // should already exist
    // if (!(bool)m_zsyncTransform)
    // {
    //   m_zsyncTransform = GetComponent<ZSyncTransform>();
    // }
    //
    // // should exist
    // if (!(bool)m_zsyncTransform.m_body)
    // {
    //   m_zsyncTransform.m_body = m_body;
    // }

    InitializeWaterVehicleController();
  }

  public override void OnEnable()
  {
    base.OnEnable();
    InitializeWaterVehicleController();
  }

  public void FixedUpdate()
  {
    if (!(bool)_controller || !(bool)m_body || !(bool)m_floatcollider)
    {
      return;
    }


    // todo remove this if unnecessary
    ShipDirectionTransform.transform.position = m_floatcollider.transform.position;

    TestFixedUpdate();
    // ValheimRaftCustomFixedUpdate();
  }

  private void InitHull()
  {
    var pieceCount = _controller.GetPieceCount();
    if (pieceCount != 0 || !_controller.m_nview)
    {
      return;
    }

    if (_controller.BaseVehicleInitState != BaseVehicleController.InitializationState.Created)
    {
      return;
    }

    var prefab = PrefabManager.Instance.GetPrefab(PrefabNames.ShipHullPrefabName);
    if (!prefab) return;

    var hull = Instantiate(prefab, transform.position, transform.rotation);
    if (hull == null) return;

    var hullNetView = hull.GetComponent<ZNetView>();
    _controller.AddNewPiece(hullNetView);

    // todo This logic is unnecessary as InitPiece is called from zdo initialization of the PlaceholderItem
    //
    // var placeholderInstance = buildGhostInstance.GetPlaceholderInstance();
    // if (placeholderInstance == null) return;
    //
    // var hullNetView = placeholderInstance.GetComponent<ZNetView>();
    // hullNetView.transform.SetParent(null);
    //
    // AddNewPiece(hullNetView);
    // buildGhostInstance.DisableVehicleGhost();
    /*
     * @todo turn the original planks into a Prefab so boat floors can be larger
     */
    GameObject floor = ZNetScene.instance.GetPrefab("wood_floor");
    for (float x = -1f; x < 1.01f; x += 2f)
    {
      for (float z = -2f; z < 2.01f; z += 2f)
      {
        Vector3 pt = base.transform.TransformPoint(new Vector3(x,
          ValheimRaftPlugin.Instance.InitialRaftFloorHeight.Value, z));
        var obj = Instantiate(floor, pt, transform.rotation);
        ZNetView netview = obj.GetComponent<ZNetView>();
        _controller.AddNewPiece(netview);
      }
    }

    _controller.SetInitComplete();
  }

  /*
   * Only initializes the controller if the prefab is enabled (when zdo is initialized this happens)
   */
  private void InitializeWaterVehicleController()
  {
    if (!(bool)m_nview || m_nview.GetZDO() == null || m_nview.m_ghost || (bool)_controller) return;

    enabled = true;

    var ladders = GetComponentsInChildren<Ladder>();
    for (var i = 0; i < ladders.Length; i++) ladders[i].m_useDistance = 10f;

    VehiclePiecesContainer = Instantiate(WaterVehiclePrefab.GetVehiclePieces, transform);

    if (!VehiclePiecesContainer)
    {
      Logger.LogError("No vehicle pieces container, will not initialize vehicle without it");
      return;
    }

    _controller = VehiclePiecesContainer.AddComponent<WaterVehicleController>();
    _controller.InitializeShipValues(Instance);
    m_mastObject.transform.SetParent(_controller.transform);
    m_sailObject.transform.SetParent(_controller.transform);
    InitHull();
  }

  /**
   * TODO this could be set to false for the ship as an override to allow the ship to never remove itself
   */
  // public bool CanBeRemoved()
  // {
  //   return m_players.Count == 0;
  // }
  private static Vector3 CalculateAnchorStopVelocity(Vector3 currentVelocity)
  {
    var zeroVelocity = Vector3.zero;
    return Vector3.SmoothDamp(currentVelocity * 0.5f, Vector3.zero, ref zeroVelocity, 5f);
  }

  public Vector3 GetDirectionForce()
  {
    // Zero would would be +1 and 180 would be -1
    var vectorX = (float)Math.Cos(currentRotationOffset);
    // VectorZ is going to be 0 force at 0 and 1 at 
    var vectorZ = (float)Math.Sin(currentRotationOffset);

    /*
     * Computed sailSpeed based on the rudder settings.
     */
    switch (m_speed)
    {
      case Speed.Full:
        vectorX *= 0.4f;
        vectorZ *= 0.4f;
        break;
      case Speed.Half:
        vectorX *= 0.25f;
        vectorZ *= 0.25f;
        break;
      case Speed.Slow:
        // sailArea = Math.Min(0.1f, sailArea * 0.1f);
        vectorX *= 0.1f;
        vectorZ *= 0.1f;
        break;
      case Speed.Stop:
      case Speed.Back:
      default:
        vectorX *= 0f;
        vectorZ *= 0f;
        break;
    }

    var shipDirectionForce = new Vector3(vectorX, 0, vectorZ);
    return shipDirectionForce;
  }

  public void AddForceAtPosition(Vector3 force, Vector3 position,
    ForceMode forceMode)
  {
    // var directionForceMult = GetDirectionForce();
    // var newForce = new Vector3(directionForceMult.x * force.x, force.y,
    //   directionForceMult.z * force.z);
    m_body.AddForceAtPosition(force, position, forceMode);
  }

  /**
   * BasedOnInternalRotation
   */
  private float GetFloatSizeFromDirection()
  {
    // either 90 or 270 degress so Sin 90 or Sin 270
    if (Mathf.Abs((int)Mathf.Sin(_shipRotationObj.transform.localEulerAngles.y)) == 1)
    {
      return m_floatcollider.size.x;
    }

    return m_floatcollider.size.z;
  }

  public void TestFixedUpdate()
  {
    if (!(bool)_controller || !(bool)m_nview || m_nview.m_zdo == null) return;

    /*
     * creative mode should not allows movement and applying force on a object will cause errors when the object is kinematic
     */
    if (_controller.isCreative)
    {
      return;
    }

    // This could be the spot that causes the raft to fly at spawn
    _controller.m_targetHeight =
      m_nview.m_zdo.GetFloat("MBTargetHeight", _controller.m_targetHeight);
    _controller.VehicleFlags =
      (WaterVehicleFlags)m_nview.m_zdo.GetInt("MBFlags",
        (int)_controller.VehicleFlags);

    // This could be the spot that causes the raft to fly at spawn
    _controller.m_zsync.m_useGravity =
      _controller.m_targetHeight == 0f;

    var flag = HaveControllingPlayer();

    UpdateControls(Time.fixedDeltaTime);
    UpdateSail(Time.fixedDeltaTime);
    SyncVehicleMastsAndSails();
    UpdateRudder(Time.fixedDeltaTime, flag);
    if (m_players.Count == 0 ||
        _controller.VehicleFlags.HasFlag(WaterVehicleFlags
          .IsAnchored))
    {
      m_speed = Speed.Stop;
      m_rudderValue = 0f;
      if (!_controller.VehicleFlags.HasFlag(
            WaterVehicleFlags.IsAnchored))
      {
        _controller.VehicleFlags |=
          WaterVehicleFlags.IsAnchored;
        m_nview.m_zdo.Set("MBFlags", (int)_controller.VehicleFlags);
      }
    }

    if ((bool)m_nview && !m_nview.IsOwner()) return;

    // don't damage the ship lol
    // UpdateUpsideDmg(Time.fixedDeltaTime);

    if (!flag && (m_speed == Speed.Slow || m_speed == Speed.Back))
      m_speed = Speed.Stop;
    var worldCenterOfMass = m_body.worldCenterOfMass;
    var vector = ShipDirectionTransform.position +
                 ShipDirectionTransform.forward * GetFloatSizeFromDirection() /
                 2f;
    var vector2 = ShipDirectionTransform.position -
                  ShipDirectionTransform.forward * GetFloatSizeFromDirection() /
                  2f;
    var vector3 = ShipDirectionTransform.position -
                  ShipDirectionTransform.right * m_floatcollider.size.x /
                  2f;
    var vector4 = ShipDirectionTransform.position +
                  ShipDirectionTransform.right * m_floatcollider.size.x /
                  2f;
    var waterLevel = Floating.GetWaterLevel(worldCenterOfMass, ref m_previousCenter);
    var waterLevel2 = Floating.GetWaterLevel(vector3, ref m_previousLeft);
    var waterLevel3 = Floating.GetWaterLevel(vector4, ref m_previousRight);
    var waterLevel4 = Floating.GetWaterLevel(vector, ref m_previousForward);
    var waterLevel5 = Floating.GetWaterLevel(vector2, ref m_previousBack);
    var averageWaterHeight =
      (waterLevel + waterLevel2 + waterLevel3 + waterLevel4 + waterLevel5) / 5f;
    var currentDepth = worldCenterOfMass.y - averageWaterHeight - m_waterLevelOffset;
    if (!(currentDepth > m_disableLevel))
    {
      _controller.UpdateStats(false);
      m_body.WakeUp();
      UpdateWaterForce(currentDepth, Time.fixedDeltaTime);
      var vector5 = new Vector3(vector3.x, waterLevel2, vector3.z);
      var vector6 = new Vector3(vector4.x, waterLevel3, vector4.z);
      var vector7 = new Vector3(vector.x, waterLevel4, vector.z);
      var vector8 = new Vector3(vector2.x, waterLevel5, vector2.z);
      var fixedDeltaTime = Time.fixedDeltaTime;
      var deltaForceMultiplier = fixedDeltaTime * 50f;

      var currentDepthForceMultiplier = Mathf.Clamp01(Mathf.Abs(currentDepth) / m_forceDistance);
      var upwardForceVector = Vector3.up * m_force * currentDepthForceMultiplier;

      AddForceAtPosition(upwardForceVector * deltaForceMultiplier, worldCenterOfMass,
        ForceMode.VelocityChange);

      var num5 = Vector3.Dot(m_body.velocity, transform.forward);
      var num6 = Vector3.Dot(m_body.velocity, transform.right);
      var velocity = m_body.velocity;
      var value = velocity.y * velocity.y * Mathf.Sign(velocity.y) * m_damping *
                  currentDepthForceMultiplier;
      var value2 = num5 * num5 * Mathf.Sign(num5) * m_dampingForward * currentDepthForceMultiplier;
      var value3 = num6 * num6 * Mathf.Sign(num6) * m_dampingSideway * currentDepthForceMultiplier;

      velocity.y -= Mathf.Clamp(value, -1f, 1f);
      velocity -= transform.forward * Mathf.Clamp(value2, -1f, 1f);
      velocity -= transform.right * Mathf.Clamp(value3, -1f, 1f);

      if (velocity.magnitude > m_body.velocity.magnitude)
        velocity = velocity.normalized * m_body.velocity.magnitude;

      if (m_players.Count == 0 ||
          _controller.VehicleFlags.HasFlag(WaterVehicleFlags.IsAnchored))
      {
        var anchoredVelocity = CalculateAnchorStopVelocity(velocity);
        velocity = anchoredVelocity;
      }

      m_body.velocity = velocity;
      m_body.angularVelocity -=
        m_body.angularVelocity * m_angularDamping * currentDepthForceMultiplier;

      var num7 = 0.15f;
      var num8 = 0.5f;
      var f = Mathf.Clamp((vector7.y - vector.y) * num7, 0f - num8, num8);
      var f2 = Mathf.Clamp((vector8.y - vector2.y) * num7, 0f - num8, num8);
      var f3 = Mathf.Clamp((vector5.y - vector3.y) * num7, 0f - num8, num8);
      var f4 = Mathf.Clamp((vector6.y - vector4.y) * num7, 0f - num8, num8);
      f = Mathf.Sign(f) * Mathf.Abs(Mathf.Pow(f, 2f));
      f2 = Mathf.Sign(f2) * Mathf.Abs(Mathf.Pow(f2, 2f));
      f3 = Mathf.Sign(f3) * Mathf.Abs(Mathf.Pow(f3, 2f));
      f4 = Mathf.Sign(f4) * Mathf.Abs(Mathf.Pow(f4, 2f));

      AddForceAtPosition(Vector3.up * f * deltaForceMultiplier, vector, ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * f2 * deltaForceMultiplier, vector2,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * f3 * deltaForceMultiplier, vector3,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * f4 * deltaForceMultiplier, vector4,
        ForceMode.VelocityChange);

      ApplySailForce(this, num5);
      ApplyEdgeForce(Time.fixedDeltaTime);
      if (_controller.m_targetHeight > 0f)
      {
        var centerpos = ShipDirectionTransform.position;
        var centerforce = GetUpwardsForce(_controller.m_targetHeight,
          centerpos.y + m_body.velocity.y, _controller.m_liftForce);
        AddForceAtPosition(Vector3.up * centerforce, centerpos,
          ForceMode.VelocityChange);
      }
    }
    else if (_controller.m_targetHeight > 0f)
    {
      if (m_players.Count == 0 ||
          _controller.VehicleFlags.HasFlag(WaterVehicleFlags.IsAnchored))
      {
        var anchoredVelocity = CalculateAnchorStopVelocity(m_body.velocity);
        m_body.velocity = anchoredVelocity;
      }

      _controller.UpdateStats(true);
      var side1 = ShipDirectionTransform.position +
                  ShipDirectionTransform.forward * GetFloatSizeFromDirection() /
                  2f;
      var side2 = ShipDirectionTransform.position -
                  ShipDirectionTransform.forward * GetFloatSizeFromDirection() /
                  2f;
      var side3 = ShipDirectionTransform.position -
                  ShipDirectionTransform.right * m_floatcollider.size.x /
                  2f;
      var side4 = ShipDirectionTransform.position +
                  ShipDirectionTransform.right * m_floatcollider.size.x /
                  2f;
      var centerpos2 = ShipDirectionTransform.position;
      var corner1curforce = m_body.GetPointVelocity(side1);
      var corner2curforce = m_body.GetPointVelocity(side2);
      var corner3curforce = m_body.GetPointVelocity(side3);
      var corner4curforce = m_body.GetPointVelocity(side4);
      var side1force =
        GetUpwardsForce(_controller.m_targetHeight,
          side1.y + corner1curforce.y,
          _controller.m_balanceForce);
      var side2force =
        GetUpwardsForce(_controller.m_targetHeight,
          side2.y + corner2curforce.y,
          _controller.m_balanceForce);
      var side3force =
        GetUpwardsForce(_controller.m_targetHeight,
          side3.y + corner3curforce.y,
          _controller.m_balanceForce);
      var side4force =
        GetUpwardsForce(_controller.m_targetHeight,
          side4.y + corner4curforce.y,
          _controller.m_balanceForce);
      var centerforce2 = GetUpwardsForce(_controller.m_targetHeight,
        centerpos2.y + m_body.velocity.y, _controller.m_liftForce);

      /**
       * applies only center force to keep boat stable and not flip
       */
      // AddForceAtPosition(Vector3.up * centerforce2, side1,
      //   ForceMode.VelocityChange);
      // AddForceAtPosition(Vector3.up * centerforce2, side2,
      //   ForceMode.VelocityChange);
      // AddForceAtPosition(Vector3.up * centerforce2, side3,
      //   ForceMode.VelocityChange);
      // AddForceAtPosition(Vector3.up * centerforce2, side4,
      //   ForceMode.VelocityChange);
      // AddForceAtPosition(Vector3.up * centerforce2, centerpos2,
      //   ForceMode.VelocityChange);


      AddForceAtPosition(Vector3.up * side1force, side1,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * side2force, side2,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * side3force, side3,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * side4force, side4,
        ForceMode.VelocityChange);
      AddForceAtPosition(Vector3.up * centerforce2, centerpos2,
        ForceMode.VelocityChange);

      var dir = Vector3.Dot(m_body.velocity, transform.forward);
      ApplySailForce(this, dir);
    }
  }

  public new void UpdateSail(float deltaTime)
  {
    base.UpdateSail(deltaTime);
  }

  /**
   * In theory we can just make the sailComponent and mastComponent parents of the masts/sails of the ship. This will make any mutations to those parents in sync with the sail changes
   */
  private void SyncVehicleMastsAndSails()
  {
    if (!(bool)_controller) return;

    foreach (var mast in _controller.m_mastPieces.ToList())
    {
      if (!(bool)mast)
      {
        _controller.m_mastPieces.Remove(mast);
      }
      else if (mast.m_allowSailShrinking)
      {
        if (mast.m_sailObject.transform.localScale != m_sailObject.transform.localScale)
          mast.m_sailCloth.enabled = false;
        mast.m_sailObject.transform.localScale = m_sailObject.transform.localScale;
        mast.m_sailCloth.enabled = m_sailCloth.enabled;
      }
      else
      {
        mast.m_sailObject.transform.localScale = Vector3.one;
        mast.m_sailCloth.enabled = !mast.m_disableCloth;
      }
    }

    foreach (var rudder in _controller.m_rudderPieces.ToList())
    {
      if (!(bool)rudder)
      {
        _controller.m_rudderPieces.Remove(rudder);
        continue;
      }

      if (!rudder.PivotPoint)
      {
        Logger.LogError("No pivot point detected for rudder");
        continue;
      }

      var newRotation = Quaternion.Slerp(
        rudder.PivotPoint.localRotation,
        Quaternion.Euler(0f, m_rudderRotationMax * (0f - m_rudderValue) * 2, 0f), 0.5f);
      rudder.PivotPoint.localRotation = newRotation;
    }

    foreach (var wheel in _controller.m_rudderWheelPieces.ToList())
    {
      if (!(bool)wheel)
      {
        _controller.m_rudderWheelPieces.Remove(wheel);
      }
      else if ((bool)wheel.wheelTransform)
      {
        wheel.wheelTransform.localRotation = Quaternion.Slerp(
          wheel.wheelTransform.localRotation,
          Quaternion.Euler(
            m_rudderRotationMax * (0f - m_rudderValue) *
            wheel.m_wheelRotationFactor, 0f, 0f), 0.5f);
      }
    }
  }

  internal Vector3 GetSailForce(float sailSize, float dt)
  {
    Vector3 windDir = EnvMan.instance.GetWindDir();
    float windIntensity = EnvMan.instance.GetWindIntensity();
    float num = Mathf.Lerp(0.25f, 1f, windIntensity);
    float windAngleFactor = GetWindAngleFactor();
    windAngleFactor *= num;
    Vector3 target = Vector3.Normalize(windDir + ShipDirectionTransform.forward) * windAngleFactor *
                     m_sailForceFactor * sailSize;
    m_sailForce = Vector3.SmoothDamp(m_sailForce, target, ref m_windChangeVelocity, 1f, 99f);
    return m_sailForce;

    // for testing rotation
    // var unchangedWindForce = new Vector3(-0.0120766945f, -0.00563957961f, 0.0823633149f);
    // return unchangedWindForce;
  }

  public float GetWindAngleFactor()
  {
    float num = Vector3.Dot(EnvMan.instance.GetWindDir(), -ShipDirectionTransform.forward);
    float num2 = Mathf.Lerp(0.7f, 1f, 1f - Mathf.Abs(num));
    float num3 = 1f - Utils.LerpStep(0.75f, 0.8f, num);
    return num2 * num3;
  }

  private static void ApplySailForce(VehicleShip instance, float num5)
  {
    var sailArea = 0f;

    if ((bool)instance._controller)
    {
      sailArea = instance._controller.GetSailingForce();
    }

    /*
     * Computed sailSpeed based on the rudder settings.
     */
    switch (instance.m_speed)
    {
      case Speed.Full:
        break;
      case Speed.Half:
        sailArea *= 0.5f;
        break;
      case Speed.Slow:
        // sailArea = Math.Min(0.1f, sailArea * 0.1f);
        // sailArea = 0.1f;
        sailArea = 0;
        break;
      case Speed.Stop:
      case Speed.Back:
      default:
        sailArea = 0f;
        break;
    }

    if (instance._controller.VehicleFlags.HasFlag(WaterVehicleFlags.IsAnchored))
    {
      sailArea = 0f;
    }

    var sailForce = instance.GetSailForce(sailArea, Time.fixedDeltaTime);

    var position = instance.m_body.worldCenterOfMass;


    //  * Math.Max(0.5f, ValheimRaftPlugin.Instance.RaftSailForceMultiplier.Value)
    // set the speed, this may need to be converted to a vector for the multiplier
    instance.AddForceAtPosition(
      sailForce,
      position,
      ForceMode.VelocityChange);


    // steer offset will need to be size x or size z depending on location of rotation.
    var stearoffset = instance.ShipDirectionTransform.position -
                      instance.ShipDirectionTransform.forward *
                      instance.GetFloatSizeFromDirection() / 2f;
    var num7 = num5 * instance.m_stearVelForceFactor;
    instance.AddForceAtPosition(
      instance.ShipDirectionTransform.right * num7 * (0f - instance.m_rudderValue) *
      Time.fixedDeltaTime,
      stearoffset, ForceMode.VelocityChange);
    var stearforce = Vector3.zero;
    switch (instance.m_speed)
    {
      case Speed.Slow:
        stearforce += instance.ShipDirectionTransform.forward * instance.m_backwardForce *
                      (1f - Mathf.Abs(instance.m_rudderValue));
        break;
      case Speed.Back:
        stearforce += -instance.ShipDirectionTransform.forward * instance.m_backwardForce *
                      (1f - Mathf.Abs(instance.m_rudderValue));
        break;
    }

    if (instance.m_speed == Speed.Back || instance.m_speed == Speed.Slow)
    {
      float num6 = instance.m_speed != Speed.Back ? 1 : -1;
      stearforce += instance.ShipDirectionTransform.right * instance.m_stearForce *
                    (0f - instance.m_rudderValue) * num6;
    }

    instance.AddForceAtPosition(stearforce * Time.fixedDeltaTime, stearoffset,
      ForceMode.VelocityChange);
  }
}