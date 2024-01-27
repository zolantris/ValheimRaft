using UnityEngine;

namespace ValheimVehicles.Vehicles;

public interface IRudderControls
{
  public string m_hoverText { get; set; }

  public IVehicleShip ShipInstance { get; }

  public float m_maxUseRange { get; set; }

  // might be safer to directly make this a getter
  public Transform m_attachPoint { get; set; }
}