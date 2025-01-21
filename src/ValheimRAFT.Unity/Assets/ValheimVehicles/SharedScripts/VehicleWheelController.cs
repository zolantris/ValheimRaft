﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimVehicles.SharedScripts
{
  /// <summary>
  ///   Meant to replace ValheimVehicle.Component.MovementController for integration
  ///   within unity.
  ///   - This will only interface with built-in unity project values.
  ///   MovementController from ValheimVehicles will then override properties and/or
  ///   set defaults.
  /// </summary>
  [RequireComponent(typeof(Rigidbody))]
  public class VehicleWheelController : MonoBehaviour
  {
    public enum SteeringType
    {
      Differential,
      Magic,
      FourWheel
    }

    [Tooltip("Top speed of the tank in m/s.")]
    public float topSpeed = 10.0f;

    [Tooltip(
      "For tanks with front/rear wheels defined, this is how far those wheels turn.")]
    public float steeringAngle = 30.0f;

    [Tooltip("Power of any wheel listed under powered wheels.")]
    public float motorTorque = 10.0f;

    [Tooltip(
      "Turn rate that is \"magically\" applied regardless of what the physics state of the tank is.")]
    public float magicTurnRate = 45.0f;

    [Tooltip(
      "Assign this to override the center of mass. This can be useful to make the tank more stable and prevent it from flipping over. \n\nNOTE: THIS TRANSFORM MUST BE A CHILD OF THE ROOT TANK OBJECT.")]
    public Transform centerOfMass;

    [Tooltip(
      "This prefab will be instantiated as a child of each wheel object and mimic the position/rotation of that wheel. If the prefab has a diameter of 1m, it will scale correct to match the wheel radius.")]
    public Transform wheelModelPrefab;

    [Tooltip(
      "Front wheels used for steering by rotating the wheels left/right.")]
    public List<WheelCollider> front;

    [Tooltip("Rear wheels for steering by rotating the wheels left/right.")]
    public List<WheelCollider> rear;

    [Tooltip("Wheels that provide power and move the tank forwards/reverse.")]
    public List<WheelCollider> poweredWheels;

    [Tooltip(
      "Wheels on the left side of the tank that are used for differential steering.")]
    public List<WheelCollider> left;

    [Tooltip(
      "Wheels on the right side of the tank that are used for differential steering.")]
    public List<WheelCollider> right;

    public SteeringType m_steeringType = SteeringType.Differential;

    [Header("Wheel Settings")]
    public Transform boundsTransform; // Reference for bounds

    public Transform forwardDirection; // Dynamic rotation reference

    public GameObject wheelSetPrefab; // Prefab for a single wheel set
    public int minimumWheelSets = 3; // Minimum number of wheel sets
    public float axelPadding = 0.5f; // Extra length on both sides of axel

    public float
      vehicleSizeThresholdFor5thSet = 18f; // Threshold for adding 5th set

    public float
      vehicleSizeThresholdFor6thSet = 30f; // Threshold for adding 6th set

    private readonly List<GameObject> wheelSets = new();

    private float forwardInput, turnInput;

    private Rigidbody rigid;
    private List<WheelCollider> wheelColliders = new();

    private Transform wheelParent;

    // Used to associate a wheel with a one of the model prefabs.
    private Dictionary<WheelCollider, Transform> WheelToTransformMap = new();

    private void Awake()
    {
      wheelParent = transform.Find("wheels");
      rigid = GetComponent<Rigidbody>();
    }

    private void Start()
    {
      GenerateWheelSets();
      // Override center of mass when a reference is passed in.
      if (centerOfMass != null)
      {
        if (centerOfMass.parent == transform)
          rigid.centerOfMass = centerOfMass.localPosition;
        else
          Debug.LogWarning(name +
                           ": PhysicsTank cannot override center of mass when " +
                           centerOfMass.name + " is not a child of " +
                           transform.name);
      }
    }

    private void Update()
    {
      // Capture input in the Update, not the FixedUpdate!
      forwardInput = Input.GetAxis("Vertical");
      turnInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
      // RunPoweredWheels();
      // RunSteering();
    }

    private void OnDestroy()
    {
      if (WheelToTransformMap != null) WheelToTransformMap.Clear();
      wheelColliders.Clear();
      right.Clear();
      left.Clear();
      rear.Clear();
      front.Clear();
    }

    private void SetupWheels()
    {
      wheelColliders = wheelParent.GetComponentsInChildren<WheelCollider>()
        .ToList();

      foreach (var wheelCollider in wheelColliders)
      {
        if (wheelCollider.name.StartsWith("Front"))
          front.Add(wheelCollider);

        if (wheelCollider.name.StartsWith("Rear"))
          rear.Add(wheelCollider);

        if (wheelCollider.name.Contains("FrontR") ||
            wheelCollider.name.Contains("LeftR") ||
            wheelCollider.name.Contains("RearR") ||
            wheelCollider.name.Contains("MidR") ||
            wheelCollider.name.Contains("right"))
          right.Add(wheelCollider);

        if (wheelCollider.name.Contains("FrontL") ||
            wheelCollider.name.Contains("LeftL") ||
            wheelCollider.name.Contains("RearL") ||
            wheelCollider.name.Contains("MidL") ||
            wheelCollider.name.Contains("left"))
          left.Add(wheelCollider);
      }

      poweredWheels = wheelColliders;
    }

    public bool IsXBoundsAlignment()
    {
      // Determine the forward direction (X or Z axis) based on ForwardDirection rotation
      var forwardAngle =
        Mathf.Round(forwardDirection.eulerAngles.y / 90f) * 90f;
      var isXBounds = Mathf.Approximately(Mathf.Abs(forwardAngle) % 180, 90);
      return isXBounds;
    }

    private void GenerateWheelSets()
    {
      if (!boundsTransform || !wheelSetPrefab || !forwardDirection)
      {
        Debug.LogError(
          "Bounds Transform, Forward Direction, and Wheel Set Prefab must be assigned.");
        return;
      }

      Physics.SyncTransforms();
      var bounds = GetBounds(boundsTransform);
      var isXBounds = IsXBoundsAlignment();
      var totalWheelSets = CalculateTotalWheelSets(bounds, isXBounds);

      // Clear any existing wheel sets
      foreach (var set in wheelSets) Destroy(set);
      wheelSets.Clear();

      var spacing = (isXBounds ? bounds.size.x : bounds.size.z) /
                    (totalWheelSets - 1);

      // Generate wheel sets dynamically
      for (var i = 0; i < totalWheelSets; i++)
      {
        var position =
          CalculateWheelSetPosition(i, totalWheelSets, bounds, spacing,
            isXBounds);
        var wheelSet = Instantiate(wheelSetPrefab, position,
          forwardDirection.rotation,
          wheelParent);
        AdjustWheelSet(wheelSet, bounds, isXBounds);
        wheelSets.Add(wheelSet);
      }

      if (isXBounds) wheelParent.position += new Vector3(spacing, 0, 0);
      // if (isXBounds && totalWheelSets > 5) wheelParent.position += new Vector3(-2.5f, 0, 0);
      // wheelParent.transform.position += deltaWheelParentToBounds;
    }

    private Bounds GetBounds(Transform boundsTransform)
    {
      var renderer = boundsTransform.GetComponent<Renderer>();
      if (renderer) return renderer.bounds;

      var collider = boundsTransform.GetComponent<Collider>();
      if (collider) return collider.bounds;

      Debug.LogError(
        "Bounds Transform must have a Renderer or Collider component.");
      return new Bounds(Vector3.zero, Vector3.zero);
    }

    private int CalculateTotalWheelSets(Bounds bounds, bool isXBounds)
    {
      var vehicleSize =
        isXBounds
          ? bounds.size.x
          : bounds.size.z; // Assuming size along the Z-axis determines length

      if (vehicleSize >= vehicleSizeThresholdFor6thSet) return 6;

      if (vehicleSize >= vehicleSizeThresholdFor5thSet) return 5;

      return minimumWheelSets;
    }

    private Vector3 CalculateWheelSetPosition(int index, int totalWheelSets,
      Bounds bounds, float spacing, bool isXBounds)
    {
      var xPosition =
        isXBounds ? bounds.min.x + spacing * index : bounds.center.x;

      // if (isXBounds) xPosition += bounds.extents.x / totalWheelSets;

      var zPosition =
        isXBounds ? bounds.center.z : bounds.min.z + spacing * index;

      return new Vector3(xPosition, bounds.min.y, zPosition);
    }

    private void AdjustWheelSet(GameObject wheelSet, Bounds bounds,
      bool isXBoundsAligned)
    {
      var wheelAxel = wheelSet.transform.Find("wheel_axel");
      var wheelLeft = wheelSet.transform.Find("wheel_left");
      var wheelRight = wheelSet.transform.Find("wheel_right");
      var wheelConnector = wheelSet.transform.Find("wheel_connector");

      if (!wheelAxel || !wheelLeft || !wheelRight)
      {
        Debug.LogError(
          "Wheel Set Prefab must contain wheel_axel, wheel_left, and wheel_right transforms.");
        return;
      }

      // Adjust axel scale and alignment
      var axelLength =
        (!isXBoundsAligned ? bounds.extents.x : bounds.extents.z) * 2 +
        2 * axelPadding;
      var axelScale = wheelAxel.localScale;
      axelScale.y = axelLength / 2;
      wheelAxel.localScale = axelScale;


      // Adjust wheel positions based on axel length
      // var offset = !isYAxis
      //   ? new Vector3(axelLength / 2, 0, 0)
      //   : new Vector3(0, 0, axelLength / 2);
      var wheelAxelLocalPosition = wheelAxel.localPosition;

      if (isXBoundsAligned)
        wheelAxelLocalPosition.x -= axelPadding * 2;
      else
        wheelAxelLocalPosition.x -= axelPadding * 2;
      wheelAxelLocalPosition.z = 0;

      var wheelConnectorLocalPosition = wheelConnector.localPosition;
      wheelConnectorLocalPosition.x = 0;

      wheelAxel.localPosition = wheelAxelLocalPosition;
      wheelConnector.localPosition = wheelConnectorLocalPosition;

      wheelLeft.localPosition = new Vector3(-axelLength / 2,
        wheelAxel.localPosition.y, wheelAxel.localPosition.z);
      wheelRight.localPosition = new Vector3(axelLength / 2,
        wheelAxel.localPosition.y, wheelAxel.localPosition.z);
      // if (!isYAxis)
      // {
      //   wheelLeft.localPosition = new Vector3(-axelLength / 2,
      //     wheelAxel.localPosition.y, wheelAxel.localPosition.z);
      //   wheelRight.localPosition = new Vector3(axelLength / 2,
      //     wheelAxel.localPosition.y, wheelAxel.localPosition.z);
      // }
      // else
      // {
      //   wheelLeft.localPosition = new Vector3(wheelAxel.localPosition.x,
      //     wheelAxel.localPosition.y, -axelLength / 2);
      //   wheelRight.localPosition = new Vector3(wheelAxel.localPosition.x,
      //     wheelAxel.localPosition.y, axelLength / 2);
      // }

      // Adjust wheel scale to fit bounds without colliding
      // var wheelScaleFactor = Mathf.Min(bounds.size.x, bounds.size.z) / 2;
      // wheelLeft.localScale = new Vector3(wheelScaleFactor,
      //   wheelLeft.localScale.y, wheelScaleFactor);
      // wheelRight.localScale = new Vector3(wheelScaleFactor,
      //   wheelRight.localScale.y, wheelScaleFactor);
    }

    private void UpdateWheelsFromBounds()
    {
      if (wheelModelPrefab != null)
        InstantiateWheelModelsFromPrefab(wheelColliders);
    }

    public void RunSteering()
    {
      switch (m_steeringType)
      {
        case SteeringType.Differential:
          RunDifferentialSteeringWheels();
          break;
        case SteeringType.Magic:
          RunMagicRotation();
          break;
        case SteeringType.FourWheel:
          RunFourWheelSteeringWheels();
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    /// <summary>
    ///   POWERED WHEELS
    ///   Sets the motor torque of the wheel based on forward input. This moves
    ///   the tank forwards and backwards.
    /// </summary>
    private void RunPoweredWheels()
    {
      foreach (var wheel in poweredWheels)
      {
        // To create a top speed for the tank, the motor torque just
        // cuts out when the tank starts moving fast enough.
        if (rigid.velocity.magnitude <= topSpeed)
          wheel.motorTorque = forwardInput * motorTorque;
        else
          wheel.motorTorque = 0.0f;

        // Update wheel mesh positions to match the physics wheels.
        if (wheelModelPrefab != null && WheelToTransformMap.ContainsKey(wheel))
        {
          Vector3 position;
          Quaternion rotation;
          wheel.GetWorldPose(out position, out rotation);
          WheelToTransformMap[wheel].position = position;
          WheelToTransformMap[wheel].rotation = rotation;
        }
      }
    }

    /// <summary>
    ///   DIFFERENTIAL STEERING
    ///   When turning, the left/right wheel colliders will apply an extra
    ///   torque in opposing directions and rotate the tank.
    ///   Note: Wheel sideways friction can easily prevent the tank from
    ///   rotating when this is done. Lowering side friction for wheels that
    ///   don't need it (i.e., wheels away from the center) can mitigate this.
    /// </summary>
    private void RunDifferentialSteeringWheels()
    {
      foreach (var wheel in left)
        wheel.motorTorque += motorTorque * turnInput;
      foreach (var wheel in right)
        wheel.motorTorque -= motorTorque * turnInput;
    }

    /// <summary>
    ///   FOUR WHEEL STEERING
    ///   Wheels assigned as front and rear wheels rotate to turn the tank.
    ///   This works great in motion, but will not turn the tank when standing
    ///   still.
    ///   Note: If only one set of wheels is filled out, only that set will
    ///   rotate.
    /// </summary>
    private void RunFourWheelSteeringWheels()
    {
      foreach (var wheel in front)
        wheel.steerAngle = turnInput * steeringAngle;
      foreach (var wheel in rear)
        wheel.steerAngle = -turnInput * steeringAngle;
    }

    /// <summary>
    ///   MAGIC ROTATION
    ///   Simply rotates the Rigidbody itself using a predefined rotation rate
    ///   and turning input. This has no connection to physics in any way, but
    ///   is very controllable and predictable.
    ///   Note: Since there is no connection to the physics, the tank could
    ///   turn even if it wasn't on the ground. A simple way to counter this
    ///   would be to check how many wheels are on the ground and then reduce
    ///   the turning speed depending on how many are touching the ground.
    /// </summary>
    private void RunMagicRotation()
    {
      var magicRotation = transform.rotation *
                          Quaternion.AngleAxis(
                            magicTurnRate * turnInput * Time.deltaTime,
                            transform.up);
      rigid.MoveRotation(magicRotation);
    }

    /// <summary>
    ///   Instantiates wheel model prefabs on each of the wheels and moves
    ///   them to match the physics wheels.
    /// </summary>
    /// <param name="wheels"></param>
    private void InstantiateWheelModelsFromPrefab(List<WheelCollider> wheels)
    {
      foreach (var wheel in wheels)
        // Don't double instantiate wheels. Check to make sure that this wheel doesn't already
        // have a model before instantiating one.
        if (WheelToTransformMap == null ||
            WheelToTransformMap.ContainsKey(wheel) == false)
        {
          if (WheelToTransformMap == null)
            WheelToTransformMap = new Dictionary<WheelCollider, Transform>();
          var temp = Instantiate(wheelModelPrefab, wheel.transform, false);

          // Scale the model prefab to match the radius. (Assumes prefab has diameter of 1m.)
          temp.localScale = Vector3.one * wheel.radius * 2.0f;
          WheelToTransformMap.Add(wheel, temp);
        }
    }
  }
}