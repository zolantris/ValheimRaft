using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ValheimVehicles.Plugins;

public static class Vector3Logger
{
  /// <summary>
  /// Logs all Vector3 points in a Unity-friendly format for copy-pasting into the inspector.
  /// </summary>
  /// <param name="points">The list of Vector3 points to log.</param>
  public static void LogPointsForInspector(IEnumerable<Vector3> points)
  {
    if (points == null)
    {
      Debug.LogError("Points array is null!");
      return;
    }

    var result = "new Vector3[] {\n";
    foreach (var point in points)
    {
      result += $"    new Vector3({point.x}f, {point.y}f, {point.z}f),\n";
    }

    result += "}";

    Logger.LogMessage(result);
  }
}